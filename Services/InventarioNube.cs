using System.Globalization;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public interface IInventarioNube
{
    void Publicar(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas, bool reemplazarColeccion);
}

public static class InventarioNube
{
    private static readonly Motor _motor = new();

    public static string Estado => _motor.Estado;

    public static event Action? EstadoCambiado
    {
        add => _motor.EstadoCambiado += value;
        remove => _motor.EstadoCambiado -= value;
    }

    public static void Preparar(FichaRepository repositorio) => _motor.Preparar(repositorio);

    public static void Iniciar(FichaRepository repositorio) => _motor.Iniciar(repositorio);

    public static void ReemplazarCon(FichaRepository repositorio) => _motor.ReemplazarCon(repositorio);

    private sealed class Motor : IInventarioNube
    {
        private const string Proyecto = "iderma-db-2026";
        private readonly object _colaLock = new();
        private readonly Queue<Func<Task>> _cola = new();
        private readonly SemaphoreSlim _escrituras = new(1, 1);
        private readonly SemaphoreSlim _conexion = new(1, 1);
        private FichaRepository? _repositorio;
        private readonly object _cursorLock = new();
        private FirestoreDb? _base;
        private FirestoreChangeListener? _escucha;
        private FirestoreChangeListener? _escuchaBajas;
        private Task _colaEnCurso = Task.CompletedTask;
        private Timestamp _cursor;
        private Timestamp _cursorDatos;
        private Timestamp _cursorBajas;
        private bool _cambiosInicial;
        private bool _bajasInicial;
        private bool _datosMarco;
        private bool _bajasMarco;
        private int _procesandoCola;
        private int _reemplazoEnCurso;
        private bool _sincronizacionInicial;
        private int _quiereEscucha;
        private string _estado = "Inventario local";

        public string Estado => _estado;

        public event Action? EstadoCambiado;

        public void Preparar(FichaRepository repositorio)
        {
            _repositorio = repositorio;
            repositorio.Nube = this;
        }

        public void Iniciar(FichaRepository repositorio)
        {
            Preparar(repositorio);
            Volatile.Write(ref _quiereEscucha, 1);
            _ = ConectarAsync();
        }

        public void ReemplazarCon(FichaRepository repositorio)
        {
            var copia = repositorio.CopiarTodas();
            Encolar(() => ReemplazarColeccionAsync(copia));
            _ = AsegurarEscrituraAsync();
        }

        public void Publicar(IReadOnlyList<FichaTecnica> fichas, IReadOnlyCollection<Guid> eliminadas, bool reemplazarColeccion)
        {
            var copia = fichas.Select(f => f.Clonar()).ToList();
            var borradas = eliminadas.ToList();
            Encolar(() => reemplazarColeccion
                ? ReemplazarColeccionAsync(copia)
                : EscribirCambiosAsync(copia, borradas));
            _ = AsegurarEscrituraAsync();
        }

        private async Task ConectarAsync()
        {
            try
            {
                if (Volatile.Read(ref _quiereEscucha) == 1 && _escucha is null)
                {
                    CambiarEstado("Conectando el inventario con Firebase…");
                }

                await AsegurarBaseAsync();
                if (_base is null)
                {
                    if (Volatile.Read(ref _quiereEscucha) == 1)
                    {
                        CambiarEstado("Inventario local: no se encontraron las credenciales de Firebase.");
                    }

                    return;
                }

                await EsperarColaAsync();
                if (Volatile.Read(ref _quiereEscucha) != 1 || _escucha is not null)
                {
                    return;
                }

                await _conexion.WaitAsync();
                try
                {
                    if (_escucha is null && _base is not null)
                    {
                        await EscucharCambiosAsync();
                        CambiarEstado("Inventario en vivo, solo datos modificados");
                    }
                }
                finally
                {
                    _conexion.Release();
                }
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _quiereEscucha, 0);
                CambiarEstado("Inventario local: no hay conexión con Firebase. " + ex.Message);
            }
        }

        private async Task AsegurarEscrituraAsync()
        {
            try
            {
                await AsegurarBaseAsync();
                if (_base is null)
                {
                    return;
                }

                await EsperarColaAsync();
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario local: no hay conexión con Firebase. " + ex.Message);
            }
        }

        private async Task AsegurarBaseAsync()
        {
            if (_base is not null)
            {
                return;
            }

            await _conexion.WaitAsync();
            try
            {
                if (_base is not null)
                {
                    return;
                }

                var credenciales = BuscarCredenciales();
                if (credenciales is null)
                {
                    return;
                }

                _base = await new FirestoreDbBuilder
                {
                    ProjectId = Proyecto,
                    Credential = CredentialFactory.FromFile<ServiceAccountCredential>(credenciales).ToGoogleCredential()
                }.BuildAsync();
            }
            finally
            {
                _conexion.Release();
            }
        }

        private async Task EsperarColaAsync()
        {
            while (true)
            {
                Task tarea;
                lock (_colaLock)
                {
                    if (_cola.Count == 0 && Volatile.Read(ref _procesandoCola) == 0)
                    {
                        return;
                    }

                    if (_colaEnCurso.IsCompleted)
                    {
                        _colaEnCurso = ProcesarColaAsync();
                    }

                    tarea = _colaEnCurso;
                }

                await tarea;
            }
        }

        private void AlRecibir(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                if (!_sincronizacionInicial)
                {
                    _sincronizacionInicial = true;
                    if (snapshot.Count == 0)
                    {
                        var local = repositorio.CopiarTodas();
                        if (local.Count > 0)
                        {
                            Encolar(() => ReemplazarColeccionAsync(local));
                        }

                        return;
                    }

                    AplicarCompleto(repositorio, snapshot);
                    return;
                }

                var cambios = new List<FichaTecnica>();
                var eliminadas = new List<Guid>();
                foreach (var cambio in snapshot.Changes)
                {
                    if (cambio.ChangeType == DocumentChange.Type.Removed)
                    {
                        if (Guid.TryParse(cambio.Document.Id, out var id))
                        {
                            eliminadas.Add(id);
                        }

                        continue;
                    }

                    if (Leer(cambio.Document) is FichaTecnica ficha)
                    {
                        cambios.Add(ficha);
                    }
                }

                if (cambios.Count > 0 || eliminadas.Count > 0)
                {
                    repositorio.AplicarDesdeNube(cambios, eliminadas);
                }
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AplicarCompleto(FichaRepository repositorio, QuerySnapshot snapshot)
        {
            var remotos = new List<FichaTecnica>();
            var ids = new HashSet<Guid>();
            foreach (var documento in snapshot.Documents)
            {
                if (Leer(documento) is not FichaTecnica ficha)
                {
                    continue;
                }

                remotos.Add(ficha);
                ids.Add(ficha.Id);
            }

            var sobrantes = repositorio.CopiarTodas()
                .Select(f => f.Id)
                .Where(id => !ids.Contains(id))
                .ToList();
            repositorio.AplicarDesdeNube(remotos, sobrantes);
        }

        private static FichaTecnica? Leer(DocumentSnapshot documento)
        {
            if (!documento.Exists)
            {
                return null;
            }

            return InventarioDocumento.Leer(documento.Id, documento.ToDictionary());
        }

        private async Task EscribirCambiosAsync(List<FichaTecnica> fichas, List<Guid> eliminadas)
        {
            if (_base is null)
            {
                return;
            }

            await _escrituras.WaitAsync();
            try
            {
                var coleccion = _base.Collection(InventarioDocumento.Coleccion);
                var bajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas");
                await EnLotesAsync(fichas, (lote, ficha) =>
                    lote.Set(coleccion.Document(ficha.Id.ToString("D")), DatosInventario(ficha)));
                await EnLotesAsync(eliminadas, (lote, id) => lote.Delete(coleccion.Document(id.ToString("D"))));
                await EnLotesAsync(eliminadas, (lote, id) => lote.Set(bajas.Document(id.ToString("D")), MarcaBorrado()));
            }
            finally
            {
                _escrituras.Release();
            }
        }

        private async Task ReemplazarColeccionAsync(List<FichaTecnica> fichas)
        {
            if (_base is null)
            {
                return;
            }

            Interlocked.Increment(ref _reemplazoEnCurso);
            await _escrituras.WaitAsync();
            try
            {
                var coleccion = _base.Collection(InventarioDocumento.Coleccion);
                var bajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas");
                var existentes = await coleccion.GetSnapshotAsync();
                var conservar = fichas.Select(f => f.Id.ToString("D")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var borrar = existentes.Documents
                    .Where(d => !conservar.Contains(d.Id))
                    .Select(d => d.Reference)
                    .ToList();
                await EnLotesAsync(borrar, (lote, referencia) => lote.Delete(referencia));
                await EnLotesAsync(borrar, (lote, referencia) => lote.Set(bajas.Document(referencia.Id), MarcaBorrado()));
                await EnLotesAsync(fichas, (lote, ficha) =>
                    lote.Set(coleccion.Document(ficha.Id.ToString("D")), DatosInventario(ficha)));
            }
            finally
            {
                _escrituras.Release();
                Interlocked.Decrement(ref _reemplazoEnCurso);
            }
        }

        private async Task EnLotesAsync<T>(IReadOnlyList<T> items, Action<WriteBatch, T> agregar)
        {
            if (_base is null || items.Count == 0)
            {
                return;
            }

            const int tamano = 400;
            for (var inicio = 0; inicio < items.Count; inicio += tamano)
            {
                var lote = _base.StartBatch();
                var cuenta = 0;
                for (var i = inicio; i < items.Count && i < inicio + tamano; i++)
                {
                    agregar(lote, items[i]);
                    cuenta++;
                }

                if (cuenta > 0)
                {
                    await lote.CommitAsync();
                }
            }
        }

        private void Encolar(Func<Task> trabajo)
        {
            lock (_colaLock)
            {
                _cola.Enqueue(trabajo);
            }

            if (_base is not null)
            {
                ArrancarCola();
            }
        }

        private Task ArrancarCola()
        {
            lock (_colaLock)
            {
                if (_colaEnCurso.IsCompleted)
                {
                    _colaEnCurso = ProcesarColaAsync();
                }

                return _colaEnCurso;
            }
        }

        private async Task ProcesarColaAsync()
        {
            if (Interlocked.CompareExchange(ref _procesandoCola, 1, 0) != 0)
            {
                return;
            }

            try
            {
                while (_base is not null)
                {
                    Func<Task>? trabajo;
                    lock (_colaLock)
                    {
                        if (_cola.Count == 0)
                        {
                            break;
                        }

                        trabajo = _cola.Dequeue();
                    }

                    try
                    {
                        await trabajo();
                    }
                    catch (Exception ex)
                    {
                        CambiarEstado("Inventario en vivo, con un error al guardar: " + ex.Message);
                    }
                }
            }
            finally
            {
            Interlocked.Exchange(ref _procesandoCola, 0);
            lock (_colaLock)
            {
                if (_cola.Count > 0 && _base is not null)
                {
                    _colaEnCurso = ProcesarColaAsync();
                }
            }
            }
        }

        private async Task EscucharCambiosAsync()
        {
            if (_base is null || _escucha is not null)
            {
                return;
            }

            var cursor = CursorLocal.Cargar();
            if (!cursor.Listo)
            {
                var local = _repositorio?.CopiarTodas();
                if (local is { Count: > 0 })
                {
                    cursor = CursorLocal.Desde(Timestamp.FromDateTime(DateTime.UnixEpoch));
                }
                else
                {
                    var snap = await _base.Collection(InventarioDocumento.Coleccion).GetSnapshotAsync();
                    if (snap.Count > 0 && _repositorio is not null)
                    {
                        AplicarCompleto(_repositorio, snap);
                    }

                    cursor = CursorLocal.Desde(snap.ReadTime);
                }

                cursor.Guardar();
            }

            _cursor = cursor.Marca;
            _escucha = _base.Collection(InventarioDocumento.Coleccion)
                .WhereGreaterThan("Actualizado", _cursor)
                .OrderBy("Actualizado")
                .Listen(AlRecibirCambios);
            _escuchaBajas = _base.Collection(InventarioDocumento.Coleccion + "-bajas")
                .WhereGreaterThan("Actualizado", _cursor)
                .OrderBy("Actualizado")
                .Listen(AlRecibirBajas);
        }

        private void AlRecibirCambios(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                if (!_cambiosInicial)
                {
                    _cambiosInicial = true;
                    var cambios = snapshot.Documents.Select(Leer).OfType<FichaTecnica>().ToList();
                    if (cambios.Count > 0)
                    {
                        repositorio.AplicarDesdeNube(cambios, []);
                    }
                }
                else
                {
                    var cambios = new List<FichaTecnica>();
                    var eliminadas = new List<Guid>();
                    foreach (var cambio in snapshot.Changes)
                    {
                        if (cambio.ChangeType == DocumentChange.Type.Removed)
                        {
                            if (Guid.TryParse(cambio.Document.Id, out var id))
                            {
                                eliminadas.Add(id);
                            }

                            continue;
                        }

                        if (Leer(cambio.Document) is FichaTecnica ficha)
                        {
                            cambios.Add(ficha);
                        }
                    }

                    if (cambios.Count > 0 || eliminadas.Count > 0)
                    {
                        repositorio.AplicarDesdeNube(cambios, eliminadas);
                    }
                }

                AvanzarCursor(false, snapshot.ReadTime);
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AlRecibirBajas(QuerySnapshot snapshot)
        {
            var repositorio = _repositorio;
            if (Volatile.Read(ref _reemplazoEnCurso) > 0 || repositorio is null)
            {
                return;
            }

            try
            {
                var ids = (_bajasInicial ? snapshot.Changes.Select(cambio => cambio.Document) : snapshot.Documents)
                    .Select(doc => Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty)
                    .Where(id => id != Guid.Empty)
                    .ToList();
                _bajasInicial = true;
                if (ids.Count > 0)
                {
                    repositorio.AplicarDesdeNube([], ids);
                }

                AvanzarCursor(true, snapshot.ReadTime);
            }
            catch (Exception ex)
            {
                CambiarEstado("Inventario en vivo, con un error al leer: " + ex.Message);
            }
        }

        private void AvanzarCursor(bool bajas, Timestamp marca)
        {
            lock (_cursorLock)
            {
                if (bajas)
                {
                    if (_bajasMarco && marca <= _cursorBajas)
                    {
                        return;
                    }

                    _cursorBajas = marca;
                    _bajasMarco = true;
                }
                else
                {
                    if (_datosMarco && marca <= _cursorDatos)
                    {
                        return;
                    }

                    _cursorDatos = marca;
                    _datosMarco = true;
                }

                if (!_datosMarco || !_bajasMarco)
                {
                    return;
                }

                var seguro = _cursorDatos <= _cursorBajas ? _cursorDatos : _cursorBajas;
                if (seguro <= _cursor)
                {
                    return;
                }

                _cursor = seguro;
                CursorLocal.Desde(seguro).Guardar();
            }
        }

        private static Dictionary<string, object> DatosInventario(FichaTecnica ficha)
        {
            var datos = InventarioDocumento.Crear(ficha);
            datos["Actualizado"] = FieldValue.ServerTimestamp;
            return datos;
        }

        private static Dictionary<string, object> MarcaBorrado() => new()
        {
            ["Actualizado"] = FieldValue.ServerTimestamp
        };

        private void CambiarEstado(string texto)
        {
            if (_estado == texto)
            {
                return;
            }

            _estado = texto;
            EstadoCambiado?.Invoke();
        }

        private static string? BuscarCredenciales()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var cred = Path.Combine(dir.FullName, "cred");
                if (Directory.Exists(cred))
                {
                    var archivo = Directory.GetFiles(cred, "*firebase-adminsdk*.json").FirstOrDefault();
                    if (archivo is not null)
                    {
                        return archivo;
                    }
                }

                dir = dir.Parent;
            }

            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdermaCapilar",
                "firebase-adminsdk.json");
            return File.Exists(appData) ? appData : null;
        }

        private sealed class CursorLocal
        {
            public bool Listo { get; init; }

            public Timestamp Marca { get; init; }

            public static CursorLocal Cargar()
            {
                var ruta = Ruta();
                if (!File.Exists(ruta))
                {
                    return new CursorLocal();
                }

                try
                {
                    var archivo = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(ruta));
                    if (archivo is not { Listo: true })
                    {
                        return new CursorLocal();
                    }

                    return new CursorLocal
                    {
                        Listo = true,
                        Marca = Timestamp.FromDateTimeOffset(DateTimeOffset.Parse(archivo.Marca, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                    };
                }
                catch
                {
                    return new CursorLocal();
                }
            }

            public static CursorLocal Desde(Timestamp marca) => new() { Listo = true, Marca = marca };

            public void Guardar()
            {
                var ruta = Ruta();
                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                File.WriteAllText(ruta, JsonSerializer.Serialize(new Archivo
                {
                    Listo = true,
                    Marca = Marca.ToDateTimeOffset().ToString("o", CultureInfo.InvariantCulture)
                }));
            }

            private static string Ruta() =>
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IdermaCapilar",
                    "nube-cursor-inventario.json");

            private sealed class Archivo
            {
                public bool Listo { get; set; }

                public string Marca { get; set; } = string.Empty;
            }
        }
    }
}

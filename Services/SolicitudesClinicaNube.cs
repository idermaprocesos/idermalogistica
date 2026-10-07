using System.Collections.ObjectModel;
using System.Globalization;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using IdermaFichas.Models;
using Microsoft.UI.Dispatching;

namespace IdermaFichas.Services;

public static class SolicitudesClinicaNube
{
    public const string Coleccion = "solicitudes-clinica";
    private const string Proyecto = "iderma-db-2026";

    private static readonly object Candado = new();
    private static FirestoreDb? _base;
    private static FirestoreChangeListener? _escucha;
    private static int _iniciado;
    private static DispatcherQueue? _ui;
    private static readonly HashSet<string> Vistas = new(StringComparer.OrdinalIgnoreCase);
    private static string _estado = "Notificaciones de clínica";
    private static bool _escuchaInicial = true;

    public static ObservableCollection<SolicitudClinica> Items { get; } = [];

    public static string Estado => _estado;

    public static int Pendientes => Items.Count(s => s.EstaPendiente);

    public static event Action? Cambiado;

    public static event Action<SolicitudClinica>? NuevaPendiente;

    public static void Iniciar(DispatcherQueue? ui = null)
    {
        if (ui is not null)
        {
            _ui = ui;
        }

        if (Interlocked.Exchange(ref _iniciado, 1) == 1)
        {
            return;
        }

        CargarLocalEnMemoria();
        _ = ConectarAsync();
    }

    private static void CargarLocalEnMemoria()
    {
        var locales = SolicitudesLocales.Cargar();
        Items.Clear();
        Vistas.Clear();
        foreach (var item in locales)
        {
            Items.Add(item);
            Vistas.Add(item.Id);
        }

        CambiarEstado(locales.Count == 0
            ? "Notificaciones locales"
            : $"{locales.Count} en caché local · conectando…");
        Cambiado?.Invoke();
    }

    private static void PersistirLocal() => SolicitudesLocales.Guardar(Items);

    public static async Task<string> AceptarAsync(SolicitudClinica solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!solicitud.EstaPendiente)
        {
            throw new InvalidOperationException("Esa solicitud ya fue respondida.");
        }

        // Primero descuenta y deja la salida en Registro; si falla, no se acepta.
        var resumenSalidas = SalidasSolicitudClinica.Registrar(solicitud);
        await ResponderAsync(solicitud, EstadoSolicitudClinica.Aceptada, string.Empty);
        return resumenSalidas;
    }

    public static async Task DenegarAsync(SolicitudClinica solicitud, string motivo)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("Escribe el motivo de la denegación.");
        }

        await ResponderAsync(solicitud, EstadoSolicitudClinica.Denegada, motivo.Trim());
    }

    private static async Task ResponderAsync(SolicitudClinica solicitud, EstadoSolicitudClinica estado, string motivo)
    {
        await AsegurarBaseAsync();
        if (_base is null)
        {
            throw new InvalidOperationException("No hay conexión con Firebase.");
        }

        var ahora = DateTimeOffset.Now;
        var datos = new Dictionary<string, object>
        {
            ["Estado"] = estado.ToString(),
            ["Motivo denegación"] = motivo,
            ["Fecha respuesta"] = ahora.ToString("o", CultureInfo.InvariantCulture),
            ["Actualizado"] = FieldValue.ServerTimestamp
        };

        await _base.Collection(Coleccion).Document(solicitud.Id).UpdateAsync(datos);

        await EnUiAsync(() =>
        {
            solicitud.Estado = estado;
            solicitud.MotivoDenegacion = motivo;
            solicitud.FechaRespuesta = ahora;
            OrdenarLocal();
            PersistirLocal();
            Cambiado?.Invoke();
        });
    }

    private static async Task ConectarAsync()
    {
        try
        {
            CambiarEstado(Items.Count == 0
                ? "Conectando notificaciones de clínica…"
                : $"{Items.Count} en caché local · sincronizando…");
            await AsegurarBaseAsync();
            if (_base is null)
            {
                CambiarEstado(Items.Count == 0
                    ? "Notificaciones: sin credenciales de Firebase."
                    : $"{Items.Count} en caché local (sin Firebase).");
                return;
            }

            // Solo Listen: evita un GetSnapshot extra. La UI ya salió de la caché local.
            _escucha = _base.Collection(Coleccion).Listen(AlRecibir);
        }
        catch (Exception ex)
        {
            CambiarEstado(Items.Count == 0
                ? "Notificaciones: " + ex.Message
                : $"{Items.Count} en caché local · {ex.Message}");
        }
    }

    private static void AlRecibir(QuerySnapshot snapshot)
    {
        try
        {
            var recibidas = new List<(SolicitudClinica Solicitud, bool EsNuevaDoc)>();
            var eliminadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cambio in snapshot.Changes)
            {
                if (cambio.ChangeType == DocumentChange.Type.Removed)
                {
                    eliminadas.Add(cambio.Document.Id);
                    continue;
                }

                var solicitud = Leer(cambio.Document);
                if (solicitud is null)
                {
                    continue;
                }

                var esNuevaDoc = cambio.ChangeType == DocumentChange.Type.Added;
                recibidas.Add((solicitud, esNuevaDoc));
            }

            if (recibidas.Count == 0 && eliminadas.Count == 0 && !snapshot.Changes.Any())
            {
                return;
            }

            EnUi(() =>
            {
                var inicial = _escuchaInicial;
                _escuchaInicial = false;

                foreach (var id in eliminadas)
                {
                    var existente = Items.FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                    if (existente is not null)
                    {
                        Items.Remove(existente);
                        Vistas.Remove(id);
                    }
                }

                foreach (var (llegada, esNuevaDoc) in recibidas)
                {
                    var actual = Items.FirstOrDefault(s => s.Id.Equals(llegada.Id, StringComparison.OrdinalIgnoreCase));
                    var esNueva = !Vistas.Contains(llegada.Id);
                    if (actual is null)
                    {
                        Items.Insert(0, llegada);
                    }
                    else
                    {
                        actual.Folio = llegada.Folio;
                        actual.Enfermera = llegada.Enfermera;
                        actual.Fecha = llegada.Fecha;
                        actual.Urgente = llegada.Urgente;
                        actual.Items = llegada.Items;
                        actual.Estado = llegada.Estado;
                        actual.MotivoDenegacion = llegada.MotivoDenegacion;
                        actual.FechaRespuesta = llegada.FechaRespuesta;
                        actual.Origen = llegada.Origen;
                    }

                    Vistas.Add(llegada.Id);
                    // Tras la 1.ª sincronización, avisar solo pendientes realmente nuevas.
                    if (!inicial && (esNueva || esNuevaDoc) && llegada.EstaPendiente)
                    {
                        NuevaPendiente?.Invoke(llegada);
                    }
                }

                OrdenarLocal();
                PersistirLocal();
                CambiarEstado(Pendientes == 0
                    ? $"Notificaciones al día · {Items.Count} en local"
                    : $"{Pendientes} pendiente{(Pendientes == 1 ? "" : "s")} · {Items.Count} en local");
                Cambiado?.Invoke();
            });
        }
        catch
        {
        }
    }

    private static void OrdenarLocal()
    {
        var ordenadas = Items
            .OrderByDescending(s => s.EstaPendiente)
            .ThenByDescending(s => s.Urgente)
            .ThenByDescending(s => s.Fecha)
            .ToList();
        Items.Clear();
        foreach (var item in ordenadas)
        {
            Items.Add(item);
        }
    }

    private static SolicitudClinica? Leer(DocumentSnapshot doc)
    {
        if (!doc.Exists)
        {
            return null;
        }

        var datos = doc.ToDictionary();
        var items = new List<LineaSolicitudClinica>();
        if (datos.TryGetValue("Items", out var raw) && raw is IEnumerable<object> lista)
        {
            foreach (var item in lista)
            {
                if (item is not IDictionary<string, object> mapa)
                {
                    continue;
                }

                items.Add(new LineaSolicitudClinica
                {
                    Codigo = Texto(mapa, "Código", "Codigo"),
                    Area = Texto(mapa, "Área", "Area"),
                    Nombre = Texto(mapa, "Nombre", "Producto"),
                    Cantidad = Entero(mapa.TryGetValue("Cantidad", out var c) ? c : 0)
                });
            }
        }

        return new SolicitudClinica
        {
            Id = doc.Id,
            Folio = Texto(datos, "Folio"),
            Enfermera = Texto(datos, "Enfermera"),
            Fecha = Fecha(datos, "Fecha") ?? DateTimeOffset.Now,
            Urgente = Bool(datos, "Urgente"),
            Estado = ParseEstado(Texto(datos, "Estado")),
            MotivoDenegacion = Texto(datos, "Motivo denegación", "MotivoDenegacion"),
            FechaRespuesta = Fecha(datos, "Fecha respuesta", "FechaRespuesta"),
            Origen = Texto(datos, "Origen") is { Length: > 0 } origen ? origen : "clinica",
            Items = items
        };
    }

    private static EstadoSolicitudClinica ParseEstado(string texto) =>
        Enum.TryParse<EstadoSolicitudClinica>(texto, true, out var estado)
            ? estado
            : EstadoSolicitudClinica.Pendiente;

    private static async Task AsegurarBaseAsync()
    {
        if (_base is not null)
        {
            return;
        }

        lock (Candado)
        {
            if (_base is not null)
            {
                return;
            }
        }

        var credenciales = BuscarCredenciales();
        if (credenciales is null)
        {
            return;
        }

        var db = await new FirestoreDbBuilder
        {
            ProjectId = Proyecto,
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(credenciales).ToGoogleCredential()
        }.BuildAsync();

        lock (Candado)
        {
            _base ??= db;
        }
    }

    private static void CambiarEstado(string texto)
    {
        if (_estado == texto)
        {
            return;
        }

        _estado = texto;
        EnUi(() => Cambiado?.Invoke());
    }

    private static void EnUi(Action accion) => _ = EnUiAsync(accion);

    private static Task EnUiAsync(Action accion)
    {
        var cola = _ui
            ?? App.Instance.MainAppWindow?.DispatcherQueue
            ?? DispatcherQueue.GetForCurrentThread();
        if (cola is null)
        {
            accion();
            return Task.CompletedTask;
        }

        if (cola.HasThreadAccess)
        {
            accion();
            return Task.CompletedTask;
        }

        var listo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!cola.TryEnqueue(() =>
            {
                try
                {
                    accion();
                    listo.TrySetResult();
                }
                catch (Exception ex)
                {
                    listo.TrySetException(ex);
                }
            }))
        {
            accion();
            return Task.CompletedTask;
        }

        return listo.Task;
    }

    private static string Texto(IDictionary<string, object> datos, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (datos.TryGetValue(clave, out var valor) && valor is string texto)
            {
                return texto.Trim();
            }
        }

        return string.Empty;
    }

    private static bool Bool(IDictionary<string, object> datos, string clave) =>
        datos.TryGetValue(clave, out var valor) && valor switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            _ => false
        };

    private static int Entero(object? valor) => valor switch
    {
        int i => Math.Max(0, i),
        long l => (int)Math.Max(0, l),
        double d when !double.IsNaN(d) && !double.IsInfinity(d) => Math.Max(0, (int)Math.Round(d)),
        string s when int.TryParse(s, out var n) => Math.Max(0, n),
        _ => 0
    };

    private static DateTimeOffset? Fecha(IDictionary<string, object> datos, params string[] claves)
    {
        foreach (var clave in claves)
        {
            if (!datos.TryGetValue(clave, out var valor))
            {
                continue;
            }

            if (valor is Timestamp ts)
            {
                return ts.ToDateTimeOffset();
            }

            if (valor is string texto &&
                DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var fecha))
            {
                return fecha;
            }
        }

        return null;
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
}

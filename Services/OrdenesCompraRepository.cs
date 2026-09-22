using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class OrdenesCompraRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<OrdenCompraRegistrada> _ordenes = [];
    private readonly object _candado = new();

    public event EventHandler? Cambio;

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "ordenes-compra.json");

    public void Cargar()
    {
        lock (_candado)
        {
            _ordenes.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                GuardarInterno();
                return;
            }

            try
            {
                var json = File.ReadAllText(RutaArchivo);
                var cargadas = JsonSerializer.Deserialize<List<OrdenCompraRegistrada>>(json, JsonOptions);
                if (cargadas is not null)
                {
                    _ordenes.AddRange(cargadas.Where(o => o.Documento is not null));
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                GuardarInterno();
            }
        }
    }

    public IReadOnlyList<OrdenCompraRegistrada> Listar()
    {
        lock (_candado)
        {
            return _ordenes
                .OrderByDescending(o => o.FechaRegistro)
                .ThenByDescending(o => o.Id)
                .ToList();
        }
    }

    public OrdenCompraRegistrada? Obtener(Guid id)
    {
        lock (_candado)
        {
            return _ordenes.FirstOrDefault(o => o.Id == id);
        }
    }

    public void Guardar(OrdenCompraRegistrada orden)
    {
        lock (_candado)
        {
            if (orden.Id == Guid.Empty)
            {
                orden.Id = Guid.NewGuid();
            }

            if (orden.FechaRegistro == default)
            {
                orden.FechaRegistro = DateTimeOffset.Now;
            }

            orden.Documento ??= new OrdenCompraDocumento();
            var indice = _ordenes.FindIndex(o => o.Id == orden.Id);
            if (indice >= 0)
            {
                _ordenes[indice] = orden;
            }
            else
            {
                _ordenes.Add(orden);
            }

            GuardarInterno();
        }

        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public bool Eliminar(Guid id)
    {
        lock (_candado)
        {
            var quitadas = _ordenes.RemoveAll(o => o.Id == id);
            if (quitadas == 0)
            {
                return false;
            }

            GuardarInterno();
        }

        Cambio?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void GuardarInterno()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_ordenes, JsonOptions));
    }
}

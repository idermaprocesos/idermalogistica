using System.Text.Json;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public sealed class MermaRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly List<MermaRegistrada> _items = [];
    private readonly object _candado = new();

    public string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "merma.json");

    public void Cargar()
    {
        lock (_candado)
        {
            _items.Clear();
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            if (!File.Exists(RutaArchivo))
            {
                GuardarInterno();
                return;
            }

            try
            {
                var cargadas = JsonSerializer.Deserialize<List<MermaRegistrada>>(File.ReadAllText(RutaArchivo), JsonOptions);
                if (cargadas is not null)
                {
                    _items.AddRange(cargadas);
                }
            }
            catch
            {
                ArchivoAtomico.AislarDañado(RutaArchivo);
                GuardarInterno();
            }
        }
    }

    public IReadOnlyList<MermaRegistrada> Listar()
    {
        lock (_candado)
        {
            return _items.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToList();
        }
    }

    public MermaRegistrada? Obtener(Guid id)
    {
        lock (_candado)
        {
            return _items.FirstOrDefault(m => m.Id == id);
        }
    }

    public void Actualizar(MermaRegistrada merma)
    {
        if (merma.Id == Guid.Empty)
        {
            return;
        }

        lock (_candado)
        {
            var indice = _items.FindIndex(m => m.Id == merma.Id);
            if (indice >= 0)
            {
                _items[indice] = merma;
            }
            else
            {
                _items.Add(merma);
            }

            GuardarInterno();
        }

        ModulosNube.PublicarMerma([merma]);
    }

    public void Eliminar(Guid id)
    {
        if (id == Guid.Empty)
        {
            return;
        }

        lock (_candado)
        {
            if (_items.RemoveAll(m => m.Id == id) == 0)
            {
                return;
            }

            GuardarInterno();
        }

        ModulosNube.EliminarMerma(id);
    }

    public void Agregar(IEnumerable<MermaRegistrada> nuevas)
    {
        var lista = nuevas.ToList();
        lock (_candado)
        {
            foreach (var item in lista)
            {
                if (item.Id == Guid.Empty)
                {
                    item.Id = Guid.NewGuid();
                }

                if (item.Fecha == default)
                {
                    item.Fecha = DateTimeOffset.Now;
                }

                _items.Add(item);
            }

            GuardarInterno();
        }

        ModulosNube.PublicarMerma(lista);
    }

    public bool AplicarDesdeNube(IReadOnlyList<MermaRegistrada> cambios, IReadOnlyCollection<Guid> eliminados, bool reemplazar)
    {
        var huboCambio = false;
        lock (_candado)
        {
            if (reemplazar)
            {
                _items.Clear();
                _items.AddRange(cambios);
                huboCambio = true;
            }
            else
            {
                var ids = eliminados.ToHashSet();
                if (_items.RemoveAll(m => ids.Contains(m.Id)) > 0)
                {
                    huboCambio = true;
                }

                foreach (var cambio in cambios)
                {
                    var indice = _items.FindIndex(m => m.Id == cambio.Id);
                    if (indice >= 0)
                    {
                        _items[indice] = cambio;
                    }
                    else
                    {
                        _items.Add(cambio);
                    }

                    huboCambio = true;
                }
            }

            if (huboCambio)
            {
                GuardarInterno();
            }
        }

        return huboCambio;
    }

    private void GuardarInterno()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        ArchivoAtomico.EscribirTexto(RutaArchivo, JsonSerializer.Serialize(_items, JsonOptions));
    }
}

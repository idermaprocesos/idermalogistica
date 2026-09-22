using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public static class AreasOperativas
{
    public static readonly Guid IdClinica = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid IdOficina = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid IdLimpieza = Guid.Parse("33333333-3333-4333-8333-333333333333");

    public const string EtiquetaSinArea = "area:none";
    public const string PrefijoEtiqueta = "area:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static List<AreaDefinicion> _areas = CrearPredeterminadas();

    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "areas-operativas.json");

    public static IReadOnlyList<AreaDefinicion> Todas =>
        _areas.OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToList();

    public static void Cargar()
    {
        if (!File.Exists(RutaArchivo))
        {
            _areas = CrearPredeterminadas();
            Guardar();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        var cargadas = JsonSerializer.Deserialize<List<AreaDefinicion>>(json, JsonOptions);
        _areas = cargadas is { Count: > 0 } ? cargadas : CrearPredeterminadas();
        Guardar();
    }

    public static void Guardar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(_areas, JsonOptions));
    }

    public static AreaDefinicion? Obtener(Guid? id) =>
        id is Guid valor ? _areas.FirstOrDefault(a => a.Id == valor) : null;

    public static string Titulo(Guid? id) =>
        id is null ? "Sin área" : Obtener(id)?.Nombre ?? "Área";

    public static string Descripcion(Guid? id) =>
        id is null
            ? "Productos que no pertenecen a ningún área operativa."
            : Obtener(id)?.Descripcion ?? string.Empty;

    public static string Prefijo(Guid? id) =>
        Obtener(id)?.Prefijo.Trim().ToUpperInvariant() is { Length: > 0 } prefijo
            ? prefijo
            : "GEN";

    public static string Glifo(Guid? id) => Obtener(id)?.Glifo ?? "\uE8F1";

    public static AreaOperativa Plantilla(Guid? id) =>
        Obtener(id)?.Plantilla ?? AreaOperativa.Clinica;

    public static string EtiquetaMenu(Guid id) => $"{PrefijoEtiqueta}{id:D}";

    public static Guid? IdDesdeEtiqueta(string etiqueta)
    {
        if (string.Equals(etiqueta, EtiquetaSinArea, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (etiqueta.StartsWith(PrefijoEtiqueta, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(etiqueta[PrefijoEtiqueta.Length..], out var id))
        {
            return id;
        }

        return etiqueta.ToLowerInvariant() switch
        {
            "clinica" => IdClinica,
            "oficina" => IdOficina,
            "limpieza" => IdLimpieza,
            _ => null
        };
    }

    public static Guid? ResolverId(string texto)
    {
        var normal = texto.Trim();
        if (Guid.TryParse(normal, out var id))
        {
            return Obtener(id)?.Id ?? id;
        }

        var porNombre = _areas.FirstOrDefault(a =>
            a.Nombre.Equals(normal, StringComparison.CurrentCultureIgnoreCase) ||
            a.Prefijo.Equals(normal, StringComparison.OrdinalIgnoreCase));
        if (porNombre is not null)
        {
            return porNombre.Id;
        }

        return normal.ToLowerInvariant() switch
        {
            "clinica" or "clínica" or "cli" => IdClinica,
            "oficina" or "ofi" => IdOficina,
            "limpieza" or "lim" => IdLimpieza,
            _ => null
        };
    }

    public static Guid? DesdeEnumAntiguo(int valor) => valor switch
    {
        0 => IdClinica,
        1 => IdOficina,
        2 => IdLimpieza,
        _ => null
    };

    public static AreaDefinicion Agregar(string nombre, string descripcion, string prefijo, AreaOperativa plantilla, string glifo)
    {
        var area = new AreaDefinicion
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion.Trim(),
            Prefijo = PrefijoLimpio(prefijo, nombre),
            Plantilla = plantilla,
            Glifo = GlifoOElegido(glifo, plantilla),
            Orden = _areas.Count == 0 ? 1 : _areas.Max(a => a.Orden) + 1
        };
        Catalogos.CopiarListasDePlantilla(plantilla, area.Id);
        _areas.Add(area);
        Guardar();
        return area;
    }

    public static void Actualizar(Guid id, string nombre, string descripcion, string prefijo, AreaOperativa plantilla, string glifo)
    {
        var area = Obtener(id) ?? throw new InvalidOperationException("El área ya no existe.");
        area.Nombre = nombre.Trim();
        area.Descripcion = descripcion.Trim();
        area.Prefijo = PrefijoLimpio(prefijo, nombre);
        area.Plantilla = plantilla;
        area.Glifo = GlifoOElegido(glifo, plantilla);
        Guardar();
    }

    public static bool Eliminar(Guid id)
    {
        var n = _areas.RemoveAll(a => a.Id == id);
        if (n > 0)
        {
            Guardar();
        }

        return n > 0;
    }

    public static bool Mover(Guid id, int desplazamiento)
    {
        var ordenadas = _areas.OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToList();
        var indice = ordenadas.FindIndex(a => a.Id == id);
        var destino = indice + desplazamiento;
        if (indice < 0 || destino < 0 || destino >= ordenadas.Count)
        {
            return false;
        }

        (ordenadas[indice], ordenadas[destino]) = (ordenadas[destino], ordenadas[indice]);
        for (var i = 0; i < ordenadas.Count; i++)
        {
            ordenadas[i].Orden = i + 1;
        }

        Guardar();
        return true;
    }

    private static string PrefijoLimpio(string prefijo, string nombre)
    {
        var limpio = LetrasYNumeros(prefijo, 6);
        if (limpio.Length >= 2)
        {
            return limpio;
        }

        var desdeNombre = LetrasYNumeros(nombre, 3);
        return desdeNombre.Length >= 2 ? desdeNombre : "GEN";
    }

    private static string LetrasYNumeros(string texto, int maximo) =>
        new string(texto.Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(maximo)
            .ToArray());

    private static string GlifoOElegido(string glifo, AreaOperativa plantilla) =>
        string.IsNullOrWhiteSpace(glifo) ? GlifoPlantilla(plantilla) : glifo;

    private static string GlifoPlantilla(AreaOperativa plantilla) => plantilla switch
    {
        AreaOperativa.Oficina => "\uE8F1",
        AreaOperativa.Limpieza => "\uEA18",
        _ => "\uE95E"
    };

    private static List<AreaDefinicion> CrearPredeterminadas() =>
    [
        new()
        {
            Id = IdClinica,
            Nombre = "Clínica",
            Descripcion = "Instrumental, insumos estériles, medicamentos y equipos de procedimiento capilar.",
            Prefijo = "CLI",
            Glifo = "\uE95E",
            Plantilla = AreaOperativa.Clinica,
            Orden = 1
        },
        new()
        {
            Id = IdOficina,
            Nombre = "Oficina",
            Descripcion = "Papelería, archivo clínico, equipos de cómputo y mobiliario administrativo.",
            Prefijo = "OFI",
            Glifo = "\uE8F1",
            Plantilla = AreaOperativa.Oficina,
            Orden = 2
        },
        new()
        {
            Id = IdLimpieza,
            Nombre = "Materiales y Suministros",
            Descripcion = "Desinfectantes, detergentes, EPP y control de residuos de la clínica.",
            Prefijo = "LIM",
            Glifo = "\uEA18",
            Plantilla = AreaOperativa.Limpieza,
            Orden = 3
        }
    ];
}

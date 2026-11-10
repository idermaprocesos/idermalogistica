using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdermaFichas.Models;

public static class AreasOperativas
{
    public static readonly Guid IdClinica = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid IdOficina = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid IdLimpieza = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid IdInsumosMedicos = IdClinica;
    public static readonly Guid IdProductosVenta = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid IdLaboratorio = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public static readonly Guid IdMobiliario = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public static readonly Guid IdTecnologia = Guid.Parse("77777777-7777-4777-8777-777777777777");
    public static readonly Guid IdInfraestructura = Guid.Parse("88888888-8888-4888-8888-888888888888");
    public static readonly Guid IdArticulosOficina = Guid.Parse("99999999-9999-4999-8999-999999999999");
    public static readonly Guid IdFolletos = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    public static readonly Guid IdCatActivosFijos = Guid.Parse("b1111111-1111-4111-8111-111111111111");
    public static readonly Guid IdCatInsumosMedicos = Guid.Parse("b2222222-2222-4222-8222-222222222222");
    public static readonly Guid IdCatProductosVentas = Guid.Parse("b3333333-3333-4333-8333-333333333333");
    public static readonly Guid IdCatMateriales = Guid.Parse("b4444444-4444-4444-8444-444444444444");
    public static readonly Guid IdCatComunicacion = Guid.Parse("b5555555-5555-4555-8555-555555555555");

    public const string EtiquetaSinArea = "area:none";
    public const string PrefijoEtiqueta = "area:";
    public const string PrefijoCategoria = "categoria:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly object Candado = new();
    private static List<CategoriaMenuDefinicion> _categorias = CrearCategoriasPredeterminadas();
    private static List<AreaDefinicion> _areas = CrearAreasPredeterminadas();

    public static bool ArchivoExistiaAlAbrir { get; private set; }

    public static event Action? Cambiaron;

    public static event Action? PublicacionPedida;

    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "areas-operativas.json");

    public static IReadOnlyList<CategoriaMenuDefinicion> Categorias
    {
        get
        {
            List<CategoriaMenuDefinicion> copia;
            lock (Candado)
            {
                copia = [.. _categorias];
            }

            return copia.OrderBy(c => c.Orden).ThenBy(c => c.Nombre).ToList();
        }
    }

    public static IReadOnlyList<AreaDefinicion> Todas
    {
        get
        {
            List<AreaDefinicion> copia;
            lock (Candado)
            {
                copia = [.. _areas];
            }

            return copia.OrderBy(a => a.Orden).ThenBy(a => a.Nombre).ToList();
        }
    }

    public static IReadOnlyList<AreaDefinicion> DeCategoria(Guid? categoriaId)
    {
        return Todas.Where(a => a.CategoriaId == categoriaId).ToList();
    }

    public static void Cargar()
    {
        ArchivoExistiaAlAbrir = File.Exists(RutaArchivo);
        if (!ArchivoExistiaAlAbrir)
        {
            lock (Candado)
            {
                _categorias = CrearCategoriasPredeterminadas();
                _areas = CrearAreasPredeterminadas();
            }

            EscribirArchivo();
            NormalizarGlifos();
            AsegurarListas();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        CargarDesdeJson(json);
        MigrarEstructuraSiHaceFalta();
        EscribirArchivo();
        NormalizarGlifos();
        AsegurarListas();
    }

    public static void ReemplazarDesdeNube(IReadOnlyList<AreaDefinicion> areas, IReadOnlyList<CategoriaMenuDefinicion>? categorias = null)
    {
        var entrantes = areas.ToList();
        var cats = categorias?.ToList();
        lock (Candado)
        {
            if (cats is null)
            {
                if (MismaLista(_areas, entrantes))
                {
                    return;
                }

                _areas = entrantes;
            }
            else if (MismaLista(_areas, entrantes) && MismaListaCategorias(_categorias, cats))
            {
                return;
            }
            else
            {
                _categorias = cats;
                _areas = entrantes;
            }
        }

        MigrarEstructuraSiHaceFalta();
        EscribirArchivo();
        foreach (var area in Todas)
        {
            if (!Catalogos.TieneListaPropia(area.Id, Catalogos.CampoCategorias))
            {
                Catalogos.CopiarListasDePlantilla(area.Plantilla, area.Id);
            }
        }

        Cambiaron?.Invoke();
    }

    public static void ReemplazarCategoriasDesdeNube(IReadOnlyList<CategoriaMenuDefinicion> categorias)
    {
        var entrantes = categorias.ToList();
        lock (Candado)
        {
            if (MismaListaCategorias(_categorias, entrantes))
            {
                return;
            }

            _categorias = entrantes;
        }

        MigrarEstructuraSiHaceFalta();
        EscribirArchivo();
        Cambiaron?.Invoke();
    }

    private static void Guardar()
    {
        EscribirArchivo();
        PublicacionPedida?.Invoke();
        Cambiaron?.Invoke();
    }

    private static void EscribirArchivo()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        AreasOperativasArchivo archivo;
        lock (Candado)
        {
            archivo = new AreasOperativasArchivo
            {
                Categorias = [.. _categorias],
                Areas = [.. _areas]
            };
        }

        File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(archivo, JsonOptions));
    }

    private static void CargarDesdeJson(string json)
    {
        using var documento = JsonDocument.Parse(json);
        if (documento.RootElement.ValueKind == JsonValueKind.Array)
        {
            var cargadas = JsonSerializer.Deserialize<List<AreaDefinicion>>(json, JsonOptions);
            lock (Candado)
            {
                _categorias = [];
                _areas = cargadas is { Count: > 0 } ? cargadas : CrearAreasPredeterminadas();
            }

            return;
        }

        var archivo = JsonSerializer.Deserialize<AreasOperativasArchivo>(json, JsonOptions);
        lock (Candado)
        {
            _categorias = archivo?.Categorias is { Count: > 0 }
                ? archivo.Categorias
                : [];
            _areas = archivo?.Areas is { Count: > 0 }
                ? archivo.Areas
                : CrearAreasPredeterminadas();
        }
    }

    private static void MigrarEstructuraSiHaceFalta()
    {
        lock (Candado)
        {
            if (_categorias.Count == 0)
            {
                _categorias = CrearCategoriasPredeterminadas();
                foreach (var area in _areas)
                {
                    area.CategoriaId ??= InferirCategoriaId(area);
                }

                RenombrarSeccionesClasicas();
                CompletarSeccionesFaltantes();
                return;
            }

            foreach (var area in _areas.Where(a => a.CategoriaId is null))
            {
                area.CategoriaId = InferirCategoriaId(area);
            }
        }
    }

    private static void RenombrarSeccionesClasicas()
    {
        RenombrarSi(IdInsumosMedicos, "Insumos Médicos", "Enfermería");
        RenombrarSi(IdProductosVenta, "Productos de Venta", "Botica");
        RenombrarSi(IdLimpieza, "Materiales y Suministros", "Limpieza");
    }

    private static void RenombrarSi(Guid id, string nombreActual, string nombreNuevo)
    {
        var area = _areas.FirstOrDefault(a => a.Id == id);
        if (area is not null &&
            area.Nombre.Equals(nombreActual, StringComparison.CurrentCultureIgnoreCase))
        {
            area.Nombre = nombreNuevo;
        }
    }

    private static void CompletarSeccionesFaltantes()
    {
        foreach (var area in CrearAreasPredeterminadas())
        {
            if (_areas.Any(a => a.Id == area.Id))
            {
                continue;
            }

            if (_areas.Any(a => a.Nombre.Equals(area.Nombre, StringComparison.CurrentCultureIgnoreCase)))
            {
                continue;
            }

            area.Orden = _areas.Count == 0 ? 1 : _areas.Max(x => x.Orden) + 1;
            _areas.Add(area);
        }
    }

    private static Guid? InferirCategoriaId(AreaDefinicion area)
    {
        if (area.Id == IdMobiliario || area.Id == IdTecnologia || area.Id == IdInfraestructura)
        {
            return IdCatActivosFijos;
        }

        if (area.Id == IdInsumosMedicos)
        {
            return IdCatInsumosMedicos;
        }

        if (area.Id == IdProductosVenta)
        {
            return IdCatProductosVentas;
        }

        if (area.Id == IdLimpieza || area.Id == IdArticulosOficina || area.Id == IdOficina)
        {
            return IdCatMateriales;
        }

        if (area.Id == IdFolletos)
        {
            return IdCatComunicacion;
        }

        var nombre = area.Nombre.Trim();
        if (Coincide(nombre, "mobiliario", "tecnolog", "infraestructura", "activo"))
        {
            return IdCatActivosFijos;
        }

        if (Coincide(nombre, "insumo", "enfermer", "clínica", "clinica"))
        {
            return IdCatInsumosMedicos;
        }

        if (Coincide(nombre, "venta", "botica", "producto"))
        {
            return IdCatProductosVentas;
        }

        if (Coincide(nombre, "limpieza", "material", "suministro", "oficina", "artículo", "articulo"))
        {
            return IdCatMateriales;
        }

        if (Coincide(nombre, "comunic", "follet", "folleter"))
        {
            return IdCatComunicacion;
        }

        return IdCatActivosFijos;
    }

    private static bool Coincide(string nombre, params string[] fragmentos) =>
        fragmentos.Any(f => nombre.Contains(f, StringComparison.CurrentCultureIgnoreCase));

    public static CategoriaMenuDefinicion? ObtenerCategoria(Guid? id)
    {
        if (id is not Guid valor)
        {
            return null;
        }

        lock (Candado)
        {
            return _categorias.FirstOrDefault(c => c.Id == valor);
        }
    }

    public static string TituloCategoria(Guid? id) =>
        ObtenerCategoria(id)?.Nombre ?? "Sin categoría";

    public static string EtiquetaCategoria(Guid id) => $"{PrefijoCategoria}{id:D}";

    public static AreaDefinicion? Obtener(Guid? id)
    {
        if (id is not Guid valor)
        {
            return null;
        }

        lock (Candado)
        {
            return _areas.FirstOrDefault(a => a.Id == valor);
        }
    }

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

        AreaDefinicion? porNombre;
        lock (Candado)
        {
            porNombre = _areas.FirstOrDefault(a =>
                a.Nombre.Equals(normal, StringComparison.CurrentCultureIgnoreCase) ||
                a.Prefijo.Equals(normal, StringComparison.OrdinalIgnoreCase));
        }
        if (porNombre is not null)
        {
            return porNombre.Id;
        }

        return normal.ToLowerInvariant() switch
        {
            "clinica" or "clínica" or "cli" or "enfermería" or "enfermeria" => IdClinica,
            "oficina" or "ofi" => IdOficina,
            "limpieza" or "lim" => IdLimpieza,
            "botica" => IdProductosVenta,
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

    public static CategoriaMenuDefinicion AgregarCategoria(string nombre, string glifo)
    {
        var categoria = new CategoriaMenuDefinicion
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Glifo = GlifoOElegido(glifo, AreaOperativa.Oficina)
        };
        lock (Candado)
        {
            categoria.Orden = _categorias.Count == 0 ? 1 : _categorias.Max(c => c.Orden) + 1;
            _categorias.Add(categoria);
        }

        Guardar();
        return categoria;
    }

    public static void ActualizarCategoria(Guid id, string nombre, string glifo)
    {
        var categoria = ObtenerCategoria(id) ?? throw new InvalidOperationException("La categoría ya no existe.");
        categoria.Nombre = nombre.Trim();
        categoria.Glifo = GlifoOElegido(glifo, AreaOperativa.Oficina);
        Guardar();
    }

    public static bool EliminarCategoria(Guid id)
    {
        lock (Candado)
        {
            if (_areas.Any(a => a.CategoriaId == id))
            {
                return false;
            }

            if (_categorias.RemoveAll(c => c.Id == id) == 0)
            {
                return false;
            }
        }

        Guardar();
        return true;
    }

    public static bool MoverCategoria(Guid id, int desplazamiento)
    {
        lock (Candado)
        {
            var ordenadas = _categorias.OrderBy(c => c.Orden).ThenBy(c => c.Nombre).ToList();
            var indice = ordenadas.FindIndex(c => c.Id == id);
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

            _categorias = ordenadas;
        }

        Guardar();
        return true;
    }

    public static AreaDefinicion Agregar(
        string nombre,
        string descripcion,
        string prefijo,
        AreaOperativa plantilla,
        string glifo,
        Guid? categoriaId = null)
    {
        var area = new AreaDefinicion
        {
            Id = Guid.NewGuid(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion.Trim(),
            Prefijo = PrefijoLimpio(prefijo, nombre),
            Plantilla = plantilla,
            Glifo = GlifoOElegido(glifo, plantilla),
            CategoriaId = categoriaId ?? Categorias.FirstOrDefault()?.Id
        };
        Catalogos.CopiarListasDePlantilla(plantilla, area.Id);
        lock (Candado)
        {
            area.Orden = _areas.Count == 0 ? 1 : _areas.Max(a => a.Orden) + 1;
            _areas.Add(area);
        }

        Guardar();
        return area;
    }

    public static void Actualizar(
        Guid id,
        string nombre,
        string descripcion,
        string prefijo,
        AreaOperativa plantilla,
        string glifo,
        Guid? categoriaId = null)
    {
        var area = Obtener(id) ?? throw new InvalidOperationException("El área ya no existe.");
        area.Nombre = nombre.Trim();
        area.Descripcion = descripcion.Trim();
        area.Prefijo = PrefijoLimpio(prefijo, nombre);
        area.Plantilla = plantilla;
        area.Glifo = GlifoOElegido(glifo, plantilla);
        area.CategoriaId = categoriaId;
        Guardar();
    }

    public static void CambiarCategoria(Guid areaId, Guid? categoriaId)
    {
        var area = Obtener(areaId) ?? throw new InvalidOperationException("El área ya no existe.");
        area.CategoriaId = categoriaId;
        Guardar();
    }

    public static bool Eliminar(Guid id)
    {
        int n;
        lock (Candado)
        {
            n = _areas.RemoveAll(a => a.Id == id);
        }

        if (n > 0)
        {
            Guardar();
        }

        return n > 0;
    }

    public static bool Mover(Guid id, int desplazamiento)
    {
        lock (Candado)
        {
            var area = _areas.FirstOrDefault(a => a.Id == id);
            if (area is null)
            {
                return false;
            }

            var hermanas = _areas
                .Where(a => a.CategoriaId == area.CategoriaId)
                .OrderBy(a => a.Orden)
                .ThenBy(a => a.Nombre)
                .ToList();
            var indice = hermanas.FindIndex(a => a.Id == id);
            var destino = indice + desplazamiento;
            if (indice < 0 || destino < 0 || destino >= hermanas.Count)
            {
                return false;
            }

            (hermanas[indice], hermanas[destino]) = (hermanas[destino], hermanas[indice]);
            for (var i = 0; i < hermanas.Count; i++)
            {
                hermanas[i].Orden = i + 1;
            }

            var otras = _areas.Where(a => a.CategoriaId != area.CategoriaId).ToList();
            var ordenBase = otras.Count == 0 ? 0 : otras.Max(a => a.Orden);
            for (var i = 0; i < hermanas.Count; i++)
            {
                hermanas[i].Orden = ordenBase + i + 1;
            }

            _areas = [.. otras, .. hermanas];
        }

        Guardar();
        return true;
    }

    private static bool MismaLista(IReadOnlyList<AreaDefinicion> actual, IReadOnlyList<AreaDefinicion> entrante)
    {
        if (actual.Count != entrante.Count)
        {
            return false;
        }

        foreach (var area in entrante)
        {
            var previa = actual.FirstOrDefault(a => a.Id == area.Id);
            if (previa is null ||
                previa.Orden != area.Orden ||
                previa.Plantilla != area.Plantilla ||
                previa.CategoriaId != area.CategoriaId ||
                !string.Equals(previa.Nombre, area.Nombre, StringComparison.Ordinal) ||
                !string.Equals(previa.Descripcion, area.Descripcion, StringComparison.Ordinal) ||
                !string.Equals(previa.Prefijo, area.Prefijo, StringComparison.Ordinal) ||
                !string.Equals(previa.Glifo, area.Glifo, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MismaListaCategorias(
        IReadOnlyList<CategoriaMenuDefinicion> actual,
        IReadOnlyList<CategoriaMenuDefinicion> entrante)
    {
        if (actual.Count != entrante.Count)
        {
            return false;
        }

        foreach (var categoria in entrante)
        {
            var previa = actual.FirstOrDefault(c => c.Id == categoria.Id);
            if (previa is null ||
                previa.Orden != categoria.Orden ||
                !string.Equals(previa.Nombre, categoria.Nombre, StringComparison.Ordinal) ||
                !string.Equals(previa.Glifo, categoria.Glifo, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public static bool DifiereDePredeterminadas()
    {
        List<AreaDefinicion> areas;
        List<CategoriaMenuDefinicion> categorias;
        lock (Candado)
        {
            areas = [.. _areas];
            categorias = [.. _categorias];
        }

        return !MismaLista(CrearAreasPredeterminadas(), areas) ||
               !MismaListaCategorias(CrearCategoriasPredeterminadas(), categorias);
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
        string.IsNullOrWhiteSpace(glifo) ? GlifoPlantilla(plantilla) : GlifosWinUi.Caracter(glifo);

    private static string GlifoPlantilla(AreaOperativa plantilla) => plantilla switch
    {
        AreaOperativa.Oficina => "\uE8F1",
        AreaOperativa.Limpieza => "\uEA18",
        _ => "\uE95E"
    };

    private static void NormalizarGlifos()
    {
        lock (Candado)
        {
            foreach (var area in _areas)
            {
                area.Glifo = GlifosWinUi.Caracter(area.Glifo);
            }

            foreach (var categoria in _categorias)
            {
                categoria.Glifo = GlifosWinUi.Caracter(categoria.Glifo);
            }
        }
    }

    private static void AsegurarListas()
    {
        foreach (var area in Todas)
        {
            if (!Catalogos.TieneListaPropia(area.Id, Catalogos.CampoCategorias))
            {
                Catalogos.CopiarListasDePlantilla(area.Plantilla, area.Id);
            }
        }

        if (!Catalogos.TieneListaPropia(null, Catalogos.CampoCategorias))
        {
            Catalogos.CopiarListasDePlantilla(AreaOperativa.Clinica, null);
        }
    }

    private static List<CategoriaMenuDefinicion> CrearCategoriasPredeterminadas() =>
    [
        new()
        {
            Id = IdCatActivosFijos,
            Nombre = "Activos fijos",
            Glifo = "\uE8F1",
            Orden = 1
        },
        new()
        {
            Id = IdCatInsumosMedicos,
            Nombre = "Insumos médicos",
            Glifo = "\uE95E",
            Orden = 2
        },
        new()
        {
            Id = IdCatProductosVentas,
            Nombre = "Productos de Ventas",
            Glifo = "\uE7BF",
            Orden = 3
        },
        new()
        {
            Id = IdCatMateriales,
            Nombre = "Materiales y suministros",
            Glifo = "\uEA18",
            Orden = 4
        },
        new()
        {
            Id = IdCatComunicacion,
            Nombre = "Comunicación y folletería",
            Glifo = "\uE8A5",
            Orden = 5
        }
    ];

    private static List<AreaDefinicion> CrearAreasPredeterminadas() =>
    [
        new()
        {
            Id = IdMobiliario,
            Nombre = "Mobiliario",
            Descripcion = "Muebles y mobiliario clínico u operativo.",
            Prefijo = "MOB",
            Glifo = "\uE8F1",
            Plantilla = AreaOperativa.Oficina,
            CategoriaId = IdCatActivosFijos,
            Orden = 1
        },
        new()
        {
            Id = IdTecnologia,
            Nombre = "Tecnología",
            Descripcion = "Equipos tecnológicos y de cómputo.",
            Prefijo = "TEC",
            Glifo = "\uE950",
            Plantilla = AreaOperativa.Oficina,
            CategoriaId = IdCatActivosFijos,
            Orden = 2
        },
        new()
        {
            Id = IdInfraestructura,
            Nombre = "Infraestructura",
            Descripcion = "Infraestructura y mejoras de las instalaciones.",
            Prefijo = "INF",
            Glifo = "\uE715",
            Plantilla = AreaOperativa.Oficina,
            CategoriaId = IdCatActivosFijos,
            Orden = 3
        },
        new()
        {
            Id = IdInsumosMedicos,
            Nombre = "Enfermería",
            Descripcion = "Instrumental, insumos estériles, medicamentos y equipos de procedimiento capilar.",
            Prefijo = "CDE",
            Glifo = "\uE95E",
            Plantilla = AreaOperativa.Clinica,
            CategoriaId = IdCatInsumosMedicos,
            Orden = 4
        },
        new()
        {
            Id = IdProductosVenta,
            Nombre = "Botica",
            Descripcion = "Productos de venta y botica.",
            Prefijo = "CLB",
            Glifo = "\uE7BF",
            Plantilla = AreaOperativa.Clinica,
            CategoriaId = IdCatProductosVentas,
            Orden = 5
        },
        new()
        {
            Id = IdLimpieza,
            Nombre = "Limpieza",
            Descripcion = "Desinfectantes, detergentes, EPP y control de residuos de la clínica.",
            Prefijo = "CDLM",
            Glifo = "\uEA18",
            Plantilla = AreaOperativa.Limpieza,
            CategoriaId = IdCatMateriales,
            Orden = 6
        },
        new()
        {
            Id = IdArticulosOficina,
            Nombre = "Artículos de oficina",
            Descripcion = "Papelería, útiles y artículos de oficina.",
            Prefijo = "OFI",
            Glifo = "\uE8A5",
            Plantilla = AreaOperativa.Oficina,
            CategoriaId = IdCatMateriales,
            Orden = 7
        },
        new()
        {
            Id = IdFolletos,
            Nombre = "Folletos",
            Descripcion = "Folletos, volantes y material de comunicación.",
            Prefijo = "FOL",
            Glifo = "\uE8A5",
            Plantilla = AreaOperativa.Oficina,
            CategoriaId = IdCatComunicacion,
            Orden = 8
        }
    ];
}

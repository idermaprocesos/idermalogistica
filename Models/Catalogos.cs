using System.Text.Json;

namespace IdermaFichas.Models;

public sealed record ListaDesplegable(string Clave, string Titulo);

public static class Catalogos
{
    public const string CampoCategorias = "categorias";
    public const string CampoEstados = "estadosFisicos";
    public const string CampoUnidades = "unidades";
    public const string CampoPresentaciones = "presentaciones";
    public const string CampoColores = "colores";
    public const string CampoMateriales = "materiales";
    public const string CampoVidasUtil = "vidasUtil";
    public const string CampoPaises = "paises";
    public const string CampoTiposOrigen = "tiposOrigen";
    public const string CampoProcedimientos = "procedimientos";
    public const string CampoRiesgos = "riesgosBiologicos";
    public const string CampoTemperaturas = "temperaturas";
    public const string CampoPeligrosidad = "peligrosidad";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static Dictionary<string, List<string>> _listas = CrearPredeterminadas();

    public static string RutaArchivo { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IdermaCapilar",
        "desplegables.json");

    public static string Clave(Guid? areaId, string campo)
    {
        var slug = areaId == AreasOperativas.IdClinica ? "clinica"
            : areaId == AreasOperativas.IdOficina ? "oficina"
            : areaId == AreasOperativas.IdLimpieza ? "limpieza"
            : areaId is Guid id ? "a" + id.ToString("N")
            : "sinarea";
        return $"{slug}_{campo}";
    }

    public static string Clave(AreaOperativa plantilla, string campo) =>
        Clave(plantilla switch
        {
            AreaOperativa.Oficina => AreasOperativas.IdOficina,
            AreaOperativa.Limpieza => AreasOperativas.IdLimpieza,
            _ => AreasOperativas.IdClinica
        }, campo);

    public static IReadOnlyList<ListaDesplegable> ListasDe(Guid? areaId) =>
        ListasDePlantilla(AreasOperativas.Plantilla(areaId), areaId);

    public static IReadOnlyList<ListaDesplegable> ListasDe(AreaOperativa plantilla) =>
        ListasDePlantilla(plantilla, plantilla switch
        {
            AreaOperativa.Oficina => AreasOperativas.IdOficina,
            AreaOperativa.Limpieza => AreasOperativas.IdLimpieza,
            _ => AreasOperativas.IdClinica
        });

    private static IReadOnlyList<ListaDesplegable> ListasDePlantilla(AreaOperativa plantilla, Guid? areaId)
    {
        var listas = new List<ListaDesplegable>
        {
            new(Clave(areaId, CampoCategorias), "Categoría"),
            new(Clave(areaId, CampoEstados), "Estado físico"),
            new(Clave(areaId, CampoUnidades), "Unidad de medida"),
            new(Clave(areaId, CampoPresentaciones), "Presentación"),
            new(Clave(areaId, CampoColores), "Color / apariencia"),
            new(Clave(areaId, CampoMateriales), "Material de fabricación"),
            new(Clave(areaId, CampoVidasUtil), "Vida útil"),
            new(Clave(areaId, CampoPaises), "País de origen"),
            new(Clave(areaId, CampoTiposOrigen), "Tipo de origen")
        };

        if (plantilla == AreaOperativa.Clinica)
        {
            listas.Add(new(Clave(areaId, CampoProcedimientos), "Procedimiento"));
            listas.Add(new(Clave(areaId, CampoRiesgos), "Riesgo biológico"));
            listas.Add(new(Clave(areaId, CampoTemperaturas), "Temperatura de almacenamiento"));
        }

        if (plantilla == AreaOperativa.Limpieza)
        {
            listas.Add(new(Clave(areaId, CampoPeligrosidad), "Peligrosidad"));
        }

        return listas;
    }

    public static void CopiarListasDePlantilla(AreaOperativa plantilla, Guid destino)
    {
        var origenId = plantilla switch
        {
            AreaOperativa.Oficina => AreasOperativas.IdOficina,
            AreaOperativa.Limpieza => AreasOperativas.IdLimpieza,
            _ => AreasOperativas.IdClinica
        };

        foreach (var lista in ListasDePlantilla(plantilla, origenId))
        {
            var campo = lista.Clave[(lista.Clave.IndexOf('_') + 1)..];
            var valores = Copia(lista.Clave);
            if (valores.Count == 0)
            {
                valores = Copia(Clave(plantilla, campo));
            }

            Reemplazar(Clave(destino, campo), valores);
        }
    }

    public static void Cargar()
    {
        _listas = CrearPredeterminadas();
        if (!File.Exists(RutaArchivo))
        {
            Guardar();
            return;
        }

        var json = File.ReadAllText(RutaArchivo);
        var cargadas = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, JsonOptions);
        if (cargadas is null)
        {
            return;
        }

        foreach (var (clave, valores) in cargadas)
        {
            if (valores.Count > 0)
            {
                _listas[clave] = [.. valores.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.CurrentCultureIgnoreCase)];
            }
        }

        MigrarClavesAntiguas(cargadas);
    }

    public static void Guardar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
        File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(_listas, JsonOptions));
    }

    public static IReadOnlyList<string> Obtener(string clave) =>
        _listas.TryGetValue(clave, out var lista) ? lista : [];

    public static IReadOnlyList<string> Obtener(Guid? areaId, string campo) =>
        Obtener(Clave(areaId, campo));

    public static IReadOnlyList<string> Obtener(AreaOperativa plantilla, string campo) =>
        Obtener(Clave(plantilla, campo));

    public static List<string> Copia(string clave) => [.. Obtener(clave)];

    public static void Reemplazar(string clave, IEnumerable<string> valores)
    {
        _listas[clave] =
        [
            .. valores
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
        ];
        Guardar();
    }

    public static void Restaurar(string clave)
    {
        var originales = CrearPredeterminadas();
        if (originales.TryGetValue(clave, out var lista))
        {
            _listas[clave] = [.. lista];
            Guardar();
        }
    }

    public static IReadOnlyList<string> CategoriasDe(Guid? areaId) =>
        Obtener(areaId, CampoCategorias);

    public static string PrefijoCodigo(Guid? areaId) => AreasOperativas.Prefijo(areaId);

    public static string Titulo(Guid? areaId) => AreasOperativas.Titulo(areaId);

    public static string Descripcion(Guid? areaId) => AreasOperativas.Descripcion(areaId);

    private static void MigrarClavesAntiguas(Dictionary<string, List<string>> cargadas)
    {
        CopiarSiFalta("categoriasClinica", Clave(AreaOperativa.Clinica, CampoCategorias), cargadas);
        CopiarSiFalta("categoriasOficina", Clave(AreaOperativa.Oficina, CampoCategorias), cargadas);
        CopiarSiFalta("categoriasLimpieza", Clave(AreaOperativa.Limpieza, CampoCategorias), cargadas);

        foreach (var area in new[] { AreaOperativa.Clinica, AreaOperativa.Oficina, AreaOperativa.Limpieza })
        {
            CopiarSiFalta("unidades", Clave(area, CampoUnidades), cargadas);
            CopiarSiFalta("estadosFisicos", Clave(area, CampoEstados), cargadas);
            CopiarSiFalta("presentaciones", Clave(area, CampoPresentaciones), cargadas);
            CopiarSiFalta("colores", Clave(area, CampoColores), cargadas);
            CopiarSiFalta("materiales", Clave(area, CampoMateriales), cargadas);
            CopiarSiFalta("vidasUtil", Clave(area, CampoVidasUtil), cargadas);
            CopiarSiFalta("paises", Clave(area, CampoPaises), cargadas);
            CopiarSiFalta("tiposOrigen", Clave(area, CampoTiposOrigen), cargadas);
        }

        CopiarSiFalta("procedimientosClinicos", Clave(AreaOperativa.Clinica, CampoProcedimientos), cargadas);
        CopiarSiFalta("riesgosBiologicos", Clave(AreaOperativa.Clinica, CampoRiesgos), cargadas);
        CopiarSiFalta("temperaturas", Clave(AreaOperativa.Clinica, CampoTemperaturas), cargadas);
        CopiarSiFalta("peligrosidad", Clave(AreaOperativa.Limpieza, CampoPeligrosidad), cargadas);
        Guardar();
    }

    private static void CopiarSiFalta(string origen, string destino, Dictionary<string, List<string>> cargadas)
    {
        if (cargadas.TryGetValue(destino, out var ya) && ya.Count > 0)
        {
            return;
        }

        if (cargadas.TryGetValue(origen, out var valores) && valores.Count > 0)
        {
            _listas[destino] = [.. valores];
        }
    }

    private static Dictionary<string, List<string>> CrearPredeterminadas()
    {
        var categoriasClinica = new List<string>
        {
            "Instrumental quirúrgico", "Insumo estéril", "Medicamento tópico", "Anestésico",
            "Equipo médico", "Implante / punch", "Material de curación", "Bioseguridad clínica",
            "Solución / irrigación", "Consumible de procedimiento"
        };
        var categoriasOficina = new List<string>
        {
            "Papelería", "Expediente y archivo", "Equipo informático", "Mobiliario",
            "Consumible administrativo", "Comunicación", "Identificación de paciente", "Software / licencia"
        };
        var categoriasLimpieza = new List<string>
        {
            "Desinfectante", "Detergente", "EPP de limpieza", "Utensilio",
            "Manejo de residuos", "Higiene de manos", "Ambientación", "Bioseguridad ambiental"
        };
        var estados = new List<string> { "Sólido", "Líquido", "Gel", "Crema", "Polvo", "Kit / set", "Equipo", "Textil", "Gas" };
        var unidades = new List<string> { "Pieza", "Caja", "Paquete", "Frasco", "Ampolla", "Vial", "Litro", "Mililitro", "Kilogramo", "Metro", "Rollo", "Par" };
        var presentaciones = new List<string> { "Unidad", "Caja", "Frasco", "Ampolla", "Vial", "Blister", "Set / kit", "Bolsa", "Bidón", "Garrafa", "Paquete", "Rollo" };
        var colores = new List<string>
        {
            "Incoloro", "Transparente", "Blanco", "Negro", "Amarillo pálido", "Ámbar",
            "Azul", "Verde institucional", "Rojo", "Plata", "Acero mate"
        };
        var materiales = new List<string>
        {
            "Acero inoxidable", "Vidrio tipo I", "PVC grado médico", "Polietileno",
            "Polímero médico", "Cartulina", "Aluminio", "Solución acuosa", "Textil"
        };
        var vidas = new List<string> { "12 meses", "24 meses", "36 meses", "5 años", "Hasta agotar ciclos", "Indefinida en almacén" };
        var paises = new List<string>
        {
            "México", "Colombia", "España", "Estados Unidos", "Alemania", "Brasil", "China",
            "India", "Francia", "Italia", "Corea del Sur", "Japón", "Suiza", "Reino Unido"
        };
        var origenes = new List<string> { "Nacional", "Importado", "Ensamblado localmente" };

        var listas = new Dictionary<string, List<string>>();
        void Copiar(AreaOperativa area, string campo, List<string> valores) =>
            listas[Clave(area, campo)] = [.. valores];

        foreach (var area in new[] { AreaOperativa.Clinica, AreaOperativa.Oficina, AreaOperativa.Limpieza })
        {
            Copiar(area, CampoEstados, estados);
            Copiar(area, CampoUnidades, unidades);
            Copiar(area, CampoPresentaciones, presentaciones);
            Copiar(area, CampoColores, colores);
            Copiar(area, CampoMateriales, materiales);
            Copiar(area, CampoVidasUtil, vidas);
            Copiar(area, CampoPaises, paises);
            Copiar(area, CampoTiposOrigen, origenes);
        }

        Copiar(AreaOperativa.Clinica, CampoCategorias, categoriasClinica);
        Copiar(AreaOperativa.Oficina, CampoCategorias, categoriasOficina);
        Copiar(AreaOperativa.Limpieza, CampoCategorias, categoriasLimpieza);
        Copiar(AreaOperativa.Clinica, CampoProcedimientos,
        [
            "Trasplante FUE", "Trasplante FUT", "PRP", "Mesoterapia capilar",
            "Consulta / diagnóstico", "Postoperatorio", "Curación", "Uso general de clínica"
        ]);
        Copiar(AreaOperativa.Clinica, CampoRiesgos, ["Ninguno", "Bajo", "Medio", "Alto", "RPBI"]);
        Copiar(AreaOperativa.Clinica, CampoTemperaturas, ["15–25 °C", "15–30 °C", "2–8 °C", "Ambiente", "No congelar"]);
        Copiar(AreaOperativa.Limpieza, CampoPeligrosidad, ["No peligroso", "Irritante", "Corrosivo", "Inflamable", "Tóxico", "Biológico"]);
        return listas;
    }
}

using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class InventarioDocumento
{
    public const string Coleccion = "inventario";

    public static readonly string[] ColumnasPartida =
    [
        "Área", "Código", "Nombre", "Lote", "Caducidad", "Cantidad", "Estado"
    ];

    public static Dictionary<string, object> Crear(FichaTecnica ficha)
    {
        var origen = ficha.Clonar();
        origen.PrepararContactosProveedor();
        var contactos = Contactos(origen);
        object[] valores =
        [
            origen.AreaTitulo,
            origen.Codigo,
            origen.Nombre,
            origen.Categoria,
            origen.Descripcion,
            origen.EstadoFisico,
            origen.Color,
            origen.Presentacion,
            origen.Dimensiones,
            origen.PesoVolumen,
            origen.MaterialFabricacion,
            origen.UnidadMedida,
            origen.Marca,
            origen.Modelo,
            origen.Especificaciones,
            origen.NormasCertificaciones,
            origen.CondicionesAlmacenamiento,
            origen.InstruccionesUso,
            origen.VidaUtil,
            origen.Existencia,
            origen.StockMinimo,
            origen.Fabricante,
            origen.PaisOrigen,
            origen.PlantaOrigen,
            origen.TipoOrigen,
            origen.Proveedor,
            origen.ContactoProveedor,
            contactos.Canal1,
            contactos.Dato1,
            contactos.Canal2,
            contactos.Dato2,
            origen.Lote,
            Fecha(origen.FechaAdquisicion),
            origen.NumeroFactura,
            origen.RegistroSanitario,
            origen.UsoClinico,
            origen.Procedimiento,
            SiNo(origen.Esteril),
            SiNo(origen.RequiereRefrigeracion),
            origen.TemperaturaAlmacenamiento,
            Fecha(origen.FechaCaducidad),
            origen.Composicion,
            origen.RiesgoBiologico,
            origen.Contraindicaciones,
            SiNo(origen.Reutilizable),
            origen.Ubicacion,
            origen.NumeroSerie,
            origen.GarantiaMeses,
            SiNo(origen.RequiereMantenimiento),
            origen.PeriodicidadMantenimiento,
            origen.Voltaje,
            origen.Compatibilidad,
            origen.Responsable,
            origen.PrincipioActivo,
            origen.Concentracion,
            origen.Ph,
            origen.SuperficiesUso,
            origen.TiempoContacto,
            origen.Dilucion,
            origen.Peligrosidad,
            origen.HojaSeguridad,
            origen.EppRequerido,
            origen.MaterialesCompatibles,
            origen.ResiduoGenerado,
            origen.Observaciones,
            AlmacenCaracteristicas.SerializarLinea(origen.Caracteristicas)
        ];

        var documento = new Dictionary<string, object>(ExcelFichaService.Encabezados.Length + 2);
        for (var i = 0; i < ExcelFichaService.Encabezados.Length; i++)
        {
            documento[ExcelFichaService.Encabezados[i]] = valores[i] ?? string.Empty;
        }

        documento["Partidas"] = origen.Partidas.Select(partida => (object)new Dictionary<string, object>
        {
            ["Id"] = partida.Id.ToString("D"),
            ["Área"] = origen.AreaTitulo,
            ["Código"] = origen.Codigo,
            ["Nombre"] = origen.Nombre,
            ["Lote"] = partida.Lote,
            ["Caducidad"] = Fecha(partida.FechaCaducidad),
            ["Cantidad"] = partida.Cantidad,
            ["Estado"] = partida.EstadoCaducidadTexto
        }).ToList();
        documento["Meta"] = new Dictionary<string, object>
        {
            ["Id área"] = origen.Area?.ToString("D") ?? string.Empty,
            ["Ruta imagen"] = origen.RutaImagen ?? string.Empty,
            ["Fecha creación"] = FechaHora(origen.FechaCreacion),
            ["Fecha actualización"] = FechaHora(origen.FechaActualizacion)
        };
        return documento;
    }

    public static FichaTecnica Leer(string idDocumento, IDictionary<string, object> datos)
    {
        var meta = Mapa(datos.TryGetValue("Meta", out var metaRaw) ? metaRaw : null);
        var areaTexto = Texto(datos, "Área");
        var areaId = Texto(meta, "Id área");
        var ficha = new FichaTecnica
        {
            Id = Guid.TryParse(idDocumento, out var id) ? id : Guid.NewGuid(),
            Area = Guid.TryParse(areaId, out var area) ? area : AreasOperativas.ResolverId(areaTexto),
            Codigo = Texto(datos, "Código"),
            Nombre = Texto(datos, "Nombre"),
            Categoria = Texto(datos, "Categoría"),
            Descripcion = Texto(datos, "Descripción"),
            EstadoFisico = Texto(datos, "Estado físico"),
            Color = Texto(datos, "Color"),
            Presentacion = Texto(datos, "Presentación"),
            Dimensiones = Texto(datos, "Dimensiones"),
            PesoVolumen = Texto(datos, "Peso / volumen"),
            MaterialFabricacion = Texto(datos, "Material"),
            UnidadMedida = Texto(datos, "Unidad"),
            Marca = Texto(datos, "Marca"),
            Modelo = Texto(datos, "Modelo"),
            Especificaciones = Texto(datos, "Especificaciones"),
            NormasCertificaciones = Texto(datos, "Normas"),
            CondicionesAlmacenamiento = Texto(datos, "Almacenamiento"),
            InstruccionesUso = Texto(datos, "Instrucciones"),
            VidaUtil = Texto(datos, "Vida útil"),
            Existencia = Numero(datos, "Existencia"),
            StockMinimo = Numero(datos, "Stock mínimo"),
            Fabricante = Texto(datos, "Fabricante"),
            PaisOrigen = Texto(datos, "País"),
            PlantaOrigen = Texto(datos, "Planta"),
            TipoOrigen = Texto(datos, "Tipo origen"),
            Proveedor = Texto(datos, "Proveedor"),
            ContactoProveedor = Texto(datos, "Contacto"),
            ContactoCanal1 = Canal(Texto(datos, "Canal contacto 1")),
            ContactoDato1 = Texto(datos, "Dato contacto 1"),
            ContactoCanal2 = Canal(Texto(datos, "Canal contacto 2")),
            ContactoDato2 = Texto(datos, "Dato contacto 2"),
            Lote = Texto(datos, "Lote"),
            FechaAdquisicion = ParseFecha(Texto(datos, "Fecha adquisición")),
            NumeroFactura = Texto(datos, "Factura"),
            RegistroSanitario = Texto(datos, "Registro sanitario"),
            UsoClinico = Texto(datos, "Uso clínico"),
            Procedimiento = Texto(datos, "Procedimiento"),
            Esteril = EsSi(Texto(datos, "Estéril")),
            RequiereRefrigeracion = EsSi(Texto(datos, "Refrigeración")),
            TemperaturaAlmacenamiento = Texto(datos, "Temperatura"),
            FechaCaducidad = ParseFecha(Texto(datos, "Fecha caducidad")),
            Composicion = Texto(datos, "Composición"),
            RiesgoBiologico = Texto(datos, "Riesgo biológico"),
            Contraindicaciones = Texto(datos, "Contraindicaciones"),
            Reutilizable = EsSi(Texto(datos, "Reutilizable")),
            Ubicacion = Texto(datos, "Ubicación"),
            NumeroSerie = Texto(datos, "Número de serie"),
            GarantiaMeses = Numero(datos, "Garantía meses"),
            RequiereMantenimiento = EsSi(Texto(datos, "Mantenimiento")),
            PeriodicidadMantenimiento = Texto(datos, "Periodicidad"),
            Voltaje = Texto(datos, "Voltaje"),
            Compatibilidad = Texto(datos, "Compatibilidad"),
            Responsable = Texto(datos, "Responsable"),
            PrincipioActivo = Texto(datos, "Principio activo"),
            Concentracion = Texto(datos, "Concentración"),
            Ph = Texto(datos, "pH"),
            SuperficiesUso = Texto(datos, "Superficies"),
            TiempoContacto = Texto(datos, "Tiempo contacto"),
            Dilucion = Texto(datos, "Dilución"),
            Peligrosidad = Texto(datos, "Peligrosidad"),
            HojaSeguridad = Texto(datos, "SDS"),
            EppRequerido = Texto(datos, "EPP"),
            MaterialesCompatibles = Texto(datos, "Materiales compatibles"),
            ResiduoGenerado = Texto(datos, "Residuo"),
            Observaciones = Texto(datos, "Observaciones"),
            Caracteristicas = AlmacenCaracteristicas.ParsearLinea(Texto(datos, "Características extra")),
            RutaImagen = Texto(meta, "Ruta imagen"),
            Partidas = LeerPartidas(datos)
        };

        var creacion = ParseFecha(Texto(meta, "Fecha creación"));
        var actualizacion = ParseFecha(Texto(meta, "Fecha actualización"));
        if (creacion.HasValue)
        {
            ficha.FechaCreacion = creacion.Value;
        }

        if (actualizacion.HasValue)
        {
            ficha.FechaActualizacion = actualizacion.Value;
        }

        if (!string.IsNullOrWhiteSpace(ficha.ContactoDato1) || !string.IsNullOrWhiteSpace(ficha.ContactoDato2))
        {
            if (string.IsNullOrWhiteSpace(ficha.ContactoCanal1))
            {
                ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
            }

            if (string.IsNullOrWhiteSpace(ficha.ContactoCanal2))
            {
                ficha.ContactoCanal2 = CanalDatoProveedor.Web;
            }

            ficha.ContactosProveedorDefinidos = true;
            ficha.SincronizarCamposProveedorLegados();
        }

        PartidasInventario.Normalizar(ficha);
        return ficha;
    }

    public static string Huella(FichaTecnica ficha)
    {
        var documento = Crear(ficha);
        var partes = new List<string>(documento.Count);
        foreach (var clave in documento.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (clave is "Partidas" or "Meta")
            {
                continue;
            }

            partes.Add(clave + "=" + ValorTexto(documento[clave]));
        }

        if (documento["Partidas"] is IEnumerable<object> partidas)
        {
            foreach (var partida in partidas.OfType<IDictionary<string, object>>())
            {
                partes.Add(string.Join("|", ColumnasPartida.Select(c => Texto(partida, c))) + "|" + Texto(partida, "Id"));
            }
        }

        var meta = Mapa(documento["Meta"]);
        partes.Add(Texto(meta, "Id área"));
        partes.Add(Texto(meta, "Ruta imagen"));
        return string.Join("\n", partes);
    }

    private static List<PartidaInventario> LeerPartidas(IDictionary<string, object> datos)
    {
        if (!datos.TryGetValue("Partidas", out var raw) || raw is not IEnumerable<object> lista)
        {
            return [];
        }

        var partidas = new List<PartidaInventario>();
        foreach (var item in lista)
        {
            var mapa = Mapa(item);
            if (mapa.Count == 0)
            {
                continue;
            }

            partidas.Add(new PartidaInventario
            {
                Id = Guid.TryParse(Texto(mapa, "Id"), out var id) ? id : Guid.NewGuid(),
                Lote = Texto(mapa, "Lote"),
                FechaCaducidad = ParseFecha(Texto(mapa, "Caducidad")),
                Cantidad = Numero(mapa, "Cantidad")
            });
        }

        return partidas;
    }

    private static (string Canal1, string Dato1, string Canal2, string Dato2) Contactos(FichaTecnica ficha) =>
    (
        string.IsNullOrWhiteSpace(ficha.ContactoDato1) ? string.Empty : CanalDatoProveedor.De(ficha.ContactoCanal1).Titulo,
        ficha.ContactoDato1,
        string.IsNullOrWhiteSpace(ficha.ContactoDato2) ? string.Empty : CanalDatoProveedor.De(ficha.ContactoCanal2).Titulo,
        ficha.ContactoDato2
    );

    private static string Canal(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var opcion = CanalDatoProveedor.Opciones.FirstOrDefault(o =>
            string.Equals(o.Id, texto, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Titulo, texto, StringComparison.CurrentCultureIgnoreCase));
        return opcion?.Id ?? CanalDatoProveedor.Web;
    }

    private static Dictionary<string, object> Mapa(object? valor) =>
        valor as Dictionary<string, object>
        ?? (valor as IDictionary<string, object>)?.ToDictionary(p => p.Key, p => p.Value)
        ?? [];

    private static string Texto(IDictionary<string, object> datos, string clave) =>
        datos.TryGetValue(clave, out var valor) ? ValorTexto(valor) : string.Empty;

    private static string ValorTexto(object? valor) => valor switch
    {
        null => string.Empty,
        string texto => texto.Trim(),
        double numero => numero.ToString("0.##", CultureInfo.InvariantCulture),
        float numero => numero.ToString("0.##", CultureInfo.InvariantCulture),
        long numero => numero.ToString(CultureInfo.InvariantCulture),
        int numero => numero.ToString(CultureInfo.InvariantCulture),
        bool bandera => bandera ? "Sí" : "No",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static double Numero(IDictionary<string, object> datos, string clave)
    {
        if (!datos.TryGetValue(clave, out var valor) || valor is null)
        {
            return 0;
        }

        return valor switch
        {
            double numero => numero,
            float numero => numero,
            long numero => numero,
            int numero => numero,
            string texto when double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariante) => invariante,
            string texto when double.TryParse(texto, NumberStyles.Any, CultureInfo.CurrentCulture, out var local) => local,
            _ => 0
        };
    }

    private static string Fecha(DateTimeOffset? fecha) =>
        fecha?.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string FechaHora(DateTimeOffset fecha) =>
        fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseFecha(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var formatos = new[] { "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exacta)
            || DateTime.TryParse(texto, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out exacta))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(exacta, DateTimeKind.Local));
        }

        return null;
    }

    private static string SiNo(bool valor) => valor ? "Sí" : "No";

    private static bool EsSi(string texto)
    {
        var normal = texto.Trim().ToLowerInvariant();
        return normal is "si" or "sí" or "true" or "1" or "x" or "yes";
    }
}

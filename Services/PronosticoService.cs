using System.Globalization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public readonly record struct RangoPronostico(DateTimeOffset Desde, DateTimeOffset Hasta, string Titulo)
{
    public bool Contiene(DateTimeOffset fecha)
    {
        DateTimeOffset local;
        try
        {
            local = fecha.ToLocalTime();
        }
        catch (ArgumentException)
        {
            return false;
        }

        return local >= Desde && local < Hasta;
    }

    public string TextoFechas => Describir(Desde, Hasta);

    public RangoPronostico PeriodoAnterior()
    {
        var duracion = Hasta - Desde;
        if (duracion <= TimeSpan.Zero)
        {
            duracion = TimeSpan.FromDays(1);
        }

        return new RangoPronostico(Desde - duracion, Desde, Describir(Desde - duracion, Desde));
    }

    public string ResumenComparacion(RangoPronostico anterior)
    {
        var fechas = TextoFechas;
        var actual = Titulo == fechas ? Titulo : $"{Titulo} ({fechas})";
        return $"{actual} · comparado con {anterior.TextoFechas}";
    }

    public static RangoPronostico De(string periodo, DateTimeOffset? desdeCustom, DateTimeOffset? hastaCustom)
    {
        var hoy = DateTime.Today;
        return periodo switch
        {
            "Esta semana" => Semana(hoy),
            "Últimos 7 días" => new RangoPronostico(hoy.AddDays(-6), hoy.AddDays(1), "Últimos 7 días"),
            "Últimos 30 días" => new RangoPronostico(hoy.AddDays(-29), hoy.AddDays(1), "Últimos 30 días"),
            "Este mes" => new RangoPronostico(
                new DateTime(hoy.Year, hoy.Month, 1),
                new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1),
                "Este mes"),
            "Últimos 3 meses" => Meses(hoy, 3),
            "Últimos 6 meses" => Meses(hoy, 6),
            "Este año" => new RangoPronostico(new DateTime(hoy.Year, 1, 1), new DateTime(hoy.Year + 1, 1, 1), "Este año"),
            "Personalizado" => Personalizado(desdeCustom, hastaCustom),
            _ => new RangoPronostico(hoy, hoy.AddDays(1), "Hoy")
        };
    }

    private static RangoPronostico Semana(DateTime hoy)
    {
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        return new RangoPronostico(lunes, lunes.AddDays(7), "Esta semana");
    }

    private static RangoPronostico Meses(DateTime hoy, int cantidad)
    {
        var inicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-(Math.Max(1, cantidad) - 1));
        var fin = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1);
        return new RangoPronostico(inicio, fin, $"Últimos {cantidad} meses");
    }

    private static RangoPronostico Personalizado(DateTimeOffset? desdeCustom, DateTimeOffset? hastaCustom)
    {
        var desde = (desdeCustom ?? DateTimeOffset.Now).LocalDateTime.Date;
        var hasta = (hastaCustom ?? desdeCustom ?? DateTimeOffset.Now).LocalDateTime.Date;
        if (hasta < desde)
        {
            (desde, hasta) = (hasta, desde);
        }

        return new RangoPronostico(desde, hasta.AddDays(1), Describir(desde, hasta.AddDays(1)));
    }

    private static string Describir(DateTimeOffset desde, DateTimeOffset hastaExclusivo)
    {
        var ultimo = hastaExclusivo.AddTicks(-1);
        return desde.Date == ultimo.Date
            ? desde.ToString("dd/MM/yyyy")
            : $"{desde:dd/MM/yyyy} al {ultimo:dd/MM/yyyy}";
    }
}

public enum OrdenPronostico
{
    MasMermas,
    MenosMermas,
    MasIngresos,
    MasSalidas
}

public sealed class FilaPronostico
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = string.Empty;
    public double Ingresos { get; init; }
    public double Salidas { get; init; }
    public double Merma { get; init; }
    public double DemandaAnterior { get; init; }
    public double IngresosAnteriores { get; init; }
    public double MermaAnterior { get; init; }
    public double? Variacion { get; init; }
    public string Tendencia { get; init; } = "Estable";
    public double Pronostico { get; init; }
}

public sealed class ResultadoPronostico
{
    public required RangoPronostico Rango { get; init; }
    public required RangoPronostico Anterior { get; init; }
    public required IReadOnlyList<FilaPronostico> Filas { get; init; }
    public bool HayMovimientosEnRango { get; init; }
    public string Area { get; init; } = "Todas las áreas";

    public double TotalIngresos => Filas.Sum(f => f.Ingresos);
    public double TotalSalidas => Filas.Sum(f => f.Salidas);
    public double TotalMerma => Filas.Sum(f => f.Merma);
    public double TotalIngresosAnteriores => Filas.Sum(f => f.IngresosAnteriores);
    public double TotalSalidasAnteriores => Filas.Sum(f => f.DemandaAnterior);
    public double TotalMermaAnterior => Filas.Sum(f => f.MermaAnterior);
    public int Subieron => Filas.Count(f => f.Tendencia == "Subió");
    public int Bajaron => Filas.Count(f => f.Tendencia == "Bajó");
}

public static class PronosticoService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private const double Epsilon = 0.0001;
    private const double UmbralEstable = 5;

    public static string FormatoCantidad(double valor) => valor.ToString("#,##0.##", Cultura);

    public static string FormatoVariacion(double? variacion) =>
        variacion is double valor
            ? $"{(valor > 0 ? "+" : "")}{valor.ToString("0.#", Cultura)} %"
            : "Nuevo";

    public static OrdenPronostico OrdenDe(string? nombre) => nombre switch
    {
        "Menos mermas" => OrdenPronostico.MenosMermas,
        "Más ingresos" => OrdenPronostico.MasIngresos,
        "Más salidas" => OrdenPronostico.MasSalidas,
        _ => OrdenPronostico.MasMermas
    };

    public static ResultadoPronostico Analizar(
        IEnumerable<MovimientoInventario> movimientos,
        IEnumerable<FichaTecnica> fichas,
        RangoPronostico rango,
        OrdenPronostico orden,
        string? busqueda,
        Guid? areaId = null,
        bool soloSinArea = false,
        string area = "Todas las áreas")
    {
        var anterior = rango.PeriodoAnterior();
        var porFicha = fichas.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
        var acumulados = new Dictionary<Guid, Acumulado>();

        foreach (var movimiento in movimientos)
        {
            if (movimiento.Cantidad <= Epsilon || !EnArea(porFicha, movimiento.FichaId, areaId, soloSinArea))
            {
                continue;
            }

            var enRango = rango.Contiene(movimiento.Fecha);
            var enAnterior = anterior.Contiene(movimiento.Fecha);
            if (!enRango && !enAnterior)
            {
                continue;
            }

            if (!acumulados.TryGetValue(movimiento.FichaId, out var acumulado))
            {
                acumulado = new Acumulado();
                acumulados[movimiento.FichaId] = acumulado;
            }

            Sumar(acumulado, movimiento, enRango, enAnterior);
        }

        var hayMovimientos = acumulados.Values.Any(a =>
            a.Ingresos > Epsilon || a.Salidas > Epsilon || a.Merma > Epsilon);

        var filas = acumulados
            .Where(par => par.Value.TieneActividad)
            .Select(par => CrearFila(par.Key, par.Value, porFicha, rango))
            .Where(fila => Coincide(fila, busqueda))
            .ToList();

        Ordenar(filas, orden);

        return new ResultadoPronostico
        {
            Rango = rango,
            Anterior = anterior,
            Filas = filas,
            HayMovimientosEnRango = hayMovimientos,
            Area = string.IsNullOrWhiteSpace(area) ? "Todas las áreas" : area
        };
    }

    private static bool EnArea(
        IReadOnlyDictionary<Guid, FichaTecnica> porFicha,
        Guid fichaId,
        Guid? areaId,
        bool soloSinArea)
    {
        if (areaId is null && !soloSinArea)
        {
            return true;
        }

        if (!porFicha.TryGetValue(fichaId, out var ficha))
        {
            return false;
        }

        return soloSinArea ? ficha.Area is null : ficha.Area == areaId;
    }

    private static void Sumar(Acumulado acumulado, MovimientoInventario movimiento, bool enRango, bool enAnterior)
    {
        if (movimiento.EsMerma)
        {
            if (enRango)
            {
                acumulado.Merma += movimiento.Cantidad;
            }

            if (enAnterior)
            {
                acumulado.MermaAnterior += movimiento.Cantidad;
            }

            return;
        }

        if (movimiento.EsSalida)
        {
            if (enRango)
            {
                acumulado.Salidas += movimiento.Cantidad;
            }

            if (enAnterior)
            {
                acumulado.DemandaAnterior += movimiento.Cantidad;
            }

            return;
        }

        if (EsStockInicial(movimiento))
        {
            return;
        }

        if (enRango)
        {
            acumulado.Ingresos += movimiento.Cantidad;
        }

        if (enAnterior)
        {
            acumulado.IngresosAnteriores += movimiento.Cantidad;
        }
    }

    private static bool EsStockInicial(MovimientoInventario movimiento) =>
        string.Equals(movimiento.Origen, "Stock inicial", StringComparison.OrdinalIgnoreCase);

    private static FilaPronostico CrearFila(
        Guid fichaId,
        Acumulado acumulado,
        IReadOnlyDictionary<Guid, FichaTecnica> porFicha,
        RangoPronostico rango)
    {
        porFicha.TryGetValue(fichaId, out var ficha);
        var (tendencia, variacion) = Evaluar(acumulado.Salidas, acumulado.DemandaAnterior);
        var nombre = ficha?.Nombre?.Trim();
        return new FilaPronostico
        {
            Codigo = ficha?.Codigo ?? string.Empty,
            Nombre = string.IsNullOrWhiteSpace(nombre) ? "Producto sin ficha" : nombre,
            Unidad = ficha?.UnidadMedida ?? string.Empty,
            Ingresos = acumulado.Ingresos,
            Salidas = acumulado.Salidas,
            Merma = acumulado.Merma,
            DemandaAnterior = acumulado.DemandaAnterior,
            IngresosAnteriores = acumulado.IngresosAnteriores,
            MermaAnterior = acumulado.MermaAnterior,
            Variacion = variacion,
            Tendencia = tendencia,
            Pronostico = Pronosticar(acumulado.Salidas, rango)
        };
    }

    private static (string Tendencia, double? Variacion) Evaluar(double actual, double anterior)
    {
        if (actual <= Epsilon && anterior <= Epsilon)
        {
            return ("Estable", 0);
        }

        if (anterior <= Epsilon)
        {
            return ("Subió", null);
        }

        var variacion = (actual - anterior) / anterior * 100;
        if (Math.Abs(variacion) < UmbralEstable)
        {
            return ("Estable", variacion);
        }

        return (variacion > 0 ? "Subió" : "Bajó", variacion);
    }

    private static double Pronosticar(double salidas, RangoPronostico rango)
    {
        if (salidas <= Epsilon)
        {
            return 0;
        }

        return salidas / DiasTranscurridos(rango) * DiasDelRango(rango);
    }

    private static double DiasDelRango(RangoPronostico rango)
    {
        var dias = (rango.Hasta - rango.Desde).TotalDays;
        return dias < 1 ? 1 : dias;
    }

    private static double DiasTranscurridos(RangoPronostico rango)
    {
        var fin = DateTimeOffset.Now < rango.Hasta ? DateTimeOffset.Now : rango.Hasta;
        if (fin <= rango.Desde)
        {
            return 1;
        }

        var dias = (fin - rango.Desde).TotalDays;
        return dias < 1d / 24d ? 1d / 24d : dias;
    }

    private static bool Coincide(FilaPronostico fila, string? busqueda)
    {
        if (string.IsNullOrWhiteSpace(busqueda))
        {
            return true;
        }

        var texto = busqueda.Trim();
        return fila.Codigo.Contains(texto, StringComparison.CurrentCultureIgnoreCase)
            || fila.Nombre.Contains(texto, StringComparison.CurrentCultureIgnoreCase);
    }

    private static void Ordenar(List<FilaPronostico> filas, OrdenPronostico orden)
    {
        filas.Sort((a, b) =>
        {
            var porCantidad = orden switch
            {
                OrdenPronostico.MenosMermas => a.Merma.CompareTo(b.Merma),
                OrdenPronostico.MasIngresos => b.Ingresos.CompareTo(a.Ingresos),
                OrdenPronostico.MasSalidas => b.Salidas.CompareTo(a.Salidas),
                _ => b.Merma.CompareTo(a.Merma)
            };

            return porCantidad != 0
                ? porCantidad
                : string.Compare(a.Nombre, b.Nombre, StringComparison.CurrentCultureIgnoreCase);
        });
    }

    private sealed class Acumulado
    {
        public double Ingresos { get; set; }
        public double Salidas { get; set; }
        public double Merma { get; set; }
        public double DemandaAnterior { get; set; }
        public double IngresosAnteriores { get; set; }
        public double MermaAnterior { get; set; }

        public bool TieneActividad =>
            Ingresos > Epsilon || Salidas > Epsilon || Merma > Epsilon
            || DemandaAnterior > Epsilon || IngresosAnteriores > Epsilon || MermaAnterior > Epsilon;
    }
}

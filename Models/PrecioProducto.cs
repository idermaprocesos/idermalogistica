namespace IdermaFichas.Models;

public static class MonedaPrecio
{
    public const string Soles = "PEN";
    public const string Dolares = "USD";

    public static string Etiqueta(string? moneda) =>
        EsDolar(moneda) ? "Dólares (US$)" : "Soles (S/)";

    public static string Simbolo(string? moneda) =>
        EsDolar(moneda) ? "US$" : "S/";

    public static bool EsDolar(string? moneda) =>
        string.Equals(moneda, Dolares, StringComparison.OrdinalIgnoreCase);

    public static string Normalizar(string? moneda) =>
        EsDolar(moneda) ? Dolares : Soles;

    public static string Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return Soles;
        }

        var normal = texto.Trim().ToLowerInvariant()
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u");
        if (normal.Contains("dolar")
            || normal.Contains("usd")
            || normal.Contains("us$")
            || normal == "$")
        {
            return Dolares;
        }

        return Soles;
    }

    public static string Formato(double monto, string? moneda)
    {
        var cultura = EsDolar(moneda)
            ? System.Globalization.CultureInfo.GetCultureInfo("en-US")
            : System.Globalization.CultureInfo.GetCultureInfo("es-PE");
        return $"{Simbolo(moneda)} {monto.ToString("N2", cultura)}";
    }
}

public sealed class PrecioProducto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FichaId { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public double Monto { get; set; }
    public string Moneda { get; set; } = MonedaPrecio.Soles;
    public string Nota { get; set; } = string.Empty;

    public PrecioProducto Clonar() => new()
    {
        Id = Id,
        FichaId = FichaId,
        Fecha = Fecha,
        Monto = Monto,
        Moneda = Moneda,
        Nota = Nota
    };
}

public sealed class PrecioProductoItem
{
    public required PrecioProducto Precio { get; init; }
    public Guid Id => Precio.Id;
    public string FechaTexto => Precio.Fecha.ToString("dd/MM/yyyy");
    public string MontoTexto => MonedaPrecio.Formato(Precio.Monto, Precio.Moneda);
    public string VariacionTexto { get; init; } = string.Empty;
    public string Nota => Precio.Nota;
}

public sealed class FichaPrecioListaItem
{
    public required FichaTecnica Ficha { get; init; }
    public string UltimoPrecioTexto { get; init; } = "Sin precio";
}

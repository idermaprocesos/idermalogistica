using System.Globalization;
using IdermaFichas.Helpers;

namespace IdermaFichas.Models;

public static class CalificacionProveedor
{
    public const string SinCalificar = "Sin calificar";
    public const string Puntual = "Puntual";
    public const string Excelente = "Excelente";
    public const string Regular = "Regular";
    public const string Retraso = "Retraso";
    public const string Problemas = "Problemas";
    public const string Incumplimiento = "Incumplimiento";

    public static readonly string[] Todas =
    [
        SinCalificar,
        Puntual,
        Excelente,
        Regular,
        Retraso,
        Problemas,
        Incumplimiento
    ];

    public static string Normalizar(string? valor) =>
        Todas.FirstOrDefault(item => string.Equals(item, valor?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? SinCalificar;
}

public static class BancoProveedor
{
    public const string Ninguno = "";
    public const string Scotiabank = "Scotiabank";
    public const string Bcp = "BCP";
    public const string Interbank = "Interbank";
    public const string Bbva = "BBVA";
    public const string Otras = "Otras entidades";

    public static readonly string[] Todas =
    [
        Scotiabank,
        Bcp,
        Interbank,
        Bbva,
        Otras
    ];

    public static string Normalizar(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return Ninguno;
        }

        return Todas.FirstOrDefault(item => string.Equals(item, limpio, StringComparison.OrdinalIgnoreCase))
            ?? Otras;
    }

    public static bool EsOtras(string? valor) =>
        string.Equals(Normalizar(valor), Otras, StringComparison.OrdinalIgnoreCase);
}

public sealed class ProveedorRegistro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public string Canal1 { get; set; } = CanalDatoProveedor.WhatsApp;
    public string Dato1 { get; set; } = string.Empty;
    public string Canal2 { get; set; } = CanalDatoProveedor.Web;
    public string Dato2 { get; set; } = string.Empty;
    public string Notas { get; set; } = string.Empty;
    public string Calificacion { get; set; } = CalificacionProveedor.SinCalificar;
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string Banco { get; set; } = string.Empty;
    public string BancoOtro { get; set; } = string.Empty;
    public string NumeroCuenta { get; set; } = string.Empty;
    public string Cci { get; set; } = string.Empty;
    public int TiempoEntregaDias { get; set; }
    public bool AcuerdoContrato { get; set; }
    public decimal CantidadContrato { get; set; }
    public string UnidadContrato { get; set; } = string.Empty;
    public DateTimeOffset FechaAgregado { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset FechaContacto { get; set; }

    public bool TieneDatosContacto =>
        Dato1.Trim().Length > 0 || Dato2.Trim().Length > 0;

    public string TextoEmpresa
    {
        get
        {
            var partes = new List<string>();
            if (Ruc.Length > 0)
            {
                partes.Add($"RUC {Ruc}");
            }

            if (RazonSocial.Length > 0)
            {
                partes.Add(RazonSocial);
            }

            return partes.Count == 0 ? "Sin RUC ni razón social" : string.Join(" · ", partes);
        }
    }

    public string TextoGestionCompra
    {
        get
        {
            var entrega = TiempoEntregaDias switch
            {
                <= 0 => "Sin tiempo de entrega",
                1 => "Entrega en 1 día",
                _ => $"Entrega en {TiempoEntregaDias} días"
            };
            if (!AcuerdoContrato || CantidadContrato <= 0)
            {
                return $"{entrega} · Sin acuerdo de compra mínima";
            }

            var cantidad = CantidadContrato.ToString("0.##", CultureInfo.GetCultureInfo("es-PE"));
            var unidad = UnidadContrato.Length == 0 ? "unidades" : UnidadContrato;
            return $"{entrega} · Contrato: comprar {cantidad} {unidad}";
        }
    }

    public string TextoBanco
    {
        get
        {
            var banco = BancoProveedor.Normalizar(Banco);
            if (banco.Length == 0)
            {
                return "Sin datos bancarios";
            }

            var nombre = BancoProveedor.EsOtras(banco)
                ? (BancoOtro.Trim().Length > 0 ? BancoOtro.Trim() : BancoProveedor.Otras)
                : banco;
            var partes = new List<string> { nombre };
            if (NumeroCuenta.Trim().Length > 0)
            {
                partes.Add($"Cuenta {NumeroCuenta.Trim()}");
            }

            if (Cci.Trim().Length > 0)
            {
                partes.Add($"CCI {Cci.Trim()}");
            }

            return string.Join(" · ", partes);
        }
    }

    public static string NormalizarRuc(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    public static string NormalizarCci(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());
}

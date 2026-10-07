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
    public DateTimeOffset FechaAgregado { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset FechaContacto { get; set; }

    public bool TieneDatosContacto =>
        Dato1.Trim().Length > 0 || Dato2.Trim().Length > 0;
}

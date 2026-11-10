namespace IdermaFichas.Models;

public sealed class CategoriaMenuDefinicion
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Glifo { get; set; } = "\uE8F1";
    public int Orden { get; set; }
}

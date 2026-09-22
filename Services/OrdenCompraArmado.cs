using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class OrdenCompraArmado
{
    public static List<LineaOrdenCompra> DesdeSeleccion(
        IReadOnlyList<FichaTecnica> fichas,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde,
        int? atajoMeses)
    {
        var registro = App.Instance.Registro;
        var precios = App.Instance.HistorialPrecios;
        var lineas = new List<LineaOrdenCompra>(fichas.Count);
        foreach (var ficha in fichas)
        {
            var ultimo = precios.Ultimo(ficha.Id);
            var consumo = CalculoOrdenCompra.ConsumoEnPeriodo(
                registro.PorFicha(ficha.Id), anio, meses, desde, atajoMeses);
            var stock = ficha.Existencia;
            var pedir = Math.Max(0, CalculoOrdenCompra.Redondear(consumo - stock));
            lineas.Add(new LineaOrdenCompra
            {
                FichaId = ficha.Id,
                Codigo = ficha.Codigo,
                Nombre = ficha.Nombre,
                Marca = ficha.Marca,
                Proveedor = ficha.Proveedor,
                Unidad = ficha.UnidadMedida,
                ConsumoPromedio = consumo,
                StockActual = stock,
                PrecioConIgv = ultimo?.Monto ?? 0,
                TienePrecio = ultimo is not null && ultimo.Monto > 0,
                Moneda = MonedaPrecio.Normalizar(ultimo?.Moneda),
                CantidadPedir = pedir
            });
        }

        return lineas;
    }
}

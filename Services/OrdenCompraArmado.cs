using System.Globalization;
using System.Text;
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
                Ruc = RucDe(ficha.Proveedor),
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

    public static LineaOrdenCompra DesdeFicha(FichaTecnica ficha, double cantidad)
    {
        var linea = DesdeSeleccion([ficha], null, new HashSet<int>(), null, null)[0];
        if (cantidad > 0)
        {
            linea.CantidadPedir = cantidad;
        }

        return linea;
    }

    public static LineaOrdenCompra Personalizada(
        string codigo,
        string nombre,
        string unidad,
        double cantidad,
        double precioConIgv)
    {
        return new LineaOrdenCompra
        {
            Codigo = string.IsNullOrWhiteSpace(codigo) ? "INC" : codigo.Trim(),
            Nombre = string.IsNullOrWhiteSpace(nombre) ? "Incidencia" : nombre.Trim(),
            Unidad = string.IsNullOrWhiteSpace(unidad) ? "und" : unidad.Trim(),
            CantidadPedir = cantidad <= 0 ? 1 : cantidad,
            PrecioConIgv = precioConIgv > 0 ? precioConIgv : 0,
            TienePrecio = precioConIgv > 0,
            Moneda = MonedaPrecio.Soles,
            TipoPrecio = "Estimado"
        };
    }

    public static LineaOrdenCompra DesdeGuardada(LineaOrdenExportacion linea)
    {
        var cantidad = linea.CantidadPedir;
        var precio = !linea.TienePrecio
            ? 0
            : cantidad > 0.0001
                ? CalculoOrdenCompra.Redondear(linea.TotalConIgv / cantidad)
                : linea.TotalConIgv;
        var fichaId = linea.FichaId;
        if (fichaId == Guid.Empty && !string.IsNullOrWhiteSpace(linea.Codigo))
        {
            var ficha = App.Instance.Repositorio.Todas.FirstOrDefault(f =>
                f.Codigo.Equals(linea.Codigo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ficha is not null)
            {
                fichaId = ficha.Id;
            }
        }

        return new LineaOrdenCompra
        {
            FichaId = fichaId,
            Codigo = linea.Codigo,
            Nombre = linea.Nombre,
            Marca = linea.Marca == "—" ? string.Empty : linea.Marca,
            Proveedor = linea.Proveedor == "—" ? string.Empty : linea.Proveedor,
            Ruc = string.IsNullOrWhiteSpace(linea.Ruc) || linea.Ruc == "—"
                ? RucDe(linea.Proveedor)
                : linea.Ruc,
            Unidad = linea.Unidad,
            ConsumoPromedio = linea.ConsumoPromedio,
            StockActual = linea.StockActual,
            PrecioConIgv = precio,
            TienePrecio = linea.TienePrecio,
            Moneda = MonedaPrecio.Normalizar(linea.Moneda),
            CantidadPedir = cantidad,
            TipoPrecio = string.IsNullOrWhiteSpace(linea.TipoPrecio) ? "Estimado" : linea.TipoPrecio
        };
    }

    public static LineaOrdenCompra ParaIncidencia(SolicitudIncidencia solicitud)
    {
        if (solicitud.PrecioReposicion <= 0 && solicitud.Tipo is TipoIncidencia.Insumo or TipoIncidencia.Recurso)
        {
            var ficha = BuscarInventario(solicitud.Nombre);
            if (ficha is not null)
            {
                return DesdeFicha(ficha, solicitud.Cantidad);
            }
        }

        var codigo = solicitud.Tipo == TipoIncidencia.EquipoElectronico ? "EQ" : "INC";
        if (solicitud.PrecioReposicion > 0)
        {
            var ficha = BuscarInventario(solicitud.Nombre);
            if (ficha is not null)
            {
                return new LineaOrdenCompra
                {
                    FichaId = ficha.Id,
                    Codigo = ficha.Codigo,
                    Nombre = ficha.Nombre,
                    Marca = ficha.Marca,
                    Proveedor = ficha.Proveedor,
                    Ruc = RucDe(ficha.Proveedor),
                    Unidad = string.IsNullOrWhiteSpace(solicitud.Unidad) ? ficha.UnidadMedida : solicitud.Unidad,
                    StockActual = ficha.Existencia,
                    PrecioConIgv = solicitud.PrecioReposicion,
                    TienePrecio = true,
                    Moneda = MonedaPrecio.Soles,
                    CantidadPedir = solicitud.Cantidad <= 0 ? 1 : solicitud.Cantidad,
                    TipoPrecio = "Estimado"
                };
            }
        }

        return Personalizada(codigo, solicitud.Nombre, solicitud.Unidad, solicitud.Cantidad, solicitud.PrecioReposicion);
    }

    public static void Incorporar(ICollection<LineaOrdenCompra> lineas, LineaOrdenCompra nueva)
    {
        var existente = lineas.FirstOrDefault(l =>
            l.FichaId != Guid.Empty && l.FichaId == nueva.FichaId
            || (l.FichaId == Guid.Empty
                && l.Codigo.Equals(nueva.Codigo, StringComparison.OrdinalIgnoreCase)
                && l.Nombre.Equals(nueva.Nombre, StringComparison.OrdinalIgnoreCase)));
        if (existente is null)
        {
            lineas.Add(nueva);
            return;
        }

        existente.CantidadPedir += nueva.CantidadPedir;
    }

    public static string RucDe(string? proveedor)
    {
        var registro = BuscarProveedor(proveedor);
        return string.IsNullOrWhiteSpace(registro?.Ruc) ? string.Empty : registro.Ruc.Trim();
    }

    public static ProveedorRegistro? BuscarProveedor(string? proveedor)
    {
        var nombre = Normalizar(proveedor);
        if (nombre.Length == 0)
        {
            return null;
        }

        var lista = App.Instance.Proveedores.Listar();
        return lista.FirstOrDefault(p =>
                   Normalizar(p.Nombre) == nombre || Normalizar(p.RazonSocial) == nombre)
               ?? lista.FirstOrDefault(p => Coincide(nombre, p.Nombre) || Coincide(nombre, p.RazonSocial));
    }

    public static bool CumpleAcuerdoContrato(IReadOnlyList<LineaOrdenCompra> lineas, out string mensaje)
    {
        mensaje = string.Empty;
        if (lineas.Count == 0)
        {
            return true;
        }

        var nombre = lineas
            .Select(l => l.Proveedor?.Trim())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && p != "—");
        var proveedor = BuscarProveedor(nombre);
        if (proveedor is null || !proveedor.AcuerdoContrato || proveedor.CantidadContrato <= 0)
        {
            return true;
        }

        var minimo = (double)proveedor.CantidadContrato;
        var total = CalculoOrdenCompra.Redondear(lineas.Sum(l => l.CantidadPedir));
        if (total + 0.0001 >= minimo)
        {
            return true;
        }

        var cantidad = proveedor.CantidadContrato.ToString("0.##", CultureInfo.GetCultureInfo("es-PE"));
        var unidad = string.IsNullOrWhiteSpace(proveedor.UnidadContrato)
            ? "unidades"
            : proveedor.UnidadContrato.Trim();
        var pedido = total.ToString("0.##", CultureInfo.GetCultureInfo("es-PE"));
        mensaje =
            $"El proveedor «{proveedor.Nombre}» tiene por contrato comprar desde {cantidad} {unidad} del producto.\n\n" +
            $"En esta orden la cantidad a pedir es {pedido} {unidad}.";
        return false;
    }

    public static void AjustarACantidadContrato(IList<LineaOrdenCompra> lineas)
    {
        if (lineas.Count == 0)
        {
            return;
        }

        var nombre = lineas
            .Select(l => l.Proveedor?.Trim())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && p != "—");
        var proveedor = BuscarProveedor(nombre);
        if (proveedor is null || !proveedor.AcuerdoContrato || proveedor.CantidadContrato <= 0)
        {
            return;
        }

        var minimo = (double)proveedor.CantidadContrato;
        var total = CalculoOrdenCompra.Redondear(lineas.Sum(l => l.CantidadPedir));
        var falta = CalculoOrdenCompra.Redondear(minimo - total);
        if (falta <= 0)
        {
            return;
        }

        var destino = lineas.OrderByDescending(l => l.CantidadPedir).First();
        destino.CantidadPedir = CalculoOrdenCompra.Redondear(destino.CantidadPedir + falta);
    }

    private static bool Coincide(string nombre, string? candidato)
    {
        var otro = Normalizar(candidato);
        return otro.Length >= 6 && (nombre.Contains(otro, StringComparison.Ordinal) || otro.Contains(nombre, StringComparison.Ordinal));
    }

    private static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Trim() == "—")
        {
            return string.Empty;
        }

        var plano = valor.Trim().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(plano.Length);
        foreach (var caracter in plano)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                limpio.Append(char.ToUpperInvariant(caracter));
            }
        }

        return limpio.ToString();
    }

    private static FichaTecnica? BuscarInventario(string nombre)
    {
        var texto = nombre.Trim();
        if (texto.Length < 2)
        {
            return null;
        }

        var candidatos = App.Instance.Repositorio.BuscarLimitado(texto, 8);
        return candidatos.FirstOrDefault(f => f.Nombre.Equals(texto, StringComparison.OrdinalIgnoreCase))
            ?? candidatos.FirstOrDefault(f => f.Codigo.Equals(texto, StringComparison.OrdinalIgnoreCase));
    }
}

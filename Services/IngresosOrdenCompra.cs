using System.Globalization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class IngresosOrdenCompra
{
    public const string TipoDocumento = "Orden de compra";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static bool YaCargada(OrdenCompraRegistrada orden)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var numero = NumeroDocumento(orden);
        if (string.IsNullOrWhiteSpace(numero))
        {
            return false;
        }

        return App.Instance.Registro.CopiarTodos().Any(m =>
            !m.EsSalida
            && string.Equals(m.TipoDocumento, TipoDocumento, StringComparison.OrdinalIgnoreCase)
            && string.Equals(m.NumeroDocumento, numero, StringComparison.OrdinalIgnoreCase));
    }

    public static string Registrar(OrdenCompraRegistrada orden)
    {
        ArgumentNullException.ThrowIfNull(orden);
        if (YaCargada(orden))
        {
            throw new InvalidOperationException(
                $"La orden {NumeroDocumento(orden)} ya está cargada en inventario. " +
                "No se puede volver a ingresar para no duplicar stock. Genere solo el PDF si lo necesita.");
        }

        var documento = orden.Documento ?? new OrdenCompraDocumento();
        var todas = documento.Lineas ?? [];
        if (todas.Count == 0)
        {
            throw new InvalidOperationException("La orden de compra no tiene productos.");
        }

        var lineas = todas.Where(l => l.CantidadPedir > 0.0001).ToList();
        if (lineas.Count == 0)
        {
            throw new InvalidOperationException(
                "Todos los productos de la orden tienen cantidad a pedir en 0 (cubiertos). " +
                "Indique cantidades en la orden de compra antes de cargar el ingreso.");
        }

        var repo = App.Instance.Repositorio;
        var preparadas = new List<(FichaTecnica Ficha, double Cantidad, LineaOrdenExportacion Linea)>();
        var faltantes = new List<string>();

        foreach (var linea in lineas)
        {
            var original = BuscarFicha(repo.Todas, linea);
            if (original is null)
            {
                faltantes.Add(NombreLinea(linea) + " (no está en inventario)");
                continue;
            }

            var ficha = original.Clonar();
            PartidasInventario.Normalizar(ficha);
            preparadas.Add((ficha, linea.CantidadPedir, linea));
        }

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                "No se pudo cargar: faltan productos en el catálogo.\n" + string.Join("\n", faltantes));
        }

        if (preparadas.Count == 0)
        {
            throw new InvalidOperationException("No hay cantidades válidas para registrar el ingreso.");
        }

        var numero = NumeroDocumento(orden);
        var nota = string.IsNullOrWhiteSpace(documento.MesAnio)
            ? "Ingreso por orden de compra"
            : $"Ingreso por orden de compra · {documento.MesAnio.Trim()}";
        var ahora = DateTimeOffset.Now;
        var movimientos = new List<MovimientoInventario>();
        var fichasGuardar = new List<FichaTecnica>();

        foreach (var (ficha, cantidad, _) in preparadas)
        {
            var partida = PartidasInventario.AplicarIngreso(ficha, lote: null, fecha: null, cantidad);
            movimientos.Add(new MovimientoInventario
            {
                FichaId = ficha.Id,
                PartidaId = partida.Id,
                Lote = partida.Lote,
                FechaCaducidad = partida.FechaCaducidad,
                Area = ficha.Area,
                Fecha = ahora,
                Cantidad = cantidad,
                ExistenciaResultante = ficha.Existencia,
                Tipo = "Ingreso",
                Origen = "Ingreso",
                TipoDocumento = TipoDocumento,
                NumeroDocumento = numero,
                Nota = nota
            });
            fichasGuardar.Add(ficha);
        }

        repo.GuardarFichas(fichasGuardar, registrarIngresoAutomatico: false);
        App.Instance.Registro.Agregar(movimientos);

        var piezas = movimientos.Sum(m => m.Cantidad);
        return $"Se registraron {movimientos.Count} ingreso{(movimientos.Count == 1 ? "" : "s")} " +
               $"({piezas.ToString("0.##", Cultura)} pzas) en Registro por {numero}.";
    }

    public static string NumeroDocumento(OrdenCompraRegistrada orden) =>
        OrdenEntradaExportService.Folio(orden.Documento ?? new OrdenCompraDocumento(), orden);

    private static FichaTecnica? BuscarFicha(IReadOnlyList<FichaTecnica> fichas, LineaOrdenExportacion linea)
    {
        if (linea.FichaId != Guid.Empty)
        {
            var porId = fichas.FirstOrDefault(f => f.Id == linea.FichaId);
            if (porId is not null)
            {
                return porId;
            }
        }

        if (!string.IsNullOrWhiteSpace(linea.Codigo))
        {
            var porCodigo = fichas
                .Where(f => f.Codigo.Equals(linea.Codigo.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (porCodigo.Count == 1)
            {
                return porCodigo[0];
            }

            if (porCodigo.Count > 1)
            {
                return porCodigo.FirstOrDefault(f =>
                           !string.IsNullOrWhiteSpace(linea.Nombre) &&
                           f.Nombre.Equals(linea.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? porCodigo[0];
            }
        }

        if (string.IsNullOrWhiteSpace(linea.Nombre))
        {
            return null;
        }

        var porNombre = fichas
            .Where(f => f.Nombre.Equals(linea.Nombre.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        return porNombre.Count == 1 ? porNombre[0] : null;
    }

    private static string NombreLinea(LineaOrdenExportacion linea)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(linea.Codigo) && linea.Codigo != "—")
        {
            partes.Add(linea.Codigo.Trim());
        }

        if (!string.IsNullOrWhiteSpace(linea.Nombre) && linea.Nombre != "—")
        {
            partes.Add(linea.Nombre.Trim());
        }

        partes.Add($"x{linea.CantidadPedir.ToString("0.##", Cultura)}");
        return string.Join(" ", partes);
    }
}

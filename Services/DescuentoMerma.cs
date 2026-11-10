using System.Globalization;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public readonly record struct SolicitudMerma(Guid FichaId, double Cantidad, string Motivo, string Detalle, string Unidad);

public static class DescuentoMerma
{
    private static readonly object Sync = new();

    public static bool Intentar(IReadOnlyList<SolicitudMerma> solicitudes, out List<MermaRegistrada> aplicadas, out string? error)
    {
        aplicadas = [];
        error = null;
        if (solicitudes.Count == 0)
        {
            error = "Indique una cantidad mayor a cero.";
            return false;
        }

        lock (Sync)
        {
            var ahora = DateTimeOffset.Now;
            var repo = App.Instance.Repositorio;
            var fichas = new List<FichaTecnica>();
            var movimientos = new List<MovimientoInventario>();
            foreach (var solicitud in solicitudes)
            {
                if (solicitud.Cantidad <= PartidasInventario.Epsilon)
                {
                    continue;
                }

                var original = repo.Obtener(solicitud.FichaId);
                if (original is null)
                {
                    continue;
                }

                var ficha = original.Clonar();
                if (!PartidasInventario.IntentarSalidaFefo(
                        ficha,
                        solicitud.Cantidad,
                        null,
                        null,
                        null,
                        automatico: true,
                        out var consumos,
                        out error)
                    || consumos.Count == 0)
                {
                    aplicadas = [];
                    error = $"{original.Nombre}: {error ?? "No hay existencia suficiente."}";
                    return false;
                }

                var detalle = (solicitud.Detalle ?? string.Empty).Trim();
                var motivo = string.IsNullOrWhiteSpace(solicitud.Motivo) ? "Producto dañado" : solicitud.Motivo.Trim();
                var nota = detalle.Length == 0 ? motivo : $"{motivo}. {detalle}";
                var unidad = string.IsNullOrWhiteSpace(solicitud.Unidad) ? original.UnidadMedida : solicitud.Unidad.Trim();
                var resultante = ficha.Existencia + consumos.Sum(c => c.Cantidad);
                foreach (var consumo in consumos)
                {
                    resultante -= consumo.Cantidad;
                    var merma = new MermaRegistrada
                    {
                        Fecha = ahora,
                        FichaId = ficha.Id,
                        Codigo = ficha.Codigo,
                        Nombre = ficha.Nombre,
                        Lote = consumo.Partida.Lote,
                        FechaCaducidad = consumo.Partida.FechaCaducidad,
                        Cantidad = consumo.Cantidad,
                        Unidad = unidad,
                        Motivo = motivo,
                        Detalle = detalle,
                        ExistenciaResultante = resultante
                    };
                    aplicadas.Add(merma);
                    movimientos.Add(CrearSalida(merma, ficha, consumo.Partida.Id, resultante, nota));
                }

                fichas.Add(ficha);
            }

            if (movimientos.Count == 0)
            {
                error = "No hay existencia suficiente.";
                return false;
            }

            Guardar(repo, fichas, movimientos);
            return true;
        }
    }

    public static void Asegurar(IReadOnlyList<MermaRegistrada> mermas)
    {
        if (mermas.Count == 0)
        {
            return;
        }

        lock (Sync)
        {
            var repo = App.Instance.Repositorio;
            var porFicha = new Dictionary<Guid, FichaTecnica>();
            var movimientos = new List<MovimientoInventario>();
            foreach (var merma in mermas)
            {
                if (merma.Cantidad <= PartidasInventario.Epsilon)
                {
                    continue;
                }

                if (merma.Id == Guid.Empty)
                {
                    merma.Id = Guid.NewGuid();
                }

                var original = BuscarFicha(repo, merma);
                if (original is null)
                {
                    continue;
                }

                merma.FichaId = original.Id;
                if (YaRegistrado(merma))
                {
                    continue;
                }

                if (!porFicha.TryGetValue(original.Id, out var ficha))
                {
                    ficha = original.Clonar();
                    porFicha[original.Id] = ficha;
                }

                var yaEnStock = merma.ExistenciaResultante is double resultante
                    && ficha.Existencia <= resultante + PartidasInventario.Epsilon;
                Guid? partidaId = null;
                if (!yaEnStock)
                {
                    var lote = string.IsNullOrWhiteSpace(merma.Lote) ? null : merma.Lote;
                    if (!PartidasInventario.IntentarSalida(
                            ficha,
                            null,
                            lote,
                            merma.FechaCaducidad,
                            merma.Cantidad,
                            out var partida,
                            out _))
                    {
                        continue;
                    }

                    partidaId = partida?.Id;
                    if (string.IsNullOrWhiteSpace(merma.Lote) && partida is not null)
                    {
                        merma.Lote = partida.Lote;
                        merma.FechaCaducidad ??= partida.FechaCaducidad;
                    }

                    merma.ExistenciaResultante = ficha.Existencia;
                }

                var detalle = (merma.Detalle ?? string.Empty).Trim();
                var motivo = string.IsNullOrWhiteSpace(merma.Motivo) ? "Producto dañado" : merma.Motivo.Trim();
                var nota = detalle.Length == 0 ? motivo : $"{motivo}. {detalle}";
                movimientos.Add(CrearSalida(merma, ficha, partidaId, merma.ExistenciaResultante ?? ficha.Existencia, nota));
            }

            var cambiadas = porFicha.Values
                .Where(ficha =>
                {
                    var anterior = repo.Obtener(ficha.Id);
                    return anterior is null || Math.Abs(anterior.Existencia - ficha.Existencia) > PartidasInventario.Epsilon;
                })
                .ToList();
            if (cambiadas.Count == 0 && movimientos.Count == 0)
            {
                return;
            }

            Guardar(repo, cambiadas, movimientos);
        }
    }

    public static bool CambiarCantidad(Guid id, double cantidad, out string? error)
    {
        error = null;
        if (double.IsNaN(cantidad) || double.IsInfinity(cantidad) || cantidad <= PartidasInventario.Epsilon)
        {
            error = "Indique una cantidad mayor a cero. Para quitar la merma, use Revertir.";
            return false;
        }

        lock (Sync)
        {
            var merma = App.Instance.Mermas.Obtener(id);
            if (merma is null)
            {
                error = "Esa merma ya no está en la lista.";
                return false;
            }

            if (Math.Abs(merma.Cantidad - cantidad) <= PartidasInventario.Epsilon)
            {
                error = "La cantidad es la misma.";
                return false;
            }

            var repo = App.Instance.Repositorio;
            var original = BuscarFicha(repo, merma);
            if (original is null)
            {
                error = "El producto ya no está en el inventario.";
                return false;
            }

            var delta = cantidad - merma.Cantidad;
            var ficha = original.Clonar();
            PartidaInventario? partida = null;
            if (delta > PartidasInventario.Epsilon)
            {
                if (!PartidasInventario.IntentarSalida(
                        ficha,
                        null,
                        string.IsNullOrWhiteSpace(merma.Lote) ? null : merma.Lote,
                        merma.FechaCaducidad,
                        delta,
                        out partida,
                        out error))
                {
                    error ??= "No hay existencia suficiente para aumentar la merma.";
                    return false;
                }
            }
            else
            {
                partida = PartidasInventario.AplicarIngreso(
                    ficha,
                    merma.Lote,
                    merma.FechaCaducidad,
                    -delta);
            }

            var movimiento = BuscarMovimiento(merma);
            if (movimiento is not null)
            {
                var idMovimiento = movimiento.Id;
                var lote = partida?.Lote ?? merma.Lote;
                var caducidad = partida?.FechaCaducidad ?? merma.FechaCaducidad;
                var partidaId = partida?.Id ?? movimiento.PartidaId;
                if (!App.Instance.Registro.IntentarMutar(
                        ficha.Id,
                        original.Existencia,
                        lista =>
                        {
                            var actual = lista.FirstOrDefault(m => m.Id == idMovimiento);
                            if (actual is null)
                            {
                                return;
                            }

                            actual.Cantidad = cantidad;
                            actual.Lote = lote;
                            actual.FechaCaducidad = caducidad;
                            if (partidaId is Guid pid && pid != Guid.Empty)
                            {
                                actual.PartidaId = pid;
                            }
                        },
                        out _,
                        out error))
                {
                    error ??= "No se pudo actualizar el registro.";
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(partida?.Lote))
            {
                merma.Lote = partida.Lote;
            }

            if (partida?.FechaCaducidad is DateTimeOffset caduca)
            {
                merma.FechaCaducidad = caduca;
            }

            merma.Cantidad = cantidad;
            merma.FichaId = ficha.Id;
            merma.ExistenciaResultante = ficha.Existencia;
            Guardar(repo, [ficha], movimiento is null ? [CrearSalida(merma, ficha, partida?.Id, ficha.Existencia, Nota(merma))] : []);
            App.Instance.Mermas.Actualizar(merma);
            return true;
        }
    }

    public static bool Revertir(Guid id, out string? error)
    {
        error = null;
        lock (Sync)
        {
            var merma = App.Instance.Mermas.Obtener(id);
            if (merma is null)
            {
                error = "Esa merma ya no está en la lista.";
                return false;
            }

            var repo = App.Instance.Repositorio;
            var original = BuscarFicha(repo, merma);
            if (original is not null)
            {
                merma.FichaId = original.Id;
            }

            FichaTecnica? ficha = null;
            if (original is not null && merma.Cantidad > PartidasInventario.Epsilon)
            {
                ficha = original.Clonar();
                PartidasInventario.AplicarIngreso(ficha, merma.Lote, merma.FechaCaducidad, merma.Cantidad);
            }

            var movimiento = BuscarMovimiento(merma);
            if (movimiento is not null && original is not null)
            {
                var idMovimiento = movimiento.Id;
                if (!App.Instance.Registro.IntentarMutar(
                        original.Id,
                        original.Existencia,
                        lista => lista.RemoveAll(m => m.Id == idMovimiento),
                        out _,
                        out error))
                {
                    error ??= "No se pudo quitar la salida del registro.";
                    return false;
                }
            }

            if (ficha is not null)
            {
                Guardar(repo, [ficha], []);
            }

            App.Instance.Mermas.Eliminar(id);
            return true;
        }
    }

    private static string Nota(MermaRegistrada merma)
    {
        var detalle = (merma.Detalle ?? string.Empty).Trim();
        var motivo = string.IsNullOrWhiteSpace(merma.Motivo) ? "Producto dañado" : merma.Motivo.Trim();
        return detalle.Length == 0 ? motivo : $"{motivo}. {detalle}";
    }

    private static void Guardar(FichaRepository repo, IReadOnlyList<FichaTecnica> fichas, IReadOnlyList<MovimientoInventario> movimientos)
    {
        foreach (var ficha in fichas)
        {
            repo.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        }

        if (movimientos.Count > 0)
        {
            App.Instance.Registro.Agregar(movimientos);
        }

        if (fichas.Count > 0 || movimientos.Count > 0)
        {
            repo.NotificarVistas();
        }
    }

    private static MovimientoInventario CrearSalida(
        MermaRegistrada merma,
        FichaTecnica ficha,
        Guid? partidaId,
        double resultante,
        string nota)
    {
        var fecha = merma.Fecha == default ? DateTimeOffset.Now : merma.Fecha;
        return new MovimientoInventario
        {
            Id = merma.Id,
            FichaId = ficha.Id,
            PartidaId = partidaId,
            Lote = merma.Lote,
            FechaCaducidad = merma.FechaCaducidad,
            Area = ficha.Area,
            Fecha = fecha,
            Cantidad = merma.Cantidad,
            ExistenciaResultante = resultante,
            Tipo = "Salida",
            Origen = "Merma",
            TipoDocumento = "Merma",
            NumeroDocumento = fecha.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture),
            Nota = nota
        };
    }

    private static MovimientoInventario? BuscarMovimiento(MermaRegistrada merma)
    {
        MovimientoInventario? parecido = null;
        foreach (var movimiento in App.Instance.Registro.PorFicha(merma.FichaId))
        {
            if (movimiento.Id == merma.Id)
            {
                return movimiento;
            }

            if (parecido is not null || !movimiento.EsSalida || !movimiento.EsMerma)
            {
                continue;
            }

            if (Math.Abs(movimiento.Cantidad - merma.Cantidad) > PartidasInventario.Epsilon)
            {
                continue;
            }

            if (!LoteIgual(movimiento.Lote, merma.Lote))
            {
                continue;
            }

            if (Math.Abs((movimiento.Fecha - merma.Fecha).TotalMinutes) <= 3)
            {
                parecido = movimiento;
            }
        }

        return parecido;
    }

    private static bool YaRegistrado(MermaRegistrada merma) => BuscarMovimiento(merma) is not null;

    private static bool LoteIgual(string? izquierda, string? derecha) =>
        string.Equals((izquierda ?? string.Empty).Trim(), (derecha ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static FichaTecnica? BuscarFicha(FichaRepository repo, MermaRegistrada merma)
    {
        if (merma.FichaId != Guid.Empty)
        {
            var porId = repo.Obtener(merma.FichaId);
            if (porId is not null)
            {
                return porId;
            }
        }

        if (string.IsNullOrWhiteSpace(merma.Codigo))
        {
            return null;
        }

        var codigo = merma.Codigo.Trim();
        var coinciden = repo.Todas
            .Where(f => f.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (coinciden.Count == 1)
        {
            return coinciden[0];
        }

        if (coinciden.Count == 0 || string.IsNullOrWhiteSpace(merma.Nombre))
        {
            return null;
        }

        var nombre = merma.Nombre.Trim();
        return coinciden.FirstOrDefault(f => f.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
    }
}

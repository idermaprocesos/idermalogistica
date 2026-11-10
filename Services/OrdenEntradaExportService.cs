using System.Globalization;
using Iderma.Core.Services;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class OrdenEntradaExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static void ExportarPdf(string ruta, OrdenCompraRegistrada orden, bool a5 = false)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var documento = orden.Documento ?? new OrdenCompraDocumento();
        var folio = Folio(documento, orden);
        var lineas = LineasConArea(documento);
        var fechaOrden = FechaOrden(documento, orden);
        var fuente = a5 ? 7.5f : 9f;
        var espacio = a5 ? 7f : 10f;

        QuestPDF.Settings.License = LicenseType.Community;
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(a5 ? PageSizes.A5 : PageSizes.A4);
                    pagina.MarginHorizontal(a5 ? 16 : 28);
                    pagina.MarginTop(a5 ? 14 : 22);
                    pagina.MarginBottom(a5 ? 12 : 20);
                    pagina.DefaultTextStyle(x =>
                        x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(fuente).FontColor(IdermaMarca.Navy));
                    pagina.Header().Element(c => EncabezadoMarca(c, "Orden de Entrada", folio, a5));
                    pagina.Footer().Element(c => PieMarca(c, a5));
                    pagina.Content().PaddingTop(a5 ? 6 : 10).Column(col =>
                    {
                        col.Spacing(espacio);
                        col.Item().Element(c => CajaDatos(c, documento, orden, folio, fechaOrden, lineas.Count, a5));
                        col.Item().Element(c => TablaProductos(c, lineas, a5));
                        col.Item().Element(c => CajaRespuesta(c, folio, a5));
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Orden de Entrada {folio}" + (a5 ? " (A5)" : ""),
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = $"Ingreso por orden de compra {folio}"
            })
            .GeneratePdf(ruta);
    }

    private static List<LineaEntrada> LineasConArea(OrdenCompraDocumento documento)
    {
        var fichas = App.Instance.Repositorio.Todas;
        var porId = new Dictionary<Guid, FichaTecnica>();
        foreach (var ficha in fichas)
        {
            porId.TryAdd(ficha.Id, ficha);
        }

        var resultado = new List<LineaEntrada>();
        // Misma lista que la orden de compra: no filtrar por cantidad (muchas quedan en 0 = Cubierto).
        foreach (var linea in documento.Lineas ?? [])
        {
            var area = ResolverArea(linea, fichas, porId);
            var cantidad = linea.CantidadPedir > 0.0001
                ? linea.CantidadPedir
                : 0;
            resultado.Add(new LineaEntrada(
                string.IsNullOrWhiteSpace(linea.Codigo) || linea.Codigo == "—"
                    ? "—"
                    : linea.Codigo.Trim(),
                area,
                string.IsNullOrWhiteSpace(linea.Nombre) || linea.Nombre == "—"
                    ? "—"
                    : linea.Nombre.Trim(),
                cantidad));
        }

        return resultado;
    }

    private static string ResolverArea(
        LineaOrdenExportacion linea,
        IReadOnlyList<FichaTecnica> fichas,
        IReadOnlyDictionary<Guid, FichaTecnica> porId)
    {
        if (linea.FichaId != Guid.Empty && porId.TryGetValue(linea.FichaId, out var porFicha)
            && !string.IsNullOrWhiteSpace(porFicha.AreaTitulo))
        {
            return porFicha.AreaTitulo;
        }

        if (!string.IsNullOrWhiteSpace(linea.Codigo) && linea.Codigo != "—")
        {
            var match = fichas.FirstOrDefault(f =>
                f.Codigo.Equals(linea.Codigo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.AreaTitulo))
            {
                return match.AreaTitulo;
            }
        }

        if (!string.IsNullOrWhiteSpace(linea.Nombre) && linea.Nombre != "—")
        {
            var match = fichas.FirstOrDefault(f =>
                f.Nombre.Equals(linea.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.AreaTitulo))
            {
                return match.AreaTitulo;
            }
        }

        return "—";
    }

    private static void EncabezadoMarca(IContainer contenedor, string tituloDocumento, string metaDerecha, bool a5 = false)
    {
        var logoAncho = a5 ? 56f : 78f;
        var logoAlto = a5 ? 34f : 48f;
        var metaAncho = a5 ? 110f : 160f;
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(logoAncho).Height(logoAlto).Image(logo).FitArea();
                    fila.ConstantItem(a5 ? 6 : 10);
                }

                fila.RelativeItem().AlignMiddle().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(a5 ? 11 : 14).Bold().FontColor(IdermaMarca.Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(a5 ? 6.5f : 8).FontColor(IdermaMarca.Oro);
                });

                fila.ConstantItem(metaAncho).AlignRight().AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight().Text("Emitido").FontSize(a5 ? 6 : 7).FontColor("#5A6570");
                    meta.Item().AlignRight().Text($"{DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(a5 ? 7.5f : 9).SemiBold().FontColor(IdermaMarca.Navy);
                    if (!string.IsNullOrWhiteSpace(metaDerecha))
                    {
                        meta.Item().PaddingTop(2).AlignRight().Text(metaDerecha)
                            .FontSize(a5 ? 7 : 8).FontColor(IdermaMarca.Oro);
                    }
                });
            });

            col.Item().PaddingTop(a5 ? 4 : 6).Height(a5 ? 1.5f : 2).Background(IdermaMarca.Oro);
            col.Item().Height(1).Background(IdermaMarca.Navy);
            col.Item().PaddingTop(a5 ? 5 : 8).Background(IdermaMarca.Navy)
                .PaddingVertical(a5 ? 5 : 7).PaddingHorizontal(a5 ? 6 : 10)
                .Text(tituloDocumento).FontSize(a5 ? 11 : 13).Bold().FontColor(Colors.White).AlignCenter();
        });
    }

    private static void PieMarca(IContainer contenedor, bool a5 = false)
    {
        var fuente = a5 ? 6f : 7f;
        contenedor.Column(col =>
        {
            col.Item().Height(1).Background(IdermaMarca.Oro);
            col.Item().PaddingTop(a5 ? 3 : 4).Row(fila =>
            {
                fila.RelativeItem().Text($"{IdermaMarca.Nombre} · {IdermaMarca.Giro}")
                    .FontSize(fuente).FontColor("#5A6570");
                fila.ConstantItem(a5 ? 80 : 100).AlignRight().Text(texto =>
                {
                    texto.Span("Página ").FontSize(fuente).FontColor("#5A6570");
                    texto.CurrentPageNumber().FontSize(fuente).FontColor("#5A6570");
                    texto.Span(" de ").FontSize(fuente).FontColor("#5A6570");
                    texto.TotalPages().FontSize(fuente).FontColor("#5A6570");
                });
            });
        });
    }

    private static void CajaDatos(
        IContainer contenedor,
        OrdenCompraDocumento documento,
        OrdenCompraRegistrada orden,
        string folio,
        string fechaOrden,
        int productos,
        bool a5 = false)
    {
        contenedor.Border(0.8f).BorderColor(IdermaMarca.Navy).Background(IdermaMarca.FondoSuave)
            .Padding(a5 ? 6 : 10).Column(col =>
        {
            col.Spacing(a5 ? 4 : 6);
            col.Item().Row(fila =>
            {
                Dato(fila.RelativeItem(), "Folio", folio, a5);
                Dato(fila.RelativeItem(), "Fecha", fechaOrden, a5);
                Dato(fila.RelativeItem(), "Elaboró", NombreUsuarioLogeado(), a5);
            });
            col.Item().Row(fila =>
            {
                Dato(fila.RelativeItem(), "Estado", string.IsNullOrWhiteSpace(documento.Estado) ? "—" : documento.Estado.Trim(), a5);
                Dato(
                    fila.RelativeItem(),
                    "Proveedor",
                    string.IsNullOrWhiteSpace(documento.ProveedorUnico) ? "—" : documento.ProveedorUnico,
                    a5);
                Dato(fila.RelativeItem(), "Productos", TextoTotalProductos(productos), a5);
            });
            if (!string.IsNullOrWhiteSpace(documento.MesAnio) || !string.IsNullOrWhiteSpace(documento.PeriodoCobertura))
            {
                col.Item().Row(fila =>
                {
                    Dato(
                        fila.RelativeItem(),
                        "Mes",
                        string.IsNullOrWhiteSpace(documento.MesAnio) ? "—" : documento.MesAnio.Trim(),
                        a5);
                    Dato(
                        fila.RelativeItem(),
                        "Cobertura",
                        string.IsNullOrWhiteSpace(documento.PeriodoCobertura) ? "—" : documento.PeriodoCobertura.Trim(),
                        a5);
                    Dato(
                        fila.RelativeItem(),
                        "Registrada",
                        orden.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura),
                        a5);
                });
            }
        });
    }

    private static void TablaProductos(IContainer contenedor, IReadOnlyList<LineaEntrada> lineas, bool a5 = false)
    {
        contenedor.Column(col =>
        {
            col.Item().Background(IdermaMarca.Navy).PaddingVertical(a5 ? 4 : 5).PaddingHorizontal(a5 ? 6 : 8)
                .Text("Detalle de productos a ingresar")
                .FontSize(a5 ? 8.5f : 10).Bold().FontColor(Colors.White);
            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(a5 ? 48 : 70);
                    c.ConstantColumn(a5 ? 48 : 70);
                    c.RelativeColumn();
                    c.ConstantColumn(a5 ? 40 : 55);
                    c.ConstantColumn(a5 ? 44 : 60);
                });

                tabla.Header(h =>
                {
                    CeldaEncabezado(h, "Código", a5);
                    CeldaEncabezado(h, "Área", a5);
                    CeldaEncabezado(h, "Producto", a5);
                    CeldaEncabezado(h, "Pedidas", a5);
                    CeldaEncabezado(h, "A ingresar", a5);
                });

                if (lineas.Count == 0)
                {
                    tabla.Cell().ColumnSpan(5).Padding(a5 ? 5 : 8)
                        .Text("Sin productos en la orden de compra.").FontColor("#5A6570");
                    return;
                }

                for (var i = 0; i < lineas.Count; i++)
                {
                    var linea = lineas[i];
                    var fondo = i % 2 == 0 ? "#FFFFFF" : IdermaMarca.FondoSuave;
                    var cantidad = linea.Cantidad.ToString("0.##", Cultura);
                    Celda(tabla, fondo, linea.Codigo, a5: a5);
                    Celda(tabla, fondo, linea.Area, a5: a5);
                    Celda(tabla, fondo, linea.Nombre, a5: a5);
                    Celda(tabla, fondo, cantidad, derecha: true, a5: a5);
                    Celda(tabla, fondo, cantidad, derecha: true, a5: a5);
                }
            });
        });
    }

    private static void CajaRespuesta(IContainer contenedor, string folio, bool a5 = false)
    {
        contenedor.Border(0.8f).BorderColor(IdermaMarca.Oro).Padding(a5 ? 6 : 10).Column(col =>
        {
            col.Spacing(a5 ? 3 : 4);
            col.Item().Text("Orden de Entrada").FontSize(a5 ? 8.5f : 10).Bold().FontColor(IdermaMarca.Navy);
            col.Item().Text($"Generada el {DateTime.Now:dd/MM/yyyy HH:mm} · {folio}.")
                .FontSize(a5 ? 7.5f : 9).FontColor(IdermaMarca.Navy);
            col.Item().Text("Documento de ingreso a inventario a partir de la orden de compra.")
                .FontSize(a5 ? 6.5f : 8).FontColor("#5A6570");
        });
    }

    private static string TextoTotalProductos(int total) =>
        total == 1 ? "1 producto" : $"{total.ToString(Cultura)} productos";

    private static string NombreUsuarioLogeado()
    {
        try
        {
            using var sesiones = new SessionStore();
            var sesion = sesiones.Snapshot();
            if (!string.IsNullOrWhiteSpace(sesion.DisplayName))
            {
                return sesion.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(sesion.Username))
            {
                return sesion.Username.Trim();
            }
        }
        catch
        {
        }

        return "—";
    }

    private static void Dato(IContainer contenedor, string etiqueta, string valor, bool a5 = false) =>
        contenedor.Column(col =>
        {
            col.Item().Text(etiqueta).FontSize(a5 ? 6 : 7).FontColor("#5A6570");
            col.Item().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor)
                .FontSize(a5 ? 8 : 10).SemiBold().FontColor(IdermaMarca.Navy);
        });

    private static void CeldaEncabezado(TableCellDescriptor celdas, string texto, bool a5 = false) =>
        celdas.Cell().Background(IdermaMarca.Oro).Padding(a5 ? 3 : 4)
            .Text(texto).FontSize(a5 ? 6.5f : 8).Bold().FontColor(Colors.White);

    private static void Celda(TableDescriptor tabla, string fondo, string texto, bool derecha = false, bool a5 = false)
    {
        var celda = tabla.Cell()
            .Background(fondo)
            .BorderBottom(0.4f)
            .BorderColor("#E4E0D8")
            .Padding(a5 ? 3 : 4);
        var contenido = derecha ? celda.AlignRight() : celda;
        contenido.Text(string.IsNullOrWhiteSpace(texto) ? "—" : texto).FontSize(a5 ? 6.5f : 8);
    }

    public static string Folio(OrdenCompraDocumento documento, OrdenCompraRegistrada orden)
    {
        if (!string.IsNullOrWhiteSpace(documento.Correlativo))
        {
            return documento.Correlativo.Trim();
        }

        if (!string.IsNullOrWhiteSpace(documento.MesAnio))
        {
            return $"OC-{documento.MesAnio.Trim()}";
        }

        return $"OC-{orden.Id.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string FechaOrden(OrdenCompraDocumento documento, OrdenCompraRegistrada orden)
    {
        if (!string.IsNullOrWhiteSpace(documento.FechaElaboracion))
        {
            return documento.FechaElaboracion.Trim();
        }

        return orden.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura);
    }

    private sealed record LineaEntrada(string Codigo, string Area, string Nombre, double Cantidad);
}

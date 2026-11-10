using System.Globalization;
using ClosedXML.Excel;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class SolicitudesExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public static void ExportarExcel(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        var filas = new List<string[]>();
        foreach (var s in solicitudes)
        {
            if (s.Items.Count == 0)
            {
                filas.Add(FilaClinica(s, null));
                continue;
            }

            foreach (var linea in s.Items)
            {
                filas.Add(FilaClinica(s, linea));
            }
        }

        GuardarExcel(
            ruta,
            "Clínica",
            ["Folio", "Fecha", "Persona", "Urgente", "Estado", "Código", "Área", "Producto", "Pedidas", "Respondidas", "Fecha respuesta", "Nota"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        if (solicitudes.Count == 1)
        {
            ExportarPdfIndividual(ruta, solicitudes[0]);
            return;
        }

        var filas = new List<string[]>();
        foreach (var s in solicitudes)
        {
            var lineas = s.Items.Count == 0 ? [null] : s.Items.Cast<LineaSolicitudClinica?>().ToList();
            foreach (var linea in lineas)
            {
                var producto = linea is null
                    ? "Sin productos"
                    : Unir(linea.Codigo, linea.Area, linea.Nombre);
                var pedidas = linea?.Cantidad.ToString(Cultura) ?? "";
                var respondidas = linea?.CantidadRespuesta?.ToString(Cultura) ?? "";
                filas.Add(
                [
                    s.Folio,
                    s.FechaTexto,
                    s.Enfermera + (s.Urgente ? " · Urgente" : ""),
                    s.EstadoTexto,
                    producto,
                    pedidas,
                    respondidas,
                    NotaClinica(s)
                ]);
            }
        }

        GuardarPdf(
            ruta,
            "Solicitudes de clínica",
            solicitudes.Count,
            filas.Count,
            [58, 72, 90, 62, 0, 36, 42, 0],
            ["Folio", "Fecha", "Persona", "Estado", "Producto", "Ped.", "Resp.", "Nota"],
            filas);
    }

    public static void ExportarPdfIndividual(string ruta, SolicitudClinica solicitud, bool a5 = false)
    {
        var fuente = a5 ? 7.5f : 9f;
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
                    pagina.Header().Element(c => EncabezadoMarca(c, "Orden de Salida", solicitud.Folio, a5));
                    pagina.Footer().Element(c => PieMarca(c, a5));
                    pagina.Content().PaddingTop(a5 ? 6 : 10).Column(col =>
                    {
                        col.Spacing(a5 ? 7 : 10);
                        col.Item().Element(c => CajaDatosClinica(c, solicitud, a5));
                        col.Item().Element(c => TablaProductosClinica(c, solicitud, a5));
                        if (!solicitud.EstaPendiente)
                        {
                            col.Item().Element(c => CajaRespuestaClinica(c, solicitud, a5));
                        }
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Orden de Salida {solicitud.Folio}" + (a5 ? " (A5)" : ""),
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = solicitud.Resumen
            })
            .GeneratePdf(ruta);
    }

    public static void ExportarExcel(string ruta, IReadOnlyList<SolicitudIncidencia> solicitudes)
    {
        var filas = solicitudes.Select(s => new[]
        {
            s.Folio,
            s.FechaTexto,
            s.Solicitante,
            s.Area,
            s.TipoTexto,
            s.Urgente ? "Sí" : "No",
            s.EstadoTexto,
            s.Nombre,
            s.Cantidad.ToString("0.##", Cultura),
            s.Unidad,
            s.PrecioReposicion > 0 ? s.PrecioReposicion.ToString("0.00", Cultura) : "",
            s.Enlace,
            s.DetalleTexto,
            s.OrdenId,
            s.EstadoOrden,
            s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
            s.Motivo
        }).ToList();

        GuardarExcel(
            ruta,
            "Incidencias",
            ["Folio", "Fecha", "Solicitante", "Área", "Tipo", "Urgente", "Estado", "Nombre", "Cantidad", "Unidad", "Precio reposición", "Enlace", "Detalle", "Orden", "Estado orden", "Fecha respuesta", "Motivo"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<SolicitudIncidencia> solicitudes)
    {
        var filas = solicitudes.Select(s => new[]
        {
            s.Folio,
            s.FechaTexto,
            Unir(s.Solicitante, s.Area, s.TipoTexto) + (s.Urgente ? " · Urgente" : ""),
            s.EstadoTexto,
            Unir(s.Nombre, $"{s.Cantidad.ToString("0.##", Cultura)} {s.Unidad}".Trim(), s.PrecioReposicion > 0 ? s.PrecioReposicion.ToString("C", Cultura) : ""),
            Unir(s.DetalleTexto, s.Enlace),
            Unir(s.EstadoOrden, s.OrdenId),
            NotaIncidencia(s)
        }).ToList();

        GuardarPdf(
            ruta,
            "Reportes de incidencias",
            solicitudes.Count,
            filas.Count,
            [58, 72, 110, 58, 0, 0, 70, 90],
            ["Folio", "Fecha", "Quién", "Estado", "Ítem", "Detalle", "Orden", "Respuesta"],
            filas);
    }

    public static void ExportarExcel(string ruta, IReadOnlyList<AvisoAutorizacion> avisos)
    {
        var filas = avisos.Select(a => new[]
        {
            a.Folio,
            a.FechaTexto,
            a.Solicitante,
            a.EstadoTexto,
            a.Resumen,
            a.Detalle,
            a.Respuesta
        }).ToList();

        GuardarExcel(
            ruta,
            "Autorizaciones",
            ["Folio", "Fecha", "Solicitante", "Estado", "Resumen", "Productos", "Respuesta"],
            filas);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<AvisoAutorizacion> avisos)
    {
        var filas = avisos.Select(a => new[]
        {
            a.Folio,
            a.FechaTexto,
            a.Solicitante,
            a.EstadoTexto,
            a.Resumen,
            a.Detalle,
            a.Respuesta
        }).ToList();

        GuardarPdf(
            ruta,
            "Autorizaciones de base de datos",
            avisos.Count,
            filas.Count,
            [58, 72, 80, 62, 0, 0, 120],
            ["Folio", "Fecha", "Solicitante", "Estado", "Resumen", "Productos", "Respuesta"],
            filas);
    }

    private static string[] FilaClinica(SolicitudClinica s, LineaSolicitudClinica? linea) =>
    [
        s.Folio,
        s.FechaTexto,
        s.Enfermera,
        s.Urgente ? "Sí" : "No",
        s.EstadoTexto,
        linea?.Codigo ?? "",
        linea?.Area ?? "",
        linea?.Nombre ?? "",
        linea?.Cantidad.ToString(Cultura) ?? "",
        linea?.CantidadRespuesta?.ToString(Cultura) ?? "",
        s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "",
        NotaClinica(s)
    ];

    private static string NotaClinica(SolicitudClinica s) =>
        s.EstaPendiente ? "" : s.ResumenRespuesta;

    private static string NotaIncidencia(SolicitudIncidencia s)
    {
        if (s.EstaPendiente)
        {
            return "";
        }

        var fecha = s.FechaRespuesta?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "";
        return Unir(s.EstadoTexto, fecha, s.Motivo);
    }

    private static string Unir(params string?[] partes) =>
        string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    private static void GuardarExcel(string ruta, string hojaNombre, string[] titulos, IReadOnlyList<string[]> filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet(hojaNombre);
        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = titulos[i];
        }

        var encabezado = hoja.Range(1, 1, 1, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontSize = 9;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");
        encabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        for (var i = 0; i < filas.Count; i++)
        {
            for (var c = 0; c < titulos.Length; c++)
            {
                var celda = hoja.Cell(i + 2, c + 1);
                celda.Value = c < filas[i].Length ? filas[i][c] : "";
                celda.Style.Font.FontSize = 9;
                celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                celda.Style.Alignment.WrapText = true;
            }
        }

        hoja.SheetView.FreezeRows(1);
        hoja.RangeUsed()?.SetAutoFilter();
        hoja.Rows().Height = 16;
        hoja.Row(1).Height = 18;
        hoja.Columns().AdjustToContents(8, 42);
        libro.SaveAs(ruta);
    }

    private static void GuardarPdf(
        string ruta,
        string titulo,
        int registros,
        int lineas,
        float[] anchos,
        string[] titulos,
        IReadOnlyList<string[]> filas)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4.Landscape());
                    pagina.MarginHorizontal(18);
                    pagina.MarginTop(16);
                    pagina.MarginBottom(14);
                    pagina.DefaultTextStyle(x =>
                        x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(IdermaMarca.Navy));
                    pagina.Header().Element(c => EncabezadoMarca(
                        c,
                        titulo,
                        $"{registros} registros · {lineas} líneas"));
                    pagina.Footer().Element(c => PieMarca(c));
                    pagina.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Background(IdermaMarca.Navy).PaddingVertical(5).PaddingHorizontal(8)
                            .Text($"Notificaciones · {titulo}")
                            .FontSize(11).Bold().FontColor(Colors.White).AlignCenter();
                        col.Item().PaddingTop(6).Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c =>
                            {
                                foreach (var ancho in anchos)
                                {
                                    if (ancho <= 0)
                                    {
                                        c.RelativeColumn();
                                    }
                                    else
                                    {
                                        c.ConstantColumn(ancho);
                                    }
                                }
                            });

                            tabla.Header(h =>
                            {
                                foreach (var texto in titulos)
                                {
                                    h.Cell().Background(IdermaMarca.Navy).Padding(3)
                                        .Text(texto).FontSize(7).FontColor(Colors.White).SemiBold();
                                }
                            });

                            if (filas.Count == 0)
                            {
                                tabla.Cell().ColumnSpan((uint)titulos.Length).Padding(6)
                                    .Text("No hay registros.").FontColor("#5A6570");
                                return;
                            }

                            for (var i = 0; i < filas.Count; i++)
                            {
                                var fondo = i % 2 == 0 ? "#FFFFFF" : IdermaMarca.FondoSuave;
                                foreach (var texto in filas[i])
                                {
                                    tabla.Cell()
                                        .Background(fondo)
                                        .BorderBottom(0.4f)
                                        .BorderColor("#E4E0D8")
                                        .Padding(3)
                                        .Text(string.IsNullOrWhiteSpace(texto) ? "—" : texto)
                                        .FontSize(7);
                                }
                            }
                        });
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = titulo,
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = $"Notificaciones · {titulo}"
            })
            .GeneratePdf(ruta);
    }

    private static void EncabezadoMarca(IContainer contenedor, string tituloDocumento, string metaDerecha, bool a5 = false)
    {
        var logoAncho = a5 ? 56f : 78f;
        var logoAlto = a5 ? 34f : 48f;
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

                fila.ConstantItem(a5 ? 110 : 160).AlignRight().AlignMiddle().Column(meta =>
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
            if (!string.IsNullOrWhiteSpace(tituloDocumento))
            {
                col.Item().PaddingTop(a5 ? 5 : 8).Background(IdermaMarca.Navy)
                    .PaddingVertical(a5 ? 5 : 7).PaddingHorizontal(a5 ? 6 : 10)
                    .Text(tituloDocumento).FontSize(a5 ? 11 : 13).Bold().FontColor(Colors.White).AlignCenter();
            }
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

    private static void CajaDatosClinica(IContainer contenedor, SolicitudClinica solicitud, bool a5 = false)
    {
        contenedor.Border(0.8f).BorderColor(IdermaMarca.Navy).Background(IdermaMarca.FondoSuave)
            .Padding(a5 ? 6 : 10).Column(col =>
        {
            col.Spacing(a5 ? 4 : 6);
            col.Item().Row(fila =>
            {
                Dato(fila.RelativeItem(), "Folio", solicitud.Folio, a5);
                Dato(fila.RelativeItem(), "Fecha", solicitud.FechaTexto, a5);
                Dato(fila.RelativeItem(), "Solicitante", string.IsNullOrWhiteSpace(solicitud.Enfermera) ? "—" : solicitud.Enfermera, a5);
            });
            col.Item().Row(fila =>
            {
                Dato(fila.RelativeItem(), "Estado", solicitud.EstadoTexto, a5);
                Dato(fila.RelativeItem(), "Prioridad", solicitud.Urgente ? "Urgente" : "Normal", a5);
                Dato(fila.RelativeItem(), "Productos", TextoTotalProductos(solicitud.Items.Count), a5);
            });
        });
    }

    private static void TablaProductosClinica(IContainer contenedor, SolicitudClinica solicitud, bool a5 = false)
    {
        contenedor.Column(col =>
        {
            col.Item().Background(IdermaMarca.Navy).PaddingVertical(a5 ? 4 : 5).PaddingHorizontal(a5 ? 6 : 8)
                .Text("Detalle de productos de la salida")
                .FontSize(a5 ? 8.5f : 10).Bold().FontColor(Colors.White);
            col.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(a5 ? 48 : 70);
                    c.ConstantColumn(a5 ? 48 : 70);
                    c.RelativeColumn();
                    c.ConstantColumn(a5 ? 40 : 55);
                    c.ConstantColumn(a5 ? 48 : 60);
                });

                tabla.Header(h =>
                {
                    CeldaEncabezado(h, "Código", a5);
                    CeldaEncabezado(h, "Área", a5);
                    CeldaEncabezado(h, "Producto", a5);
                    CeldaEncabezado(h, "Pedidas", a5);
                    CeldaEncabezado(h, "Respondidas", a5);
                });

                if (solicitud.Items.Count == 0)
                {
                    tabla.Cell().ColumnSpan(5).Padding(a5 ? 5 : 8).Text("Sin productos en la solicitud.").FontColor("#5A6570");
                    return;
                }

                for (var i = 0; i < solicitud.Items.Count; i++)
                {
                    var linea = solicitud.Items[i];
                    var fondo = i % 2 == 0 ? "#FFFFFF" : IdermaMarca.FondoSuave;
                    Celda(tabla, fondo, linea.Codigo, a5: a5);
                    Celda(tabla, fondo, linea.Area, a5: a5);
                    Celda(tabla, fondo, linea.Nombre, a5: a5);
                    Celda(tabla, fondo, linea.Cantidad.ToString(Cultura), derecha: true, a5: a5);
                    Celda(tabla, fondo, linea.CantidadRespuesta?.ToString(Cultura) ?? "—", derecha: true, a5: a5);
                }
            });
        });
    }

    private static void CajaRespuestaClinica(IContainer contenedor, SolicitudClinica solicitud, bool a5 = false)
    {
        contenedor.Border(0.8f).BorderColor(IdermaMarca.Oro).Padding(a5 ? 6 : 10).Column(col =>
        {
            col.Spacing(a5 ? 3 : 4);
            col.Item().Text("Respuesta de logística").FontSize(a5 ? 8.5f : 10).Bold().FontColor(IdermaMarca.Navy);
            col.Item().Text(solicitud.ResumenRespuesta).FontSize(a5 ? 7.5f : 9).FontColor(IdermaMarca.Navy);
            if (!string.IsNullOrWhiteSpace(solicitud.MotivoDenegacion) &&
                !solicitud.ResumenRespuesta.Contains(solicitud.MotivoDenegacion, StringComparison.Ordinal))
            {
                col.Item().Text(solicitud.MotivoDenegacion).FontSize(a5 ? 6.5f : 8).FontColor("#5A6570");
            }
        });
    }

    private static string TextoTotalProductos(int total) =>
        total == 1 ? "1 producto" : $"{total.ToString(Cultura)} productos";

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
}

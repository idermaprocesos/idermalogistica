using ClosedXML.Excel;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class SolicitudesExportService
{
    public static void ExportarExcel(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Notificaciones");
        string[] titulos =
        [
            "Folio", "Fecha", "Enfermera", "Urgente", "Estado", "Productos", "Piezas",
            "Detalle", "Fecha respuesta", "Motivo denegación"
        ];

        for (var i = 0; i < titulos.Length; i++)
        {
            hoja.Cell(1, i + 1).Value = titulos[i];
        }

        var encabezado = hoja.Range(1, 1, 1, titulos.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Font.FontColor = XLColor.White;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#0F766E");

        for (var i = 0; i < solicitudes.Count; i++)
        {
            var s = solicitudes[i];
            var fila = i + 2;
            hoja.Cell(fila, 1).Value = s.Folio;
            hoja.Cell(fila, 2).Value = s.Fecha.ToLocalTime().DateTime;
            hoja.Cell(fila, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            hoja.Cell(fila, 3).Value = s.Enfermera;
            hoja.Cell(fila, 4).Value = s.Urgente ? "Sí" : "No";
            hoja.Cell(fila, 5).Value = s.EstadoTexto;
            hoja.Cell(fila, 6).Value = s.Items.Count;
            hoja.Cell(fila, 7).Value = s.TotalCantidad;
            hoja.Cell(fila, 8).Value = s.Detalle;
            hoja.Cell(fila, 9).Value = s.FechaRespuesta?.ToLocalTime().DateTime;
            hoja.Cell(fila, 9).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            hoja.Cell(fila, 10).Value = s.MotivoDenegacion;
        }

        hoja.Columns().AdjustToContents();
        hoja.SheetView.FreezeRows(1);
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, IReadOnlyList<SolicitudClinica> solicitudes)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Margin(36);
                pagina.Size(PageSizes.A4);
                pagina.DefaultTextStyle(x => x.FontSize(10));
                pagina.Header().Column(col =>
                {
                    col.Item().Text("Notificaciones · Solicitudes de clínica").SemiBold().FontSize(16);
                    col.Item().Text($"Exportado {DateTime.Now:dd/MM/yyyy HH:mm} · {solicitudes.Count} registros")
                        .FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Teal.Medium);
                });

                pagina.Content().PaddingTop(12).Column(col =>
                {
                    if (solicitudes.Count == 0)
                    {
                        col.Item().Text("No hay notificaciones para exportar.");
                        return;
                    }

                    foreach (var s in solicitudes)
                    {
                        col.Item().PaddingBottom(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(bloque =>
                        {
                            bloque.Item().Row(fila =>
                            {
                                fila.RelativeItem().Text(s.Folio).SemiBold();
                                fila.ConstantItem(90).AlignRight().Text(s.EstadoTexto).SemiBold();
                            });
                            bloque.Item().Text($"{s.FechaTexto} · {s.Enfermera}" + (s.Urgente ? " · URGENTE" : string.Empty))
                                .FontColor(Colors.Grey.Darken2);
                            bloque.Item().PaddingTop(4).Text(s.Detalle);
                            if (!s.EstaPendiente)
                            {
                                bloque.Item().PaddingTop(4).Text(s.ResumenRespuesta).Italic();
                            }
                        });
                    }
                });

                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf(ruta);
    }
}

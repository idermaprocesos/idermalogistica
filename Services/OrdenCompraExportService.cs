using System.Globalization;
using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class OrdenCompraExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string AzulPrecios = "#1E4E8C";
    private static readonly string Gris = "#5A6570";
    private static readonly string Linea = "#D8D2C6";

    private static readonly string[] Columnas =
    [
        "#", "CÓD.", "NOMBRE DEL INSUMO", "MARCA", "CONS. PROM.",
        "CANT. PEDIR", "UND.", "PRECIO UNIT. (sin IGV)", "IGV 18%", "TOTAL (con IGV)", "ESTADO"
    ];

    public static void ExportarExcel(string ruta, OrdenCompraDocumento documento)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Orden de compra");
        const int columnas = 11;

        hoja.Style.Font.FontName = "Calibri";
        hoja.Style.Font.FontSize = 10;
        hoja.Row(1).Height = 22;
        hoja.Row(2).Height = 16;
        hoja.Row(3).Height = 6;
        hoja.Row(4).Height = 24;
        hoja.Row(5).Height = 22;
        hoja.Row(6).Height = 22;
        hoja.Row(7).Height = 22;

        hoja.Range(1, 1, 2, columnas).Style.Fill.BackgroundColor = XLColor.White;
        InsertarLogo(hoja);
        hoja.Range(1, 3, 1, 7).Merge();
        hoja.Cell(1, 3).Value = IdermaMarca.NombreMayusculas;
        hoja.Cell(1, 3).Style.Font.Bold = true;
        hoja.Cell(1, 3).Style.Font.FontSize = 16;
        hoja.Cell(1, 3).Style.Font.FontColor = XLColor.FromHtml(Navy);
        hoja.Cell(1, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Range(2, 3, 2, 7).Merge();
        hoja.Cell(2, 3).Value = IdermaMarca.Giro;
        hoja.Cell(2, 3).Style.Font.FontSize = 10;
        hoja.Cell(2, 3).Style.Font.FontColor = XLColor.FromHtml(Oro);
        hoja.Cell(2, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Range(1, 8, 1, columnas).Merge();
        hoja.Cell(1, 8).Value = Vacio(documento.Correlativo);
        hoja.Cell(1, 8).Style.Font.Bold = true;
        hoja.Cell(1, 8).Style.Font.FontSize = 14;
        hoja.Cell(1, 8).Style.Font.FontColor = XLColor.FromHtml(Navy);
        hoja.Cell(1, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        hoja.Cell(1, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        hoja.Range(2, 8, 2, columnas).Merge();
        hoja.Cell(2, 8).Value = "Nº OC";
        hoja.Cell(2, 8).Style.Font.FontSize = 9;
        hoja.Cell(2, 8).Style.Font.FontColor = XLColor.FromHtml(Gris);
        hoja.Cell(2, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        hoja.Range(3, 1, 3, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Oro);

        hoja.Range(4, 1, 4, 8).Merge();
        hoja.Range(4, 1, 4, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);
        hoja.Range(4, 1, 4, 8).Style.Font.Bold = true;
        hoja.Range(4, 1, 4, 8).Style.Font.FontSize = 13;
        hoja.Range(4, 1, 4, 8).Style.Font.FontColor = XLColor.White;
        hoja.Range(4, 1, 4, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        hoja.Range(4, 1, 4, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(4, 1).Value = documento.TituloImpresion;

        hoja.Range(4, 9, 4, columnas).Merge();
        hoja.Range(4, 9, 4, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);
        hoja.Range(4, 9, 4, columnas).Style.Font.Bold = true;
        hoja.Range(4, 9, 4, columnas).Style.Font.FontSize = 11;
        hoja.Range(4, 9, 4, columnas).Style.Font.FontColor = XLColor.White;
        hoja.Range(4, 9, 4, columnas).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        hoja.Range(4, 9, 4, columnas).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(4, 9).Value = $"Ítems: {documento.Lineas.Count.ToString(Cultura)}";

        Meta(hoja, 5, 1, 4, "Proveedor", documento.ProveedorUnico);
        Meta(hoja, 5, 5, 7, "RUC", documento.RucUnico);
        Meta(hoja, 5, 8, 9, "Mes / Año", documento.MesAnio);
        Meta(hoja, 5, 10, columnas, "Fecha de elaboración", documento.FechaElaboracion);
        Meta(hoja, 6, 1, 4, "Elaborado por", documento.ElaboradoPor);
        Meta(hoja, 6, 5, 7, "Revisado por", documento.RevisadoPor);
        Meta(hoja, 6, 8, 9, "Período de cobertura", documento.PeriodoCobertura);
        Meta(hoja, 6, 10, columnas, "Estado", documento.Estado);

        var meta = hoja.Range(5, 1, 6, columnas);
        meta.Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);
        meta.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        meta.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        meta.Style.Border.OutsideBorderColor = XLColor.FromHtml(Navy);

        const int filaGrupo = 8;
        const int filaCabeza = 9;
        Grupo(hoja, filaGrupo, 1, 7, "DETALLE DEL INSUMO", Navy);
        Grupo(hoja, filaGrupo, 8, 10, documento.TituloPrecios, AzulPrecios);
        Grupo(hoja, filaGrupo, 11, columnas, "ESTADO", Navy);
        hoja.Row(filaGrupo).Height = 18;

        for (var i = 0; i < Columnas.Length; i++)
        {
            var celda = hoja.Cell(filaCabeza, i + 1);
            celda.Value = Columnas[i];
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontSize = 8;
            celda.Style.Font.FontColor = XLColor.White;
            celda.Style.Fill.BackgroundColor = XLColor.FromHtml(i is >= 7 and <= 9 ? AzulPrecios : Navy);
            celda.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            celda.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            celda.Style.Alignment.WrapText = true;
        }

        hoja.Row(filaCabeza).Height = 32;

        var fila = filaCabeza;
        for (var i = 0; i < documento.Lineas.Count; i++)
        {
            fila++;
            var linea = documento.Lineas[i];
            var fondo = i % 2 == 0 ? XLColor.White : XLColor.FromHtml(Fondo);
            hoja.Cell(fila, 1).Value = i + 1;
            hoja.Cell(fila, 2).Value = linea.Codigo;
            hoja.Cell(fila, 3).Value = linea.Nombre;
            hoja.Cell(fila, 4).Value = linea.Marca;
            hoja.Cell(fila, 5).Value = linea.ConsumoPromedio;
            hoja.Cell(fila, 6).Value = linea.CantidadPedir;
            hoja.Cell(fila, 7).Value = linea.Unidad;
            if (linea.TienePrecio)
            {
                hoja.Cell(fila, 8).Value = linea.PrecioUnitarioSinIgv;
                hoja.Cell(fila, 9).Value = linea.Igv;
                hoja.Cell(fila, 10).Value = linea.TotalConIgv;
            }
            else
            {
                hoja.Cell(fila, 8).Value = "Sin precio";
                hoja.Cell(fila, 9).Value = "—";
                hoja.Cell(fila, 10).Value = "—";
            }

            hoja.Cell(fila, 11).Value = linea.Estado;

            var rango = hoja.Range(fila, 1, fila, columnas);
            rango.Style.Fill.BackgroundColor = fondo;
            rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            hoja.Cell(fila, 3).Style.Alignment.WrapText = true;
            foreach (var col in new[] { 1, 7, 11 })
            {
                hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            foreach (var col in new[] { 5, 6 })
            {
                hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
                hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }

            if (linea.TienePrecio)
            {
                foreach (var col in new[] { 8, 9, 10 })
                {
                    hoja.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";
                    hoja.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
            }
        }

        var filaDatosFin = Math.Max(fila, filaCabeza);
        foreach (var total in Totales(documento))
        {
            fila++;
            hoja.Range(fila, 1, fila, 7).Merge();
            hoja.Cell(fila, 1).Value = $"Subtotal productos {MonedaPrecio.Simbolo(total.Moneda)}";
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            hoja.Cell(fila, 9).Value = total.Igv;
            hoja.Cell(fila, 10).Value = total.Total;
            hoja.Cell(fila, 9).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 10).Style.NumberFormat.Format = "#,##0.00";
            hoja.Cell(fila, 9).Style.Font.Bold = true;
            hoja.Cell(fila, 10).Style.Font.Bold = true;
            hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);
        }

        var tabla = hoja.Range(filaGrupo, 1, Math.Max(fila, filaDatosFin), columnas);
        tabla.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tabla.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tabla.Style.Border.OutsideBorderColor = XLColor.FromHtml(Navy);
        tabla.Style.Border.InsideBorderColor = XLColor.FromHtml(Linea);

        fila += 2;
        EscribirBloqueFleteExcel(hoja, ref fila, columnas, documento);

        fila += 2;
        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value =
            "Precio sin IGV = último precio − 18 %. IGV = 18 % de ese precio, en la moneda registrada (soles o dólares).";
        hoja.Cell(fila, 1).Style.Font.Italic = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 8;
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(Gris);

        fila += 2;
        EscribirDatosPagoExcel(hoja, ref fila, columnas, documento);

        fila += 1;
        hoja.Range(fila, 1, fila, 6).Merge();
        hoja.Cell(fila, 1).Value = string.IsNullOrWhiteSpace(documento.ElaboradoPor)
            ? "Elaborado por: —"
            : $"Elaborado por: {documento.ElaboradoPor.Trim()}";
        hoja.Range(fila, 7, fila, columnas).Merge();
        hoja.Cell(fila, 7).Value = string.IsNullOrWhiteSpace(documento.RevisadoPor)
            ? "Revisado por: —"
            : $"Revisado por: {documento.RevisadoPor.Trim()}";

        fila++;
        hoja.Range(fila, 1, fila, 6).Merge();
        hoja.Cell(fila, 1).Value = "Firma: ______________________________";
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml(Gris);
        hoja.Range(fila, 7, fila, columnas).Merge();
        hoja.Cell(fila, 7).Value = "Firma: ______________________________";
        hoja.Cell(fila, 7).Style.Font.FontColor = XLColor.FromHtml(Gris);

        double[] anchos = [5, 14, 36, 14, 12, 12, 8, 16, 12, 15, 14];
        for (var i = 0; i < anchos.Length; i++)
        {
            hoja.Column(i + 1).Width = anchos[i];
        }

        hoja.SheetView.FreezeRows(filaCabeza);
        hoja.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        hoja.PageSetup.PaperSize = XLPaperSize.A4Paper;
        hoja.PageSetup.FitToPages(1, 0);
        hoja.PageSetup.CenterHorizontally = true;
        hoja.PageSetup.Margins.Top = 0.4;
        hoja.PageSetup.Margins.Bottom = 0.5;
        hoja.PageSetup.Margins.Left = 0.4;
        hoja.PageSetup.Margins.Right = 0.4;
        hoja.PageSetup.Footer.Right.AddText("Página &P de &N");

        IdermaMarca.AplicarPropiedades(libro, documento.TituloImpresion);
        libro.SaveAs(ruta);
    }

    public static void ExportarPdf(string ruta, OrdenCompraDocumento documento)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4.Landscape());
                    pagina.MarginHorizontal(22);
                    pagina.MarginTop(18);
                    pagina.MarginBottom(16);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(Navy));
                    pagina.Header().Element(c => Encabezado(c, documento));
                    pagina.Footer().Element(Pie);
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(4);
                        col.Item().Background(Navy).PaddingVertical(6).PaddingHorizontal(10).Row(titulo =>
                        {
                            titulo.RelativeItem().AlignMiddle().AlignCenter()
                                .Text(documento.TituloImpresion)
                                .FontSize(12).Bold().FontColor(Colors.White);
                            titulo.ConstantItem(90).AlignMiddle().AlignRight()
                                .Text($"Ítems: {documento.Lineas.Count.ToString(Cultura)}")
                                .FontSize(10).Bold().FontColor(Colors.White);
                        });
                        col.Item().Element(c => CajaControl(c, documento));
                        col.Item().Element(c => Tabla(c, documento));
                        col.Item().Element(c => TotalesYFletePdf(c, documento));
                        col.Item().Text(
                                "Precio sin IGV = último precio − 18 %. IGV = 18 % de ese precio, en la moneda registrada (soles o dólares).")
                            .FontSize(7).Italic().FontColor(Gris);
                        col.Item().Element(c => DatosPagoPdf(c, documento));
                        col.Item().Element(c => Firmas(c, documento));
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = documento.TituloImpresion,
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = $"{documento.Correlativo} · {documento.MesAnio}"
            })
            .GeneratePdf(ruta);
    }

    private static void Encabezado(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(78).Height(42).Image(logo).FitArea();
                    fila.ConstantItem(8);
                }

                fila.RelativeItem().AlignMiddle().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(8).FontColor(Oro);
                });

                fila.ConstantItem(150).AlignRight().AlignMiddle().Column(meta =>
                {
                    meta.Item().AlignRight().Text("Nº OC").FontSize(8).FontColor(Gris);
                    meta.Item().AlignRight().Text(Vacio(documento.Correlativo)).FontSize(14).Bold().FontColor(Navy);
                });
            });

            col.Item().PaddingTop(6).Height(2).Background(Oro);
            col.Item().Height(1).Background(Navy);
        });
    }

    private static void Pie(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Height(1).Background(Oro);
            col.Item().PaddingTop(4).AlignRight().Text(texto =>
            {
                texto.Span("Página ").FontSize(7).FontColor(Gris);
                texto.CurrentPageNumber().FontSize(7).FontColor(Gris);
                texto.Span(" de ").FontSize(7).FontColor(Gris);
                texto.TotalPages().FontSize(7).FontColor(Gris);
            });
        });
    }

    private static void CajaControl(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.Border(0.8f).BorderColor(Navy).Background(Fondo).Padding(8).Column(col =>
        {
            col.Spacing(4);
            col.Item().Row(fila =>
            {
                Dato(fila, "Proveedor", documento.ProveedorUnico);
                Dato(fila, "RUC", documento.RucUnico);
                Dato(fila, "Mes / Año", Vacio(documento.MesAnio));
                Dato(fila, "Fecha de elaboración", Vacio(documento.FechaElaboracion));
            });
            col.Item().Row(fila =>
            {
                Dato(fila, "Elaborado por", Vacio(documento.ElaboradoPor));
                Dato(fila, "Revisado por", Vacio(documento.RevisadoPor));
                Dato(fila, "Período de cobertura", Vacio(documento.PeriodoCobertura));
                Dato(fila, "Estado", Vacio(documento.Estado));
            });
        });
    }

    private static void DatosPagoPdf(IContainer contenedor, OrdenCompraDocumento documento)
    {
        var datos = DatosPago(documento);
        contenedor.PaddingTop(2).Border(0.6f).BorderColor(Linea).Column(col =>
        {
            col.Item().Background(Navy).PaddingVertical(2).PaddingHorizontal(4)
                .Text("DATOS DE PAGO Y ENTREGA").FontSize(7).Bold().FontColor(Colors.White);
            col.Item().Background(Fondo).PaddingVertical(2).PaddingHorizontal(4).Row(fila =>
            {
                ParPago(fila, "Banco", datos.Banco);
                ParPago(fila, "Cuenta", datos.Cuenta);
                ParPago(fila, "CCI", datos.Cci);
                ParPago(fila, "Pago", datos.Condicion);
            });
            col.Item().PaddingVertical(2).PaddingHorizontal(4).Text(texto =>
            {
                texto.Span("Entrega: ").FontSize(7).Bold().FontColor(Navy);
                texto.Span(datos.Entrega).FontSize(7).FontColor(Navy);
            });
        });

        static void ParPago(RowDescriptor fila, string etiqueta, string valor)
        {
            fila.RelativeItem().Text(texto =>
            {
                texto.Span($"{etiqueta}: ").FontSize(7).Bold().FontColor(Navy);
                texto.Span(valor).FontSize(7).FontColor(Navy);
            });
        }
    }

    private static void EscribirDatosPagoExcel(IXLWorksheet hoja, ref int fila, int columnas, OrdenCompraDocumento documento)
    {
        var datos = DatosPago(documento);
        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value = "DATOS DE PAGO Y ENTREGA";
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 8;
        hoja.Cell(fila, 1).Style.Font.FontColor = XLColor.White;
        hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Navy);
        hoja.Row(fila).Height = 14;

        fila++;
        hoja.Range(fila, 1, fila, 3).Merge();
        hoja.Cell(fila, 1).Value = $"Banco: {datos.Banco}";
        hoja.Range(fila, 4, fila, 6).Merge();
        hoja.Cell(fila, 4).Value = $"Cuenta: {datos.Cuenta}";
        hoja.Range(fila, 7, fila, 8).Merge();
        hoja.Cell(fila, 7).Value = $"CCI: {datos.Cci}";
        hoja.Range(fila, 9, fila, columnas).Merge();
        hoja.Cell(fila, 9).Value = $"Pago: {datos.Condicion}";
        hoja.Range(fila, 1, fila, columnas).Style.Font.FontSize = 8;
        hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);
        hoja.Range(fila, 1, fila, columnas).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        hoja.Range(fila, 1, fila, columnas).Style.Border.OutsideBorderColor = XLColor.FromHtml(Linea);
        hoja.Row(fila).Height = 15;

        fila++;
        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value = $"Entrega: {datos.Entrega}";
        hoja.Cell(fila, 1).Style.Font.FontSize = 8;
        hoja.Range(fila, 1, fila, columnas).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        hoja.Range(fila, 1, fila, columnas).Style.Border.OutsideBorderColor = XLColor.FromHtml(Linea);
        hoja.Row(fila).Height = 15;
    }

    private static (string Banco, string Cuenta, string Cci, string Condicion, string Entrega) DatosPago(
        OrdenCompraDocumento documento) =>
    (
        Vacio(documento.BancoProveedor),
        Vacio(documento.NumeroCuentaProveedor),
        Vacio(documento.CciProveedor),
        CalculoOrdenCompra.NormalizarCondicionPago(documento.CondicionPago),
        string.IsNullOrWhiteSpace(documento.LugarEntrega)
            ? CalculoOrdenCompra.LugarEntrega
            : documento.LugarEntrega.Trim()
    );

    private static void Firmas(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.PaddingTop(12).Row(firmas =>
        {
            firmas.RelativeItem().Column(col =>
            {
                col.Item().Text($"Elaborado por: {Vacio(documento.ElaboradoPor)}").FontSize(9).FontColor(Navy);
                col.Item().PaddingTop(20).Text("Firma: ______________________________").FontSize(8).FontColor(Gris);
            });
            firmas.ConstantItem(40);
            firmas.RelativeItem().Column(col =>
            {
                col.Item().Text($"Revisado por: {Vacio(documento.RevisadoPor)}").FontSize(9).FontColor(Navy);
                col.Item().PaddingTop(20).Text("Firma: ______________________________").FontSize(8).FontColor(Gris);
            });
        });
    }

    private static void Dato(RowDescriptor fila, string etiqueta, string valor)
    {
        fila.RelativeItem().Column(col =>
        {
            col.Item().Text(etiqueta.ToUpperInvariant()).FontSize(6.5f).FontColor(Gris);
            col.Item().Text(valor).FontSize(8).Bold().FontColor(Navy);
        });
    }

    private static void Tabla(IContainer contenedor, OrdenCompraDocumento documento)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(18);
                cols.ConstantColumn(52);
                cols.RelativeColumn(2.8f);
                cols.RelativeColumn(1.2f);
                cols.ConstantColumn(46);
                cols.ConstantColumn(48);
                cols.ConstantColumn(32);
                cols.ConstantColumn(66);
                cols.ConstantColumn(52);
                cols.ConstantColumn(66);
                cols.ConstantColumn(56);
            });

            tabla.Header(h =>
            {
                GrupoPdf(h, 7, "DETALLE DEL INSUMO", Navy);
                GrupoPdf(h, 3, documento.TituloPrecios, AzulPrecios);
                GrupoPdf(h, 1, "ESTADO", Navy);
                foreach (var (texto, indice) in Columnas.Select((texto, indice) => (texto, indice)))
                {
                    h.Cell().Background(indice is >= 7 and <= 9 ? AzulPrecios : Navy).Padding(3)
                        .AlignCenter().AlignMiddle()
                        .Text(texto).FontSize(5.6f).Bold().FontColor(Colors.White);
                }
            });

            for (var i = 0; i < documento.Lineas.Count; i++)
            {
                var linea = documento.Lineas[i];
                var fondo = i % 2 == 0 ? "#FFFFFF" : Fondo;
                void Celda(string texto, bool derecha = false, bool centro = false)
                {
                    var celda = tabla.Cell().Background(fondo).BorderBottom(0.4f).BorderColor(Linea).Padding(2);
                    if (derecha)
                    {
                        celda.AlignRight().AlignMiddle().Text(texto).FontSize(6.6f).FontColor(Navy);
                        return;
                    }

                    if (centro)
                    {
                        celda.AlignCenter().AlignMiddle().Text(texto).FontSize(6.6f).FontColor(Navy);
                        return;
                    }

                    celda.AlignMiddle().Text(texto).FontSize(6.6f).FontColor(Navy);
                }

                Celda((i + 1).ToString(Cultura), centro: true);
                Celda(linea.Codigo);
                Celda(linea.Nombre);
                Celda(linea.Marca);
                Celda(linea.ConsumoPromedio.ToString("0.##", Cultura), derecha: true);
                Celda(linea.CantidadPedir.ToString("0.##", Cultura), derecha: true);
                Celda(linea.Unidad, centro: true);
                if (linea.TienePrecio)
                {
                    Celda(MonedaPrecio.Formato(linea.PrecioUnitarioSinIgv, linea.Moneda), derecha: true);
                    Celda(MonedaPrecio.Formato(linea.Igv, linea.Moneda), derecha: true);
                    Celda(MonedaPrecio.Formato(linea.TotalConIgv, linea.Moneda), derecha: true);
                }
                else
                {
                    Celda("Sin precio", centro: true);
                    Celda("—", centro: true);
                    Celda("—", centro: true);
                }

                Celda(linea.Estado, centro: true);
            }
        });
    }

    private static void TotalesYFletePdf(IContainer contenedor, OrdenCompraDocumento documento)
    {
        var totales = Totales(documento).ToList();
        var estadoFlete = string.IsNullOrWhiteSpace(documento.FleteEstado) ? "No incluido" : documento.FleteEstado.Trim();
        var montoFlete = documento.MontoFlete;

        contenedor.AlignRight().Column(col =>
        {
            col.Spacing(3);
            if (totales.Count == 0)
            {
                col.Item().Text("Subtotal productos: sin precios registrados.").FontSize(8).FontColor(Gris);
            }
            else
            {
                foreach (var total in totales)
                {
                    col.Item().Text(
                            $"Subtotal productos {MonedaPrecio.Simbolo(total.Moneda)}   IGV {MonedaPrecio.Formato(total.Igv, total.Moneda)}    ·    Con IGV {MonedaPrecio.Formato(total.Total, total.Moneda)}")
                        .FontSize(9).Bold().FontColor(Navy);
                }
            }

            col.Item().PaddingTop(6).Text($"Flete ({estadoFlete}): {(montoFlete is double m ? m.ToString("N2", Cultura) : "—")}")
                .FontSize(9).FontColor(Navy);

            var flete = montoFlete ?? 0;
            if (totales.Count == 0)
            {
                col.Item().Text($"Total general (productos + flete): {flete.ToString("N2", Cultura)}")
                    .FontSize(10).Bold().FontColor(Navy);
            }
            else
            {
                foreach (var total in totales)
                {
                    var general = CalculoOrdenCompra.Redondear(total.Total + flete);
                    col.Item().Text(
                            $"Total general {MonedaPrecio.Simbolo(total.Moneda)} (productos + flete): {MonedaPrecio.Formato(general, total.Moneda)}")
                        .FontSize(10).Bold().FontColor(Navy);
                }
            }
        });
    }

    private static void EscribirBloqueFleteExcel(IXLWorksheet hoja, ref int fila, int columnas, OrdenCompraDocumento documento)
    {
        var estado = string.IsNullOrWhiteSpace(documento.FleteEstado) ? "No incluido" : documento.FleteEstado.Trim();
        var monto = documento.MontoFlete;
        var flete = monto ?? 0;

        hoja.Range(fila, 1, fila, columnas).Merge();
        hoja.Cell(fila, 1).Value = $"Flete ({estado}): {(monto is double m ? m.ToString("N2", Cultura) : "—")}";
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 10;
        hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        hoja.Range(fila, 1, fila, columnas).Style.Fill.BackgroundColor = XLColor.FromHtml(Fondo);

        fila++;
        var totales = Totales(documento).ToList();
        if (totales.Count == 0)
        {
            hoja.Range(fila, 1, fila, columnas).Merge();
            hoja.Cell(fila, 1).Value = $"Total general (productos + flete): {flete.ToString("N2", Cultura)}";
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            return;
        }

        foreach (var total in totales)
        {
            hoja.Range(fila, 1, fila, columnas).Merge();
            var general = CalculoOrdenCompra.Redondear(total.Total + flete);
            hoja.Cell(fila, 1).Value =
                $"Total general {MonedaPrecio.Simbolo(total.Moneda)} (productos + flete): {MonedaPrecio.Formato(general, total.Moneda)}";
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            hoja.Cell(fila, 1).Style.Font.FontSize = 11;
            hoja.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            fila++;
        }

        fila--;
    }

    private static void GrupoPdf(TableCellDescriptor celdas, uint span, string texto, string fondo) =>
        celdas.Cell().ColumnSpan(span).Background(fondo).PaddingVertical(3).AlignCenter()
            .Text(texto).FontSize(7).Bold().FontColor(Colors.White);

    private static void Grupo(IXLWorksheet hoja, int fila, int desde, int hasta, string texto, string fondo)
    {
        var rango = hoja.Range(fila, desde, fila, hasta);
        rango.Merge();
        rango.Style.Fill.BackgroundColor = XLColor.FromHtml(fondo);
        rango.Style.Font.Bold = true;
        rango.Style.Font.FontColor = XLColor.White;
        rango.Style.Font.FontSize = 9;
        rango.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Cell(fila, desde).Value = texto;
    }

    private static void Meta(IXLWorksheet hoja, int fila, int desde, int hasta, string etiqueta, string valor)
    {
        var rango = hoja.Range(fila, desde, fila, hasta);
        rango.Merge();
        rango.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        rango.Style.Alignment.WrapText = false;
        hoja.Cell(fila, desde).Value = string.IsNullOrWhiteSpace(valor)
            ? $"{etiqueta}:"
            : $"{etiqueta}: {valor}";
        hoja.Cell(fila, desde).Style.Font.FontSize = 9;
    }

    private static void InsertarLogo(IXLWorksheet hoja)
    {
        var ruta = IdermaMarca.RutaLogo();
        if (ruta is null)
        {
            return;
        }

        using var flujo = new MemoryStream(File.ReadAllBytes(ruta));
        var formato = Path.GetExtension(ruta).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? XLPictureFormat.Png
            : XLPictureFormat.Jpeg;
        hoja.AddPicture(flujo, formato)
            .MoveTo(hoja.Cell(1, 1), 2, 1)
            .WithSize(96, 34);
    }

    private static IEnumerable<(string Moneda, double Igv, double Total)> Totales(OrdenCompraDocumento documento) =>
        documento.Lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => (
                g.Key,
                CalculoOrdenCompra.Redondear(g.Sum(l => l.Igv)),
                CalculoOrdenCompra.Redondear(g.Sum(l => l.TotalConIgv))));

    private static string Vacio(string valor) =>
        string.IsNullOrWhiteSpace(valor) ? "—" : valor.Trim();
}

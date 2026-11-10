using System.Globalization;
using IdermaFichas.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IdermaFichas.Services;

public static class PronosticoExportService
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string Navy = IdermaMarca.Navy;
    private static readonly string Oro = IdermaMarca.Oro;
    private static readonly string Fondo = IdermaMarca.FondoSuave;
    private static readonly string Gris = "#5A6570";

    public static void ExportarPdf(string ruta, ResultadoPronostico resultado)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var emitido = DateTime.Now.ToString("dd/MM/yyyy HH:mm", Cultura);
        var comparacion = resultado.Rango.ResumenComparacion(resultado.Anterior);

        Document
            .Create(contenedor =>
            {
                contenedor.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4.Landscape());
                    pagina.MarginHorizontal(28);
                    pagina.MarginTop(24);
                    pagina.MarginBottom(20);
                    pagina.DefaultTextStyle(x => x.FontFamily("Calibri", "Segoe UI", "Arial").FontSize(8).FontColor(Navy));
                    pagina.Header().Element(Encabezado);
                    pagina.Footer().Element(Pie);
                    pagina.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Text("PRONÓSTICO DE DEMANDA").FontSize(13).Bold().FontColor(Navy);
                        col.Item().Text($"{resultado.Area}  ·  {comparacion}  ·  Emitido el {emitido}")
                            .FontSize(9).FontColor(Gris);
                        col.Item().Text(
                                $"Ingresos {PronosticoService.FormatoCantidad(resultado.TotalIngresos)}" +
                                $"   ·   Salidas {PronosticoService.FormatoCantidad(resultado.TotalSalidas)}" +
                                $"   ·   Merma {PronosticoService.FormatoCantidad(resultado.TotalMerma)}" +
                                $"   ·   Subieron {resultado.Subieron}" +
                                $"   ·   Bajaron {resultado.Bajaron}")
                            .FontSize(9);
                        col.Item().Row(fila =>
                        {
                            fila.RelativeItem().Element(c => GraficaBarras(
                                c,
                                "Productos con más salidas",
                                Top(resultado.Filas, f => f.Salidas)));
                            fila.ConstantItem(16);
                            fila.RelativeItem().Element(c => GraficaBarras(
                                c,
                                "Productos con más merma",
                                Top(resultado.Filas, f => f.Merma),
                                Oro));
                        });
                        col.Item().Element(c => GraficaComparacion(c, resultado));
                        col.Item().PaddingTop(4).Element(c => Tabla(c, resultado.Filas));
                    });
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = "Pronóstico de demanda",
                Author = IdermaMarca.Nombre,
                Creator = IdermaMarca.Sistema,
                Subject = comparacion
            })
            .GeneratePdf(ruta);
    }

    private static void Encabezado(IContainer contenedor)
    {
        contenedor.Column(col =>
        {
            col.Item().Row(fila =>
            {
                var logo = IdermaMarca.RutaLogo();
                if (logo is not null)
                {
                    fila.ConstantItem(80).Height(48).Image(logo).FitArea();
                    fila.ConstantItem(10);
                }

                fila.RelativeItem().Column(texto =>
                {
                    texto.Item().Text(IdermaMarca.NombreMayusculas).FontSize(14).Bold().FontColor(Navy);
                    texto.Item().Text(IdermaMarca.Giro).FontSize(8).FontColor(Oro);
                    texto.Item().Text("Pronóstico de demanda del inventario").FontSize(8).FontColor(Gris);
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
            col.Item().PaddingTop(4).Row(fila =>
            {
                fila.RelativeItem().Text(IdermaMarca.Pie).FontSize(7).FontColor(Gris);
                fila.ConstantItem(90).AlignRight().Text(texto =>
                {
                    texto.Span("Página ").FontSize(7).FontColor(Gris);
                    texto.CurrentPageNumber().FontSize(7).FontColor(Gris);
                    texto.Span(" de ").FontSize(7).FontColor(Gris);
                    texto.TotalPages().FontSize(7).FontColor(Gris);
                });
            });
        });
    }

    private static List<(string Etiqueta, double Valor)> Top(
        IReadOnlyList<FilaPronostico> filas,
        Func<FilaPronostico, double> valor)
    {
        return filas
            .Select(f => (Etiqueta: string.IsNullOrWhiteSpace(f.Nombre) ? f.Codigo : f.Nombre, Valor: valor(f)))
            .Where(b => b.Valor > 0.0001)
            .OrderByDescending(b => b.Valor)
            .Take(10)
            .ToList();
    }

    private static void GraficaBarras(
        IContainer contenedor,
        string titulo,
        IReadOnlyList<(string Etiqueta, double Valor)> barras,
        string color = "")
    {
        if (string.IsNullOrEmpty(color))
        {
            color = Navy;
        }

        var maximo = barras.Count == 0 ? 1 : Math.Max(barras.Max(b => b.Valor), 0.0001);
        contenedor.ShowEntire().Column(col =>
        {
            col.Spacing(4);
            col.Item().Text(titulo).FontSize(11).Bold().FontColor(Navy);
            if (barras.Count == 0)
            {
                col.Item().Text("Sin datos en el rango.").FontSize(8).FontColor(Gris);
                return;
            }

            foreach (var barra in barras)
            {
                var peso = (float)Math.Clamp(barra.Valor / maximo, 0.02, 1);
                var resto = Math.Max(0, 1f - peso);
                col.Item().Row(fila =>
                {
                    fila.ConstantItem(148).AlignMiddle().Text(Cortar(barra.Etiqueta, 26)).FontSize(7.5f);
                    fila.RelativeItem().AlignMiddle().Row(pista =>
                    {
                        pista.RelativeItem(peso).Height(10).Background(color);
                        if (resto > 0.001f)
                        {
                            pista.RelativeItem(resto).Height(10).Background(Fondo);
                        }
                    });
                    fila.ConstantItem(6);
                    fila.ConstantItem(52).AlignMiddle().AlignRight()
                        .Text(PronosticoService.FormatoCantidad(barra.Valor)).FontSize(7.5f);
                });
            }
        });
    }

    private static void GraficaComparacion(IContainer contenedor, ResultadoPronostico resultado)
    {
        (string Nombre, double Actual, double Anterior)[] pares =
        [
            ("Ingresos", resultado.TotalIngresos, resultado.TotalIngresosAnteriores),
            ("Salidas", resultado.TotalSalidas, resultado.TotalSalidasAnteriores),
            ("Merma", resultado.TotalMerma, resultado.TotalMermaAnterior)
        ];
        var maximo = pares.Max(p => Math.Max(p.Actual, p.Anterior));
        if (maximo < 0.0001)
        {
            maximo = 1;
        }

        contenedor.ShowEntire().Column(col =>
        {
            col.Spacing(4);
            col.Item().Text("Rango actual frente al periodo anterior").FontSize(11).Bold().FontColor(Navy);
            col.Item().Row(leyenda =>
            {
                leyenda.ConstantItem(10).Height(10).Background(Navy);
                leyenda.ConstantItem(4);
                leyenda.AutoItem().AlignMiddle().Text("Rango actual").FontSize(8).FontColor(Gris);
                leyenda.ConstantItem(14);
                leyenda.ConstantItem(10).Height(10).Background(Oro);
                leyenda.ConstantItem(4);
                leyenda.AutoItem().AlignMiddle().Text("Periodo anterior").FontSize(8).FontColor(Gris);
            });

            foreach (var par in pares)
            {
                col.Item().PaddingTop(2).Row(fila =>
                {
                    fila.ConstantItem(70).AlignMiddle().Text(par.Nombre).FontSize(8).Bold();
                    fila.RelativeItem().Column(barras =>
                    {
                        barras.Spacing(2);
                        barras.Item().Element(c => BarraComparada(c, par.Actual, maximo, Navy));
                        barras.Item().Element(c => BarraComparada(c, par.Anterior, maximo, Oro));
                    });
                    fila.ConstantItem(8);
                    fila.ConstantItem(110).AlignMiddle().AlignRight().Text(
                        $"{PronosticoService.FormatoCantidad(par.Actual)}  /  {PronosticoService.FormatoCantidad(par.Anterior)}")
                        .FontSize(7.5f);
                });
            }
        });
    }

    private static void BarraComparada(IContainer contenedor, double valor, double maximo, string color)
    {
        var peso = valor <= 0.0001 ? 0.02f : (float)Math.Clamp(valor / maximo, 0.02, 1);
        var resto = Math.Max(0, 1f - peso);
        contenedor.Row(pista =>
        {
            pista.RelativeItem(peso).Height(8).Background(color);
            if (resto > 0.001f)
            {
                pista.RelativeItem(resto).Height(8).Background(Fondo);
            }
        });
    }

    private static void Tabla(IContainer contenedor, IReadOnlyList<FilaPronostico> filas)
    {
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(52);
                cols.RelativeColumn(2.2f);
                cols.ConstantColumn(46);
                cols.ConstantColumn(52);
                cols.ConstantColumn(52);
                cols.ConstantColumn(48);
                cols.ConstantColumn(52);
                cols.ConstantColumn(52);
                cols.ConstantColumn(48);
                cols.ConstantColumn(58);
            });

            tabla.Header(h =>
            {
                foreach (var titulo in new[]
                         {
                             "Código", "Producto", "Unidad", "Ingresos", "Salidas", "Merma",
                             "Anterior", "Variación", "Tendencia", "Pronóstico"
                         })
                {
                    h.Cell().Background(Navy).Padding(3)
                        .Text(titulo).FontSize(7).Bold().FontColor(Colors.White);
                }
            });

            for (var i = 0; i < filas.Count; i++)
            {
                var fila = filas[i];
                var fondo = i % 2 == 0 ? Fondo : "#FFFFFF";
                Celda(tabla, string.IsNullOrWhiteSpace(fila.Codigo) ? "—" : fila.Codigo, fondo);
                Celda(tabla, fila.Nombre, fondo);
                Celda(tabla, string.IsNullOrWhiteSpace(fila.Unidad) ? "—" : fila.Unidad, fondo);
                Celda(tabla, PronosticoService.FormatoCantidad(fila.Ingresos), fondo);
                Celda(tabla, PronosticoService.FormatoCantidad(fila.Salidas), fondo);
                Celda(tabla, PronosticoService.FormatoCantidad(fila.Merma), fondo);
                Celda(tabla, PronosticoService.FormatoCantidad(fila.DemandaAnterior), fondo);
                Celda(tabla, PronosticoService.FormatoVariacion(fila.Variacion), fondo);
                Celda(tabla, fila.Tendencia, fondo);
                Celda(tabla, PronosticoService.FormatoCantidad(fila.Pronostico), fondo);
            }
        });
    }

    private static void Celda(TableDescriptor tabla, string texto, string fondo)
    {
        tabla.Cell().Background(fondo).BorderBottom(0.4f).BorderColor("#E4E0D8").Padding(3)
            .Text(texto).FontSize(7);
    }

    private static string Cortar(string texto, int maximo)
    {
        var limpio = string.IsNullOrWhiteSpace(texto) ? "—" : texto.Trim();
        return limpio.Length <= maximo ? limpio : $"{limpio[..(maximo - 1)]}…";
    }
}

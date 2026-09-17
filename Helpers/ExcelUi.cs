using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace IdermaFichas.Helpers;

public sealed record ResultadoExportacion(StorageFile Archivo, string Extension)
{
    public string Ruta => Archivo.Path;
}

public static class ExcelUi
{
    public static async Task<ResultadoImportacion?> ImportarAsync(Guid? area = null)
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".xlsx");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        await using (var origen = await archivo.OpenStreamForReadAsync())
        await using (var destino = File.Create(temporal))
        {
            await origen.CopyToAsync(destino);
        }

        try
        {
            var fichas = ExcelFichaService.Leer(temporal, area);
            return App.Instance.Repositorio.ImportarFichas(fichas, area);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    public static Task<ResultadoExportacion?> GuardarPdfAsync(string nombreSugerido, Action<string> escribir) =>
        GuardarDocumentoAsync(nombreSugerido, (ruta, _) => escribir(ruta), soloPdf: true);

    public static async Task<StorageFile?> ElegirGuardarPdfAsync(string nombreSugerido)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("PDF", [".pdf"]);
        selector.SuggestedFileName = nombreSugerido;
        return await selector.PickSaveFileAsync();
    }

    public static async Task<ResultadoExportacion> CopiarPdfAsync(string rutaTemporal, StorageFile archivo)
    {
        try
        {
            await CopiarArchivoAsync(rutaTemporal, archivo);
            return new ResultadoExportacion(archivo, ".pdf");
        }
        finally
        {
            if (File.Exists(rutaTemporal))
            {
                File.Delete(rutaTemporal);
            }
        }
    }

    public static Task<ResultadoExportacion?> GuardarExcelAsync(string nombreSugerido, Action<string> escribir) =>
        GuardarDocumentoAsync(nombreSugerido, (ruta, _) => escribir(ruta), soloExcel: true);

    public static async Task<string?> ElegirExcelTemporalAsync()
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".xlsx");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        await using (var origen = await archivo.OpenStreamForReadAsync())
        await using (var destino = File.Create(temporal))
        {
            await origen.CopyToAsync(destino);
        }

        return temporal;
    }

    public static async Task<bool> GuardarPlantillaAsync() =>
        await GuardarExcelAsync("plantilla-fichas-iderma", ExcelFichaService.CrearPlantilla) is not null;

    public static Task<ResultadoExportacion?> ExportarFichaAsync(FichaTecnica ficha) =>
        GuardarDocumentoAsync(
            $"ficha-{Sanitizar(ficha.Codigo)}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    PdfFichaService.ExportarIndividual(ruta, ficha);
                }
                else
                {
                    ExcelFichaService.ExportarIndividual(ruta, ficha);
                }
            });

    public static Task<ResultadoExportacion?> ExportarHistorialPreciosAsync(
        FichaTecnica ficha,
        IReadOnlyList<PrecioProducto> precios) =>
        GuardarDocumentoAsync(
            $"historial-precios-{Sanitizar(ficha.Codigo)}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    HistorialPreciosExportService.ExportarPdf(ruta, ficha, precios);
                }
                else
                {
                    HistorialPreciosExportService.ExportarExcel(ruta, ficha, precios);
                }
            });

    public static Task<ResultadoExportacion?> ExportarIngresosAsync(
        string nombreSugerido,
        IReadOnlyList<LineaRegistroIngreso> lineas,
        string periodo) =>
        GuardarDocumentoAsync(
            nombreSugerido,
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    RegistroIngresosExportService.ExportarPdf(ruta, lineas, periodo);
                }
                else
                {
                    RegistroIngresosExportService.ExportarExcel(ruta, lineas, periodo);
                }
            });

    public static Task<ResultadoExportacion?> ExportarConsolidadoAsync(IReadOnlyList<FichaTecnica> fichas) =>
        GuardarDocumentoAsync(
            $"consolidado-fichas-{DateTime.Now:yyyyMMdd}",
            (ruta, extension) =>
            {
                if (EsPdf(extension))
                {
                    PdfFichaService.ExportarConsolidado(ruta, fichas);
                }
                else
                {
                    ExcelFichaService.ExportarConsolidado(ruta, fichas);
                }
            });

    public static async Task OfrecerAbrirAsync(XamlRoot? raiz, ResultadoExportacion resultado)
    {
        if (raiz is not null)
        {
            var ubicacion = string.IsNullOrWhiteSpace(resultado.Ruta)
                ? $"Se guardó «{resultado.Archivo.Name}»."
                : $"Se guardó en:\n{resultado.Ruta}";
            var dialogo = new ContentDialog
            {
                Title = "Archivo generado",
                Content = $"{ubicacion}\n\n¿Desea abrirlo ahora?",
                PrimaryButtonText = "Abrir",
                CloseButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = raiz
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        if (!await Launcher.LaunchFileAsync(resultado.Archivo)
            && !string.IsNullOrWhiteSpace(resultado.Ruta))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = resultado.Ruta,
                UseShellExecute = true
            });
        }
    }

    private static async Task<ResultadoExportacion?> GuardarDocumentoAsync(
        string nombreSugerido,
        Action<string, string> escribir,
        bool soloPdf = false,
        bool soloExcel = false)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        if (soloExcel)
        {
            selector.FileTypeChoices.Add("Excel", [".xlsx"]);
        }
        else if (soloPdf)
        {
            selector.FileTypeChoices.Add("PDF", [".pdf"]);
        }
        else
        {
            selector.FileTypeChoices.Add("PDF", [".pdf"]);
            selector.FileTypeChoices.Add("Excel", [".xlsx"]);
        }

        selector.SuggestedFileName = nombreSugerido;

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return null;
        }

        var extension = archivo.FileType;
        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        try
        {
            await Task.Run(() => escribir(temporal, extension));
            await CopiarArchivoAsync(temporal, archivo);
            return new ResultadoExportacion(archivo, extension);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                try
                {
                    File.Delete(temporal);
                }
                catch
                {
                }
            }
        }
    }

    public static async Task CopiarArchivoAsync(string temporal, StorageFile archivo)
    {
        var aplazado = false;
        try
        {
            CachedFileManager.DeferUpdates(archivo);
            aplazado = true;
        }
        catch
        {
        }

        try
        {
            await using var origen = File.OpenRead(temporal);
            await using var destino = await archivo.OpenStreamForWriteAsync();
            destino.SetLength(0);
            await origen.CopyToAsync(destino);
        }
        finally
        {
            if (aplazado)
            {
                await CachedFileManager.CompleteUpdatesAsync(archivo);
            }
        }
    }

    private static bool EsPdf(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string Sanitizar(string valor)
    {
        var limpio = new string(valor.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "ficha" : limpio;
    }
}

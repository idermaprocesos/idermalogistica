using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class RegistroOrdenesWindow : Page
{
    private bool _exportando;

    public RegistroOrdenesWindow()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Instance.OrdenesCompra.Cambio -= AlCambiar;
        App.Instance.OrdenesCompra.Cambio += AlCambiar;
        Refrescar();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) =>
        App.Instance.OrdenesCompra.Cambio -= AlCambiar;

    private void AlCambiar(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Refrescar);

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    public void Refrescar()
    {
        var items = App.Instance.OrdenesCompra.Listar().Select(o => new OrdenRegistradaItem(o)).ToList();
        Lista.ItemsSource = items;
        var hay = items.Count > 0;
        Lista.Visibility = hay ? Visibility.Visible : Visibility.Collapsed;
        TextoVacio.Visibility = hay ? Visibility.Collapsed : Visibility.Visible;
        Subtitulo.Text = hay
            ? $"{items.Count} orden(es) guardada(s). Genere la orden de entrada o descargue de nuevo el PDF/Excel."
            : "Las órdenes exportadas quedan guardadas aquí. Puede generar la orden de entrada, cargarla al registro o volver a descargar PDF/Excel.";
    }

    private async void OrdenEntrada_Click(object sender, RoutedEventArgs e) =>
        await GenerarOrdenEntradaAsync(sender, cargarAlSistema: false, a5: false);

    private async void OrdenEntradaA5_Click(object sender, RoutedEventArgs e) =>
        await GenerarOrdenEntradaAsync(sender, cargarAlSistema: false, a5: true);

    private async void OrdenEntradaYCargar_Click(object sender, RoutedEventArgs e) =>
        await GenerarOrdenEntradaAsync(sender, cargarAlSistema: true, a5: false);

    private async void DescargarPdf_Click(object sender, RoutedEventArgs e) =>
        await DescargarAsync(sender, pdf: true);

    private async void DescargarExcel_Click(object sender, RoutedEventArgs e) =>
        await DescargarAsync(sender, pdf: false);

    private async void Quitar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.OrdenesCompra.Obtener(id) is not { } orden)
        {
            return;
        }

        var mes = string.IsNullOrWhiteSpace(orden.Documento.MesAnio) ? "esta orden" : $"la orden de {orden.Documento.MesAnio}";
        var dialogo = new ContentDialog
        {
            Title = "Quitar orden",
            Content = $"¿Quitar {mes} del registro? El archivo que ya haya guardado en su equipo no se borra.",
            PrimaryButtonText = "Quitar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Instance.OrdenesCompra.Eliminar(id);
        Refrescar();
    }

    private async Task GenerarOrdenEntradaAsync(object sender, bool cargarAlSistema, bool a5)
    {
        if (_exportando || !TryId(sender, out var id) || App.Instance.OrdenesCompra.Obtener(id) is not { } orden)
        {
            return;
        }

        if (cargarAlSistema)
        {
            var yaCargada = IngresosOrdenCompra.YaCargada(orden);
            if (yaCargada)
            {
                var soloPdf = new ContentDialog
                {
                    Title = "Orden ya cargada",
                    Content =
                        "Esta orden ya tiene ingresos en Registro. " +
                        "Para no duplicar inventario solo se generará el PDF, sin volver a sumar cantidades.",
                    PrimaryButtonText = "Solo PDF",
                    CloseButtonText = "Cancelar",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                if (await soloPdf.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                cargarAlSistema = false;
            }
            else
            {
                var dialogo = new ContentDialog
                {
                    Title = "Generar orden y cargar",
                    Content =
                        "Se generará el PDF de la orden de entrada y se cargará en Registro el ingreso de cada producto con su cantidad. ¿Continuar?",
                    PrimaryButtonText = "Generar y cargar",
                    CloseButtonText = "Cancelar",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };
                if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }
        }

        _exportando = true;
        try
        {
            string? mensajeCarga = null;
            if (cargarAlSistema)
            {
                mensajeCarga = IngresosOrdenCompra.Registrar(orden);
            }

            var folio = IngresosOrdenCompra.NumeroDocumento(orden);
            var tamano = a5 ? "A5" : "A4";
            var resultado = await ExcelUi.GuardarPdfAsync(
                $"orden-entrada-{tamano.ToLowerInvariant()}-{folio}-{DateTime.Now:yyyyMMdd}",
                ruta => OrdenEntradaExportService.ExportarPdf(ruta, orden, a5));

            if (resultado is null)
            {
                if (cargarAlSistema && mensajeCarga is not null)
                {
                    BarraAviso.Severity = InfoBarSeverity.Success;
                    BarraAviso.Message = mensajeCarga + " El PDF no se guardó (cancelado).";
                    BarraAviso.IsOpen = true;
                }

                return;
            }

            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = cargarAlSistema && mensajeCarga is not null
                ? $"{mensajeCarga} También se generó el PDF {tamano} de la orden de entrada."
                : $"Se generó el PDF {tamano} de la orden de entrada.";
            BarraAviso.IsOpen = true;
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            BarraAviso.Severity = InfoBarSeverity.Error;
            BarraAviso.Message = cargarAlSistema
                ? $"No se pudo generar/cargar: {ex.Message}"
                : $"No se pudo generar la orden de entrada: {ex.Message}";
            BarraAviso.IsOpen = true;
        }
        finally
        {
            _exportando = false;
        }
    }

    private async Task DescargarAsync(object sender, bool pdf)
    {
        if (_exportando || !TryId(sender, out var id) || App.Instance.OrdenesCompra.Obtener(id) is not { } orden)
        {
            return;
        }

        _exportando = true;
        try
        {
            var resultado = await ExcelUi.ExportarOrdenCompraAsync(orden.Documento, pdf);
            if (resultado is null)
            {
                return;
            }

            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = pdf ? "Se descargó el PDF de la orden." : "Se descargó el Excel de la orden.";
            BarraAviso.IsOpen = true;
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            BarraAviso.Severity = InfoBarSeverity.Error;
            BarraAviso.Message = $"No se pudo descargar: {ex.Message}";
            BarraAviso.IsOpen = true;
        }
        finally
        {
            _exportando = false;
        }
    }

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: string texto } && Guid.TryParse(texto, out id);
    }
}

public sealed class OrdenRegistradaItem
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");

    public OrdenRegistradaItem(OrdenCompraRegistrada orden)
    {
        IdTexto = orden.Id.ToString();
        var doc = orden.Documento ?? new OrdenCompraDocumento();
        var mes = string.IsNullOrWhiteSpace(doc.MesAnio) ? "sin mes" : doc.MesAnio.Trim();
        Titulo = string.IsNullOrWhiteSpace(doc.Correlativo)
            ? $"Orden {mes}"
            : $"{doc.Correlativo.Trim()} · {mes}";
        var productos = doc.Lineas.Count == 1 ? "1 producto" : $"{doc.Lineas.Count} productos";
        Resumen = $"{orden.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura)} · {productos} · {doc.Estado} · {Total(doc)}";
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(doc.ElaboradoPor))
        {
            partes.Add($"Elaboró {doc.ElaboradoPor.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(doc.RevisadoPor))
        {
            partes.Add($"Revisó {doc.RevisadoPor.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(doc.PeriodoCobertura))
        {
            partes.Add(doc.PeriodoCobertura.Trim());
        }

        Extra = partes.Count == 0 ? "Sin datos de elaboración" : string.Join(" · ", partes);
    }

    public string IdTexto { get; }
    public string Titulo { get; }
    public string Resumen { get; }
    public string Extra { get; }

    private static string Total(OrdenCompraDocumento doc)
    {
        var grupos = doc.Lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => MonedaPrecio.Formato(g.Sum(l => l.TotalConIgv), g.Key))
            .ToList();
        return grupos.Count == 0 ? "sin total" : string.Join(" · ", grupos);
    }
}

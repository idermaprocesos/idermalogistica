using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class NotificacionesPage : Page, IVistaInventario
{
    private readonly List<string> _estados = ["Todas", "Pendiente", "Aceptada", "Denegada"];

    public NotificacionesPage()
    {
        InitializeComponent();
        ComboEstado.ItemsSource = _estados;
        ComboEstado.SelectedIndex = 0;
        SolicitudesClinicaNube.Cambiado += Refrescar;
        Loaded += (_, _) =>
        {
            SolicitudesClinicaNube.Iniciar(DispatcherQueue);
            Refrescar();
        };
        Unloaded += (_, _) => SolicitudesClinicaNube.Cambiado -= Refrescar;
    }

    public void AplicarCambioInventario() => Refrescar();

    private void Refrescar()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(Refrescar);
            return;
        }

        var filtro = CajaBusqueda.Text?.Trim() ?? string.Empty;
        var estado = ComboEstado.SelectedItem as string ?? "Todas";
        var vista = SolicitudesClinicaNube.Items
            .Where(s => estado == "Todas" || s.EstadoTexto.Equals(estado, StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(filtro) ||
                        Contiene(s.Folio, filtro) ||
                        Contiene(s.Enfermera, filtro) ||
                        Contiene(s.Detalle, filtro) ||
                        Contiene(s.MotivoDenegacion, filtro))
            .ToList();

        // Forzar redibujado: x:Bind no siempre refresca al mutar el mismo objeto.
        ListaNotificaciones.ItemsSource = null;
        ListaNotificaciones.ItemsSource = vista;
        TextoVacio.Visibility = vista.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var pendientes = SolicitudesClinicaNube.Pendientes;
        TextoConteo.Text = pendientes == 0
            ? $"{SolicitudesClinicaNube.Items.Count} notificaciones · {SolicitudesClinicaNube.Estado}"
            : $"{SolicitudesClinicaNube.Items.Count} notificaciones · {pendientes} pendientes · {SolicitudesClinicaNube.Estado}";
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e) => Refrescar();

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e) => Refrescar();

    private async void Aceptar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SolicitudClinica solicitud })
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Aceptar solicitud",
            Content = $"¿Aceptar {solicitud.Folio} de {solicitud.Enfermera}? Se registrará la salida de los productos en Registro.",
            PrimaryButtonText = "Aceptar y registrar salida",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var resumen = await SolicitudesClinicaNube.AceptarAsync(solicitud);
            Refrescar();
            Mostrar("Solicitud aceptada. " + resumen, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void Denegar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SolicitudClinica solicitud })
        {
            return;
        }

        var motivo = new TextBox
        {
            PlaceholderText = "Escribe el motivo de la denegación",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 100,
            MaxLength = 500
        };

        var dialogo = new ContentDialog
        {
            Title = $"Denegar {solicitud.Folio}",
            Content = motivo,
            PrimaryButtonText = "Denegar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await SolicitudesClinicaNube.DenegarAsync(solicitud, motivo.Text);
            Refrescar();
            Mostrar("Solicitud denegada.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void ExportarExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(soloExcel: true);

    private async void ExportarPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(soloPdf: true);

    private async Task ExportarAsync(bool soloExcel = false, bool soloPdf = false)
    {
        var lista = (ListaNotificaciones.ItemsSource as IEnumerable<SolicitudClinica>)?.ToList()
            ?? SolicitudesClinicaNube.Items.ToList();
        if (lista.Count == 0)
        {
            Mostrar("No hay notificaciones para exportar.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ResultadoExportacion? resultado;
            if (soloExcel)
            {
                resultado = await ExcelUi.GuardarExcelAsync(
                    $"notificaciones-clinica-{DateTime.Now:yyyyMMdd}",
                    ruta => SolicitudesExportService.ExportarExcel(ruta, lista));
            }
            else if (soloPdf)
            {
                resultado = await ExcelUi.GuardarPdfAsync(
                    $"notificaciones-clinica-{DateTime.Now:yyyyMMdd}",
                    ruta => SolicitudesExportService.ExportarPdf(ruta, lista));
            }
            else
            {
                return;
            }

            if (resultado is not null)
            {
                await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
            }
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo exportar: " + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Title = severidad == InfoBarSeverity.Error ? "Error" : "Notificaciones";
        BarraEstado.Message = mensaje;
        BarraEstado.Severity = severidad;
        BarraEstado.IsOpen = true;
    }

    private static bool Contiene(string? texto, string filtro) =>
        !string.IsNullOrWhiteSpace(texto) &&
        texto.Contains(filtro, StringComparison.CurrentCultureIgnoreCase);
}

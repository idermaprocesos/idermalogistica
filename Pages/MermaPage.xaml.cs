using System.Globalization;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class MermaPage : Page
{
    public MermaPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FechaDesde.Date ??= DateTimeOffset.Now;
            FechaHasta.Date ??= DateTimeOffset.Now;
            ModulosNube.DatosCambiados -= AlLlegarNube;
            ModulosNube.DatosCambiados += AlLlegarNube;
            Cargar();
        };
        Unloaded += (_, _) => ModulosNube.DatosCambiados -= AlLlegarNube;
    }

    private void AlLlegarNube() => DispatcherQueue.TryEnqueue(Cargar);

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var personalizado = Periodo() == "Personalizado";
        FechaDesde.Visibility = personalizado ? Visibility.Visible : Visibility.Collapsed;
        FechaHasta.Visibility = personalizado ? Visibility.Visible : Visibility.Collapsed;
        Cargar();
    }

    private void FechaCustom_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (IsLoaded && Periodo() == "Personalizado")
        {
            Cargar();
        }
    }

    private async void Imprimir_Click(object sender, RoutedEventArgs e)
    {
        var rango = RangoActual();
        var lineas = Filtrar(rango);
        if (lineas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin movimientos";
            Aviso.Message = $"No hay merma en {rango.Titulo.ToLowerInvariant()}.";
            Aviso.IsOpen = true;
            return;
        }

        var nombre = $"Reporte de merma {rango.Titulo}".Replace('/', '-');
        var resultado = await ExcelUi.GuardarPdfAsync(nombre, ruta => MermaReporte.ExportarPdf(ruta, rango.Titulo, lineas));
        if (resultado is not null)
        {
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
    }

    private void Cargar()
    {
        if (ComboPeriodo is null)
        {
            return;
        }

        var rango = RangoActual();
        var items = Filtrar(rango).Select(m => new MermaItem(m)).ToList();
        Lista.ItemsSource = items;
        Vacio.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Vacio.Text = $"No hay merma en {rango.Titulo.ToLowerInvariant()}.";
        TextoRango.Text = rango.Titulo;
    }

    private List<MermaRegistrada> Filtrar(RangoMerma rango) =>
        App.Instance.Mermas.Listar().Where(m => rango.Contiene(m.Fecha)).ToList();

    private RangoMerma RangoActual() =>
        RangoMerma.De(Periodo(), FechaDesde?.Date, FechaHasta?.Date);

    private string Periodo() =>
        (ComboPeriodo.SelectedItem as ComboBoxItem)?.Content as string ?? "Hoy";

    private async void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Mermas.Obtener(id) is not MermaRegistrada merma)
        {
            return;
        }

        var cultura = CultureInfo.GetCultureInfo("es-PE");
        var cantidad = new NumberBox
        {
            Header = "Cantidad",
            Minimum = 0,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = merma.Cantidad,
            Width = 220
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = $"{merma.Codigo}  {merma.Nombre}".Trim(),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Opacity = 0.75,
            Text = $"Cantidad actual: {merma.Cantidad.ToString("0.##", cultura)} {merma.Unidad}".Trim(),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(cantidad);

        var dialogo = new ContentDialog
        {
            Title = "Editar merma",
            Content = panel,
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!DescuentoMerma.CambiarCantidad(id, cantidad.Value, out var error))
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "No se cambió la cantidad";
            Aviso.Message = error ?? "No se pudo actualizar la merma.";
            Aviso.IsOpen = true;
            return;
        }

        Cargar();
        Aviso.Severity = InfoBarSeverity.Success;
        Aviso.Title = "Cantidad actualizada";
        Aviso.Message = "La existencia y el registro quedaron con la nueva cantidad.";
        Aviso.IsOpen = true;
    }

    private async void Revertir_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Mermas.Obtener(id) is not MermaRegistrada merma)
        {
            return;
        }

        var cultura = CultureInfo.GetCultureInfo("es-PE");
        var dialogo = new ContentDialog
        {
            Title = "Revertir merma",
            Content = $"Se devuelven {merma.Cantidad.ToString("0.##", cultura)} {merma.Unidad} de {merma.Nombre} al inventario y se quita esta merma del registro.".Trim(),
            PrimaryButtonText = "Revertir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!DescuentoMerma.Revertir(id, out var error))
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "No se revirtió";
            Aviso.Message = error ?? "No se pudo devolver el producto al inventario.";
            Aviso.IsOpen = true;
            return;
        }

        Cargar();
        Aviso.Severity = InfoBarSeverity.Success;
        Aviso.Title = "Merma revertida";
        Aviso.Message = "La cantidad volvió al inventario y se quitó la salida del registro.";
        Aviso.IsOpen = true;
    }

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is FrameworkElement elemento
            && elemento.Tag is string texto
            && Guid.TryParse(texto, out id);
    }
}

public sealed class MermaItem
{
    public MermaItem(MermaRegistrada merma)
    {
        var cultura = CultureInfo.GetCultureInfo("es-PE");
        Id = merma.Id.ToString("D");
        Titulo = $"{merma.Codigo}  {merma.Nombre} · {merma.Cantidad.ToString("0.##", cultura)} {merma.Unidad}".Trim();
        var caduca = merma.FechaCaducidad?.ToLocalTime().ToString("dd/MM/yyyy") ?? "Sin caducidad";
        var lote = string.IsNullOrWhiteSpace(merma.Lote) ? "Sin lote" : merma.Lote;
        Lote = $"{lote} · {caduca}";
        Motivo = string.IsNullOrWhiteSpace(merma.Detalle) ? merma.Motivo : $"{merma.Motivo} · {merma.Detalle}";
        Cuando = merma.Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public string Id { get; }
    public string Titulo { get; }
    public string Lote { get; }
    public string Motivo { get; }
    public string Cuando { get; }
}

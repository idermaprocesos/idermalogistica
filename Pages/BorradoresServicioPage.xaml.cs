using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IdermaFichas.Pages;

public sealed partial class BorradoresServicioPage : Page
{
    public BorradoresServicioPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Instance.OrdenesServicio.Cambio -= AlCambiar;
        App.Instance.OrdenesServicio.Cambio += AlCambiar;
        Cargar();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) =>
        App.Instance.OrdenesServicio.Cambio -= AlCambiar;

    private void AlCambiar(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(Cargar);

    private void Cargar()
    {
        var items = App.Instance.OrdenesServicio.Listar()
            .Where(o => string.Equals(o.Documento.Estado, "Borrador", StringComparison.OrdinalIgnoreCase))
            .Select(o => new OrdenServicioItem(o))
            .ToList();
        Lista.ItemsSource = items;
        Vacio.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button boton || boton.Tag is not string texto || !Guid.TryParse(texto, out var id))
        {
            return;
        }

        var orden = App.Instance.OrdenesServicio.Obtener(id);
        if (orden is null)
        {
            return;
        }

        Frame.Navigate(typeof(OrdenServicioWindow), new OrdenServicioEdicion
        {
            Id = orden.Id,
            FechaRegistro = orden.FechaRegistro,
            AmpliacionAplicada = orden.AmpliacionAplicada,
            Documento = orden.Documento,
            Lineas = orden.Documento.Lineas.Select(OrdenesServicioRepository.DesdeGuardada).ToList()
        });
    }
}

public sealed class OrdenServicioItem
{
    public OrdenServicioItem(OrdenServicioRegistrada orden)
    {
        IdTexto = orden.Id.ToString();
        var doc = orden.Documento;
        Titulo = $"{doc.MesAnio} · {doc.Estado}";
        Resumen = $"{doc.Lineas.Count} contrato(s) · {doc.FechaElaboracion} · {CalculoOrdenServicio.TotalesPorMoneda(doc.Lineas.Select(l => (l.Moneda, l.TotalConIgv)))}";
    }

    public string IdTexto { get; }
    public string Titulo { get; }
    public string Resumen { get; }
}

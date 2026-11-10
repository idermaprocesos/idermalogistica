using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class HomePage : Page, IVistaInventario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) => AplicarCambioInventario();

    public void AplicarCambioInventario()
    {
        var repo = App.Instance.Repositorio;
        var todas = repo.Todas;
        var total = todas.Count;
        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();
        var stockBajo = todas.Count(f => f.EstaStockCritico);
        var actualizadosHoy = todas.Count(f => f.FechaActualizacion.Date == DateTime.Today);

        KpiTotal.Text = total.ToString("N0", Cultura);
        KpiVencidos.Text = vencidos.ToString("N0", Cultura);
        KpiProximos.Text = proximos.ToString("N0", Cultura);
        KpiStockBajo.Text = stockBajo.ToString("N0", Cultura);

        DetalleVencidos.Text = vencidos == 0 && proximos == 0
            ? "No hay materiales vencidos ni próximos a caducar en 30 días."
            : $"{vencidos} lote(s) vencido(s) y {proximos} por vencer. Priorice reposición y baja de lote.";

        DetalleActividad.Text = actualizadosHoy == 0
            ? "Hoy no se han actualizado fichas. El catálogo permanece estable."
            : $"{actualizadosHoy} ficha(s) se actualizaron hoy.";
    }

    private void AbrirCaducidad_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad();

    private void AbrirVencidos_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad(new CaducidadNavegacion());

    private void AbrirProximos_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACaducidad(new CaducidadNavegacion(Proximos: true));

    private void AbrirCatalogo_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACatalogo();

    private void AbrirStockBajo_Click(object sender, RoutedEventArgs e) =>
        App.Instance.MainAppWindow?.IrACatalogo(new CatalogoNavegacion(Stock: FiltroStockCatalogo.Critico));
}

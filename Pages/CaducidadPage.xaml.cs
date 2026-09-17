using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class CaducidadPage : Page
{
    private static readonly int[] Horizontes = [30, 60, 90];
    private Guid? _fichaNavegada;
    private bool _enfocarProximos;
    private bool _ajustandoHorizonte;

    public CaducidadPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _fichaNavegada = e.Parameter switch
        {
            CaducidadNavegacion nav => nav.FichaId,
            Guid id => id,
            _ => null
        };
        _enfocarProximos = e.Parameter is CaducidadNavegacion { Proximos: true };
        ComboHorizonte.ItemsSource = new[]
        {
            "Próximos 30 días",
            "Próximos 60 días",
            "Próximos 90 días"
        };
        _ajustandoHorizonte = true;
        ComboHorizonte.SelectedIndex = IndiceHorizonte(_fichaNavegada);
        _ajustandoHorizonte = false;
        Refrescar();
    }

    private void ComboHorizonte_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ajustandoHorizonte)
        {
            return;
        }

        Refrescar();
    }

    private void Refrescar()
    {
        var dias = ComboHorizonte.SelectedIndex is >= 0 and < 3
            ? Horizontes[ComboHorizonte.SelectedIndex]
            : 30;

        var repo = App.Instance.Repositorio;
        var vencidos = repo.PartidasVencidas().ToList();
        var proximos = repo.PartidasProximas(dias).ToList();

        ListaVencidos.ItemsSource = vencidos;
        ListaProximos.ItemsSource = proximos;
        ConteoVencidos.Text = vencidos.Count.ToString();
        ConteoProximos.Text = proximos.Count.ToString();
        DetalleHorizonte.Text = $"Caducan en los próximos {dias} días.";
        ResaltarFicha(vencidos, proximos);
        if (_enfocarProximos && proximos.Count > 0)
        {
            ListaProximos.ScrollIntoView(proximos[0]);
        }
    }

    private void ResaltarFicha(List<AlertaCaducidad> vencidos, List<AlertaCaducidad> proximos)
    {
        if (_fichaNavegada is not Guid id)
        {
            return;
        }

        var vencido = vencidos.FirstOrDefault(a => a.Ficha.Id == id);
        if (vencido is not null)
        {
            ListaVencidos.ScrollIntoView(vencido);
            return;
        }

        var proximo = proximos.FirstOrDefault(a => a.Ficha.Id == id);
        if (proximo is not null)
        {
            ListaProximos.ScrollIntoView(proximo);
        }
    }

    private static int IndiceHorizonte(Guid? fichaId)
    {
        if (fichaId is not Guid id)
        {
            return 0;
        }

        var repo = App.Instance.Repositorio;
        if (repo.PartidasVencidas().Any(a => a.Ficha.Id == id) ||
            repo.PartidasProximas(30).Any(a => a.Ficha.Id == id))
        {
            return 0;
        }

        if (repo.PartidasProximas(60).Any(a => a.Ficha.Id == id))
        {
            return 1;
        }

        return 2;
    }

    private void Lista_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AlertaCaducidad alerta)
        {
            App.Instance.MainAppWindow?.IrAArea(alerta.Ficha.Area, alerta.Ficha.Id);
        }
    }
}

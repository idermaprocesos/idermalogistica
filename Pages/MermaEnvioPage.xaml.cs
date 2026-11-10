using System.Collections.ObjectModel;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class MermaEnvioPage : Page
{
    private readonly ObservableCollection<LineaMermaEnvio> _lineas = [];

    public MermaEnvioPage()
    {
        InitializeComponent();
        Lista.ItemsSource = _lineas;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        _lineas.Clear();
        if (e.Parameter is not IEnumerable<Guid> ids)
        {
            return;
        }

        foreach (var id in ids.Distinct())
        {
            var ficha = App.Instance.Repositorio.Obtener(id);
            if (ficha is null || ficha.Existencia <= 0.0001)
            {
                continue;
            }

            var proximo = PartidasInventario.PreferidaParaSalida(ficha);
            var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "und" : ficha.UnidadMedida.Trim();
            _lineas.Add(new LineaMermaEnvio
            {
                FichaId = ficha.Id,
                Codigo = ficha.Codigo,
                Nombre = ficha.Nombre,
                Unidad = unidad,
                Lote = proximo?.LoteTexto ?? "Sin lote",
                Caducidad = proximo?.FechaCaducidadTexto ?? "Sin caducidad",
                Stock = ficha.Existencia,
                Cantidad = proximo is null ? ficha.Existencia : Math.Min(proximo.Cantidad, ficha.Existencia),
                Motivo = MotivosMerma.Sugerir(proximo)
            });
        }
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void Enviar_Click(object sender, RoutedEventArgs e)
    {
        var elegidas = _lineas.Where(l => l.Cantidad > 0.0001).ToList();
        if (elegidas.Count == 0)
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin cantidad";
            Aviso.Message = "Indique una cantidad mayor a cero.";
            Aviso.IsOpen = true;
            return;
        }

        var solicitudes = elegidas
            .Select(l => new SolicitudMerma(l.FichaId, l.Cantidad, l.Motivo, l.Detalle, l.Unidad))
            .ToList();
        if (!DescuentoMerma.Intentar(solicitudes, out var registros, out var error))
        {
            Aviso.Severity = InfoBarSeverity.Warning;
            Aviso.Title = "Sin existencia";
            Aviso.Message = error ?? "No hay existencia suficiente.";
            Aviso.IsOpen = true;
            return;
        }

        App.Instance.Mermas.Agregar(registros);
        Frame.Navigate(typeof(MermaPage));
    }
}

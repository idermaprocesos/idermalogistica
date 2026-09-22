using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Pages;

public sealed partial class OrdenCompraWindow : Page
{
    private readonly List<LineaOrdenCompra> _lineas = [];
    private readonly Guid _ordenId = Guid.NewGuid();
    private DateTimeOffset? _fechaRegistro;
    private bool _exportando;
    private bool _cargada;

    public OrdenCompraWindow()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (_cargada)
        {
            return;
        }

        if (e.Parameter is not IEnumerable<LineaOrdenCompra> lineas)
        {
            if (Frame?.CanGoBack == true)
            {
                Frame.GoBack();
            }

            return;
        }

        _cargada = true;
        _lineas.AddRange(lineas);
        PrepararEncabezado();
        foreach (var linea in _lineas)
        {
            linea.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(LineaOrdenCompra.CantidadPedir))
                {
                    ActualizarTotal();
                }
            };
        }

        Lista.ItemsSource = _lineas;
        ResumenProductos.Text = _lineas.Count == 1
            ? "1 producto"
            : $"{_lineas.Count} productos";
        ActualizarTotal();
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void PrepararEncabezado()
    {
        var hoy = DateTime.Today;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        CajaMes.Text = hoy.ToString("MM / yyyy");
        CajaCobertura.Text = $"{inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}";
        FechaElaboracion.Date = new DateTimeOffset(hoy);
        ComboEstado.ItemsSource = CalculoOrdenCompra.EstadosDocumento;
        ComboEstado.SelectedIndex = 0;
    }

    private void ActualizarTotal()
    {
        var grupos = _lineas
            .Where(l => l.TienePrecio)
            .GroupBy(l => MonedaPrecio.Normalizar(l.Moneda))
            .Select(g => MonedaPrecio.Formato(CalculoOrdenCompra.Redondear(g.Sum(l => l.ImporteConIgv)), g.Key))
            .ToList();
        TotalGeneral.Text = grupos.Count == 0
            ? "Total con IGV: sin precios registrados"
            : "Total con IGV: " + string.Join("   ·   ", grupos);
    }

    private async void ExportarPdf_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(pdf: true);

    private async void ExportarExcel_Click(object sender, RoutedEventArgs e) =>
        await ExportarAsync(pdf: false);

    private async Task ExportarAsync(bool pdf)
    {
        if (_exportando)
        {
            return;
        }

        _exportando = true;
        try
        {
            var documento = Capturar();
            var resultado = await ExcelUi.ExportarOrdenCompraAsync(documento, pdf);
            if (resultado is null)
            {
                return;
            }

            Registrar(documento);
            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = pdf
                ? "Se generó el PDF y la orden quedó en Órdenes anteriores."
                : "Se generó el Excel y la orden quedó en Órdenes anteriores.";
            BarraAviso.IsOpen = true;
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            BarraAviso.Severity = InfoBarSeverity.Error;
            BarraAviso.Message = $"No se pudo exportar: {ex.Message}";
            BarraAviso.IsOpen = true;
        }
        finally
        {
            _exportando = false;
        }
    }

    private OrdenCompraDocumento Capturar()
    {
        var elegido = FechaElaboracion.Date;
        var fecha = elegido.Year < 2000 ? DateTime.Today : elegido.LocalDateTime.Date;
        return new OrdenCompraDocumento
        {
            MesAnio = (CajaMes.Text ?? string.Empty).Trim(),
            FechaElaboracion = fecha.ToString("dd/MM/yyyy"),
            ElaboradoPor = (CajaElaborado.Text ?? string.Empty).Trim(),
            RevisadoPor = (CajaRevisado.Text ?? string.Empty).Trim(),
            PeriodoCobertura = (CajaCobertura.Text ?? string.Empty).Trim(),
            Estado = ComboEstado.SelectedItem as string ?? "Borrador",
            Lineas = _lineas.Select(l => l.Exportar()).ToList()
        };
    }

    private void Registrar(OrdenCompraDocumento documento)
    {
        var fecha = _fechaRegistro ?? DateTimeOffset.Now;
        _fechaRegistro = fecha;
        App.Instance.OrdenesCompra.Guardar(new OrdenCompraRegistrada
        {
            Id = _ordenId,
            FechaRegistro = fecha,
            Documento = documento
        });
    }
}

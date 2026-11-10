using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdermaFichas.Pages;

public sealed partial class PronosticoPage : Page
{
    private readonly List<OpcionAreaPronostico> _areas = [];
    private ResultadoPronostico? _resultado;
    private bool _rellenandoAreas;

    public PronosticoPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            FechaDesde.Date ??= DateTimeOffset.Now;
            FechaHasta.Date ??= DateTimeOffset.Now;
            ModulosNube.DatosCambiados -= AlLlegarNube;
            ModulosNube.DatosCambiados += AlLlegarNube;
            AreasOperativas.Cambiaron -= AlCambiarAreas;
            AreasOperativas.Cambiaron += AlCambiarAreas;
            CargarOpcionesArea();
            Cargar();
        };
        Unloaded += (_, _) =>
        {
            ModulosNube.DatosCambiados -= AlLlegarNube;
            AreasOperativas.Cambiaron -= AlCambiarAreas;
        };
    }

    private void AlLlegarNube() => DispatcherQueue.TryEnqueue(Cargar);

    private void AlCambiarAreas() => DispatcherQueue.TryEnqueue(CargarOpcionesArea);

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ComboPeriodo is null)
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

    private void Orden_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Cargar();
        }
    }

    private void Area_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_rellenandoAreas)
        {
            Cargar();
        }
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Cargar();
        }
    }

    private async void Exportar_Click(object sender, RoutedEventArgs e)
    {
        Cargar();
        if (_resultado is not ResultadoPronostico resultado || !resultado.HayMovimientosEnRango)
        {
            var area = _resultado?.Area ?? "el rango elegido";
            MostrarAviso(
                InfoBarSeverity.Warning,
                "Sin movimientos",
                area == "Todas las áreas"
                    ? "No hay ingresos, salidas ni merma en el rango elegido."
                    : $"No hay ingresos, salidas ni merma de {area} en el rango elegido.");
            return;
        }

        if (resultado.Filas.Count == 0)
        {
            MostrarAviso(
                InfoBarSeverity.Warning,
                "Sin productos",
                HayBusqueda()
                    ? "Ningún producto coincide con la búsqueda."
                    : $"No hay movimientos de {resultado.Area} para exportar.");
            return;
        }

        var nombre = $"Pronostico {resultado.Area} {resultado.Rango.Titulo}".Replace('/', '-');
        var exportado = resultado;
        var archivo = await ExcelUi.GuardarPdfAsync(nombre, ruta => PronosticoExportService.ExportarPdf(ruta, exportado));
        if (archivo is not null)
        {
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, archivo);
        }
    }

    private void Cargar()
    {
        if (ComboPeriodo is null || ComboOrden is null || ComboArea is null)
        {
            return;
        }

        var rango = RangoPronostico.De(Periodo(), FechaDesde?.Date, FechaHasta?.Date);
        var orden = PronosticoService.OrdenDe(TextoCombo(ComboOrden));
        var (areaId, soloSinArea, tituloArea) = FiltroAreaActual();
        var movimientos = App.Instance.Registro.CopiarTodos();
        var fichas = App.Instance.Repositorio.Todas;
        var busqueda = CajaBusqueda?.Text;
        var completo = PronosticoService.Analizar(movimientos, fichas, rango, orden, null, areaId, soloSinArea, tituloArea);
        var visible = string.IsNullOrWhiteSpace(busqueda)
            ? completo
            : PronosticoService.Analizar(movimientos, fichas, rango, orden, busqueda, areaId, soloSinArea, tituloArea);
        _resultado = visible;

        Lista.ItemsSource = visible.Filas.Select(f => new PronosticoItem(f)).ToList();
        Vacio.Visibility = visible.Filas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Vacio.Text = HayBusqueda()
            ? "Ningún producto coincide con la búsqueda."
            : soloSinArea || areaId is not null
                ? "No hay ingresos, salidas ni merma de esta área en este rango ni en el periodo anterior."
                : "No hay ingresos, salidas ni merma en este rango ni en el periodo anterior.";
        TextoRango.Text = $"{tituloArea} · {rango.ResumenComparacion(completo.Anterior)}";
        KpiIngresos.Text = PronosticoService.FormatoCantidad(completo.TotalIngresos);
        KpiSalidas.Text = PronosticoService.FormatoCantidad(completo.TotalSalidas);
        KpiMerma.Text = PronosticoService.FormatoCantidad(completo.TotalMerma);
        KpiSubieron.Text = completo.Subieron.ToString();
        KpiBajaron.Text = completo.Bajaron.ToString();
    }

    private void MostrarAviso(InfoBarSeverity severidad, string titulo, string mensaje)
    {
        Aviso.Severity = severidad;
        Aviso.Title = titulo;
        Aviso.Message = mensaje;
        Aviso.IsOpen = true;
    }

    private void CargarOpcionesArea()
    {
        if (ComboArea is null)
        {
            return;
        }

        var anterior = ComboArea.SelectedIndex >= 0 && ComboArea.SelectedIndex < _areas.Count
            ? _areas[ComboArea.SelectedIndex]
            : null;
        _areas.Clear();
        _areas.Add(new OpcionAreaPronostico("Todas las áreas", null, false));
        _areas.AddRange(AreasOperativas.Todas.Select(a => new OpcionAreaPronostico(a.Nombre, a.Id, false)));
        _areas.Add(new OpcionAreaPronostico("Sin área", null, true));

        _rellenandoAreas = true;
        ComboArea.ItemsSource = _areas.Select(o => o.Texto).ToList();
        var indice = anterior is null
            ? 0
            : _areas.FindIndex(o => o.SinArea == anterior.SinArea && o.Id == anterior.Id);
        ComboArea.SelectedIndex = indice >= 0 ? indice : 0;
        _rellenandoAreas = false;
        if (IsLoaded)
        {
            Cargar();
        }
    }

    private (Guid? Area, bool SinArea, string Titulo) FiltroAreaActual()
    {
        if (ComboArea.SelectedIndex <= 0 || ComboArea.SelectedIndex >= _areas.Count)
        {
            return (null, false, "Todas las áreas");
        }

        var opcion = _areas[ComboArea.SelectedIndex];
        return (opcion.Id, opcion.SinArea, opcion.Texto);
    }

    private bool HayBusqueda() => !string.IsNullOrWhiteSpace(CajaBusqueda?.Text);

    private string Periodo() => TextoCombo(ComboPeriodo);

    private static string TextoCombo(ComboBox combo) =>
        (combo.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty;
}

public sealed record OpcionAreaPronostico(string Texto, Guid? Id, bool SinArea);

public sealed class PronosticoItem
{
    public PronosticoItem(FilaPronostico fila)
    {
        Codigo = string.IsNullOrWhiteSpace(fila.Codigo) ? "—" : fila.Codigo;
        Nombre = fila.Nombre;
        Unidad = string.IsNullOrWhiteSpace(fila.Unidad) ? "—" : fila.Unidad;
        Ingresos = PronosticoService.FormatoCantidad(fila.Ingresos);
        Salidas = PronosticoService.FormatoCantidad(fila.Salidas);
        Merma = PronosticoService.FormatoCantidad(fila.Merma);
        Anterior = PronosticoService.FormatoCantidad(fila.DemandaAnterior);
        Variacion = PronosticoService.FormatoVariacion(fila.Variacion);
        Tendencia = fila.Tendencia;
        Pronostico = PronosticoService.FormatoCantidad(fila.Pronostico);
    }

    public string Codigo { get; }
    public string Nombre { get; }
    public string Unidad { get; }
    public string Ingresos { get; }
    public string Salidas { get; }
    public string Merma { get; }
    public string Anterior { get; }
    public string Variacion { get; }
    public string Tendencia { get; }
    public string Pronostico { get; }
}

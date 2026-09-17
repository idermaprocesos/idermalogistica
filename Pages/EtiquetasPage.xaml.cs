using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace IdermaFichas.Pages;

public sealed partial class EtiquetasPage : Page
{
    private static readonly (string Texto, TipoSimboloEtiqueta Tipo)[] Tipos =
    [
        ("Código QR", TipoSimboloEtiqueta.Qr),
        ("Código de barras (Code 128)", TipoSimboloEtiqueta.Code128),
        ("Code 39", TipoSimboloEtiqueta.Code39),
        ("EAN-13", TipoSimboloEtiqueta.Ean13),
        ("Data Matrix", TipoSimboloEtiqueta.DataMatrix),
        ("PDF417", TipoSimboloEtiqueta.Pdf417),
        ("Aztec", TipoSimboloEtiqueta.Aztec)
    ];

    private static readonly (string Texto, TamanoHojaEtiqueta Hoja)[] Hojas =
    [
        ("A4 (predeterminada)", TamanoHojaEtiqueta.A4),
        ("Carta", TamanoHojaEtiqueta.Carta),
        ("Oficio / Legal", TamanoHojaEtiqueta.Oficio),
        ("A5", TamanoHojaEtiqueta.A5)
    ];

    private static readonly (string Texto, DisposicionEtiqueta Disposicion)[] Disposiciones =
    [
        ("Vertical (código arriba)", DisposicionEtiqueta.Vertical),
        ("Horizontal (código a la izquierda)", DisposicionEtiqueta.Horizontal)
    ];

    private IReadOnlyList<FilaEtiqueta> _filas = [];
    private TipoSimboloEtiqueta? _previewTipo;

    public EtiquetasPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ComboTipo.ItemsSource = Tipos.Select(t => t.Texto).ToList();
        ComboTipo.SelectedIndex = 0;
        ComboHoja.ItemsSource = Hojas.Select(h => h.Texto).ToList();
        ComboHoja.SelectedIndex = 0;
        ComboDisposicion.ItemsSource = Disposiciones.Select(d => d.Texto).ToList();
        ComboDisposicion.SelectedIndex = 0;
        ActualizarResumen();
    }

    private OpcionesEtiqueta OpcionesActuales()
    {
        var tipo = ComboTipo.SelectedIndex >= 0 && ComboTipo.SelectedIndex < Tipos.Length
            ? Tipos[ComboTipo.SelectedIndex].Tipo
            : TipoSimboloEtiqueta.Qr;
        var hoja = ComboHoja.SelectedIndex >= 0 && ComboHoja.SelectedIndex < Hojas.Length
            ? Hojas[ComboHoja.SelectedIndex].Hoja
            : TamanoHojaEtiqueta.A4;

        var disposicion = ComboDisposicion is { SelectedIndex: >= 0 } && ComboDisposicion.SelectedIndex < Disposiciones.Length
            ? Disposiciones[ComboDisposicion.SelectedIndex].Disposicion
            : DisposicionEtiqueta.Vertical;

        return new OpcionesEtiqueta
        {
            Tipo = tipo,
            Hoja = hoja,
            Columnas = (int)Math.Clamp(double.IsNaN(CajaColumnas.Value) ? 3 : CajaColumnas.Value, 1, 8),
            Filas = (int)Math.Clamp(double.IsNaN(CajaFilas.Value) ? 8 : CajaFilas.Value, 1, 16),
            TamanoMm = (float)Math.Clamp(SliderTamano.Value, 10, 70),
            Disposicion = disposicion,
            GuiaCorte = ToggleGuiaCorte?.IsOn == true
        };
    }

    private void Opciones_Changed(object sender, SelectionChangedEventArgs e) => ActualizarResumen();

    private void GuiaCorte_Toggled(object sender, RoutedEventArgs e) => ActualizarResumen();

    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => ActualizarResumen();

    private void SliderTamano_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        TextoTamano.Text = $"Tamaño del código: {SliderTamano.Value:0} mm";
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        if (ResumenOpciones is null || ComboTipo is null || ImagenPreview is null)
        {
            return;
        }

        var opciones = OpcionesActuales();
        var etiquetas = _filas.Sum(f => f.Cantidad);
        var omitidas = _filas.Count(f => f.Cantidad == 0);
        var porPagina = opciones.Columnas * opciones.Filas;
        var paginas = etiquetas == 0 ? 0 : (int)Math.Ceiling(etiquetas / (double)porPagina);

        var disposicion = opciones.Disposicion == DisposicionEtiqueta.Horizontal
            ? "horizontal"
            : "vertical";
        ResumenOpciones.Text =
            $"{SimboloEtiquetaService.Titulo(opciones.Tipo)} · {opciones.Columnas} × {opciones.Filas} por hoja {Hojas.First(h => h.Hoja == opciones.Hoja).Texto} · " +
            $"código de {opciones.TamanoMm:0} mm · {disposicion}" +
            (opciones.GuiaCorte ? " · con guía de corte" : string.Empty) +
            (etiquetas > 0 ? $" · se imprimirán {etiquetas} etiqueta(s) (la cantidad de cada fila) en {paginas} página(s)" : string.Empty) +
            (omitidas > 0 ? $" · {omitidas} fila(s) con cantidad 0 no se imprimen" : string.Empty);

        _ = ActualizarPreviewAsync(opciones);
    }

    private async Task ActualizarPreviewAsync(OpcionesEtiqueta opciones)
    {
        var muestra = _filas.FirstOrDefault(f => f.Cantidad > 0);
        var codigo = muestra?.Codigo ?? "CDB_002";
        var producto = muestra?.Producto ?? "MONALISA (HARD TYPE) FUCSIA";
        if (opciones.Tipo == TipoSimboloEtiqueta.Ean13 && codigo.Count(char.IsDigit) is not (12 or 13))
        {
            codigo = "750123456789";
        }

        PreviewFormato.Text = SimboloEtiquetaService.Titulo(opciones.Tipo);
        PreviewCodigo.Text = codigo;
        PreviewProducto.Text = producto;
        AplicarDisposicionPreview(opciones);
        if (GuiaPreview is not null)
        {
            GuiaPreview.Visibility = opciones.GuiaCorte ? Visibility.Visible : Visibility.Collapsed;
        }

        var copias = muestra?.Cantidad ?? 0;
        PreviewCuadricula.Text = copias > 0
            ? $"Este producto se imprimirá {copias} vez/veces, como indica su fila. La hoja se llena {opciones.Columnas} × {opciones.Filas}."
            : $"Así se verá cada etiqueta. La cantidad de copias la define la columna CANTIDAD de cada fila, no la lista sola.";

        var lado = Math.Clamp(opciones.TamanoMm * 3.6, 72, 220);
        if (SimboloEtiquetaService.EsBidimensional(opciones.Tipo))
        {
            ImagenPreview.Width = lado;
            ImagenPreview.Height = lado;
        }
        else
        {
            ImagenPreview.Width = Math.Min(280, lado * 2.6);
            ImagenPreview.Height = Math.Max(44, lado * 0.55);
        }

        if (_previewTipo == opciones.Tipo && ImagenPreview.Source is not null)
        {
            return;
        }

        try
        {
            var png = SimboloEtiquetaService.Generar(codigo, opciones.Tipo, Math.Max(24, opciones.TamanoMm));
            using var flujo = new InMemoryRandomAccessStream();
            await flujo.WriteAsync(png.AsBuffer());
            flujo.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(flujo);
            ImagenPreview.Source = bitmap;
            _previewTipo = opciones.Tipo;
        }
        catch
        {
            var png = SimboloEtiquetaService.Generar(codigo, TipoSimboloEtiqueta.Code128, 24);
            using var flujo = new InMemoryRandomAccessStream();
            await flujo.WriteAsync(png.AsBuffer());
            flujo.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(flujo);
            ImagenPreview.Source = bitmap;
            _previewTipo = opciones.Tipo;
        }
    }

    private void AplicarDisposicionPreview(OpcionesEtiqueta opciones)
    {
        if (ImagenPreview is null || PreviewCodigo is null || PreviewProducto is null)
        {
            return;
        }

        var horizontal = opciones.Disposicion == DisposicionEtiqueta.Horizontal;
        if (horizontal)
        {
            Grid.SetRow(ImagenPreview, 0);
            Grid.SetRowSpan(ImagenPreview, 2);
            Grid.SetColumn(ImagenPreview, 0);
            Grid.SetColumnSpan(ImagenPreview, 1);
            ImagenPreview.HorizontalAlignment = HorizontalAlignment.Left;
            ImagenPreview.Margin = new Thickness(0, 0, 10, 0);

            Grid.SetRow(PreviewCodigo, 0);
            Grid.SetColumn(PreviewCodigo, 1);
            Grid.SetColumnSpan(PreviewCodigo, 1);
            PreviewCodigo.HorizontalAlignment = HorizontalAlignment.Left;
            PreviewCodigo.TextAlignment = TextAlignment.Left;
            PreviewCodigo.VerticalAlignment = VerticalAlignment.Bottom;
            PreviewCodigo.Margin = new Thickness(0, 0, 0, 0);

            Grid.SetRow(PreviewProducto, 1);
            Grid.SetColumn(PreviewProducto, 1);
            Grid.SetColumnSpan(PreviewProducto, 1);
            PreviewProducto.HorizontalAlignment = HorizontalAlignment.Left;
            PreviewProducto.TextAlignment = TextAlignment.Left;
            PreviewProducto.VerticalAlignment = VerticalAlignment.Top;
            PreviewProducto.Margin = new Thickness(0, 4, 0, 0);
            PreviewProducto.MaxWidth = 150;
            return;
        }

        Grid.SetRow(ImagenPreview, 0);
        Grid.SetRowSpan(ImagenPreview, 1);
        Grid.SetColumn(ImagenPreview, 0);
        Grid.SetColumnSpan(ImagenPreview, 2);
        ImagenPreview.HorizontalAlignment = HorizontalAlignment.Center;
        ImagenPreview.Margin = new Thickness(0);

        Grid.SetRow(PreviewCodigo, 1);
        Grid.SetColumn(PreviewCodigo, 0);
        Grid.SetColumnSpan(PreviewCodigo, 2);
        PreviewCodigo.HorizontalAlignment = HorizontalAlignment.Center;
        PreviewCodigo.TextAlignment = TextAlignment.Center;
        PreviewCodigo.VerticalAlignment = VerticalAlignment.Top;
        PreviewCodigo.Margin = new Thickness(0, 2, 0, 0);

        Grid.SetRow(PreviewProducto, 2);
        Grid.SetColumn(PreviewProducto, 0);
        Grid.SetColumnSpan(PreviewProducto, 2);
        PreviewProducto.HorizontalAlignment = HorizontalAlignment.Center;
        PreviewProducto.TextAlignment = TextAlignment.Center;
        PreviewProducto.VerticalAlignment = VerticalAlignment.Top;
        PreviewProducto.Margin = new Thickness(0, 4, 0, 0);
        PreviewProducto.MaxWidth = 160;
    }

    private async void AdjuntarExcel_Click(object sender, RoutedEventArgs e)
    {
        string? temporal = null;
        try
        {
            temporal = await ExcelUi.ElegirExcelTemporalAsync();
            if (temporal is null)
            {
                return;
            }

            _filas = ExcelEtiquetaService.Leer(temporal);
            _previewTipo = null;
            ListaProductos.ItemsSource = _filas;
            NombreArchivo.Text = "Excel validado. Encabezados: CÓDIGO · ÁREA · PRODUCTO · CANTIDAD.";
            var total = _filas.Sum(f => f.Cantidad);
            ResumenArchivo.Text = $"{_filas.Count} producto(s) en la lista · se imprimirán {total} etiqueta(s) según la columna CANTIDAD de cada fila.";
            ActualizarResumen();
            Mostrar("El archivo cumple la estructura y ya puede generar las etiquetas.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            _filas = [];
            _previewTipo = null;
            ListaProductos.ItemsSource = null;
            NombreArchivo.Text = "Ningún archivo adjunto.";
            ResumenArchivo.Text = string.Empty;
            ActualizarResumen();
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (temporal is not null && File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private async void Plantilla_Click(object sender, RoutedEventArgs e)
    {
        var resultado = await ExcelUi.GuardarExcelAsync(
            "plantilla-etiquetas-iderma",
            ExcelEtiquetaService.CrearPlantilla);
        if (resultado is null)
        {
            return;
        }

        Mostrar("Se exportó la plantilla con los encabezados CÓDIGO, ÁREA, PRODUCTO y CANTIDAD.", InfoBarSeverity.Success);
        await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
    }

    private async void PruebaImpresion_Click(object sender, RoutedEventArgs e)
    {
        var archivo = await ExcelUi.ElegirGuardarPdfAsync("prueba-impresion-calibracion-iderma");
        if (archivo is null)
        {
            return;
        }

        try
        {
            var resultado = await EjecutarConProgresoAsync(
                "Generando prueba de impresión…",
                progreso =>
                {
                    progreso.Report((10, "Preparando la hoja de calibración…"));
                    var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
                    progreso.Report((40, "Dibujando colores, grosores y QR…"));
                    PdfEtiquetaService.ExportarPruebaImpresion(temporal);
                    progreso.Report((90, "Guardando el PDF…"));
                    return temporal;
                },
                archivo);

            if (resultado is null)
            {
                return;
            }

            Mostrar("Se generó la prueba de calibración (colores, tinta, grosores, QR y estándares).", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo generar la prueba: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void Generar_Click(object sender, RoutedEventArgs e)
    {
        if (_filas.Count == 0)
        {
            Mostrar("Adjunte primero un Excel con la estructura obligatoria.", InfoBarSeverity.Warning);
            return;
        }

        var archivo = await ExcelUi.ElegirGuardarPdfAsync("etiquetas-iderma");
        if (archivo is null)
        {
            return;
        }

        try
        {
            var opciones = OpcionesActuales();
            var filas = _filas.ToList();
            var resultado = await EjecutarConProgresoAsync(
                "Generando etiquetas…",
                progreso =>
                {
                    var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
                    PdfEtiquetaService.ExportarEtiquetas(temporal, filas, opciones, progreso);
                    return temporal;
                },
                archivo);

            if (resultado is null)
            {
                return;
            }

            var total = filas.Sum(f => f.Cantidad);
            Mostrar($"Se generó el PDF con {total} etiqueta(s): cada fila se repite las veces que indica su CANTIDAD.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task<ResultadoExportacion?> EjecutarConProgresoAsync(
        string titulo,
        Func<IProgress<(int Porcentaje, string Mensaje)>, string> trabajo,
        StorageFile archivo)
    {
        TextoProgreso.Text = titulo;
        BarraProgreso.IsIndeterminate = true;
        BarraProgreso.Value = 0;
        PanelProgreso.Visibility = Visibility.Visible;
        if (BotonGenerar is not null)
        {
            BotonGenerar.IsEnabled = false;
        }

        var textoDialogo = new TextBlock { Text = titulo, TextWrapping = TextWrapping.Wrap };
        var barraDialogo = new ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true, Height = 10 };
        var contenido = new StackPanel { Spacing = 12 };
        contenido.Children.Add(textoDialogo);
        contenido.Children.Add(barraDialogo);

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = contenido,
            XamlRoot = XamlRoot
        };
        var mostrar = dialogo.ShowAsync();

        var progreso = new Progress<(int Porcentaje, string Mensaje)>(estado =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                BarraProgreso.IsIndeterminate = false;
                BarraProgreso.Value = estado.Porcentaje;
                TextoProgreso.Text = estado.Mensaje;
                barraDialogo.IsIndeterminate = false;
                barraDialogo.Value = estado.Porcentaje;
                textoDialogo.Text = estado.Mensaje;
            });
        });

        try
        {
            var temporal = await Task.Run(() => trabajo(progreso));
            DispatcherQueue.TryEnqueue(() =>
            {
                BarraProgreso.IsIndeterminate = false;
                BarraProgreso.Value = 100;
                TextoProgreso.Text = "Guardando el archivo…";
                barraDialogo.Value = 100;
                textoDialogo.Text = "Guardando el archivo…";
            });
            return await ExcelUi.CopiarPdfAsync(temporal, archivo);
        }
        finally
        {
            dialogo.Hide();
            try
            {
                await mostrar;
            }
            catch (Exception)
            {
                // El diálogo se cerró al terminar el proceso.
            }

            PanelProgreso.Visibility = Visibility.Collapsed;
            BarraProgreso.IsIndeterminate = false;
            if (BotonGenerar is not null)
            {
                BotonGenerar.IsEnabled = true;
            }
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Severity = severidad;
        BarraEstado.Message = mensaje;
        BarraEstado.IsOpen = true;
    }
}

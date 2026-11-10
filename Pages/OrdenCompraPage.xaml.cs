using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class OrdenCompraPage : Page, IVistaInventario
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private readonly HashSet<Guid> _marcadas = [];
    private readonly HashSet<int> _meses = [];
    private int? _atajoMeses;
    private List<OrdenProductoItem> _visibles = [];
    private List<OpcionAreaOrden> _opcionesArea = [];
    private Guid? _unidad;
    private bool _sinArea;
    private bool _actualizandoFiltros;
    private bool _modoSeleccionar;
    private bool _ignorarClicLista;
    private bool _tipProveedorMostrado;
    private int _anclaSeleccion = -1;
    private string? _proveedorActivo;
    private Guid? _productoElegidoId;

    public OrdenCompraPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Disabled;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.NavigationMode == Microsoft.UI.Xaml.Navigation.NavigationMode.Back)
        {
            return;
        }

        _actualizandoFiltros = true;
        ComboOrden.ItemsSource = OrdenFichas.Opciones.Select(o => o.Texto).ToList();
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboStock.ItemsSource = FiltrosCatalogo.OpcionesStock;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.ItemsSource = FiltrosCatalogo.OpcionesCaducidad;
        ComboCaducidad.SelectedIndex = 0;
        ComboMesDesde.ItemsSource = FiltroPeriodoIngresos.NombresMesCompletos;
        ComboMesHasta.ItemsSource = FiltroPeriodoIngresos.NombresMesCompletos;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        ConstruirMeses();
        CargarAnios();
        CargarOpcionesArea();
        _actualizandoFiltros = false;
        RefrescarLista();
    }

    private void CajaBusqueda_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        ActualizarSugerencias(sender.Text);
    }

    private async void CajaBusqueda_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SugerenciaOrdenCompra sugerencia)
        {
            await AplicarSugerenciaAsync(sugerencia);
            sender.Text = sugerencia.TextoCaja;
        }
    }

    private async void CajaBusqueda_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SugerenciaOrdenCompra elegida)
        {
            await AplicarSugerenciaAsync(elegida);
            sender.Text = elegida.TextoCaja;
            return;
        }

        var texto = (args.QueryText ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            return;
        }

        var primera = ConstruirSugerencias(texto).FirstOrDefault();
        if (primera is null)
        {
            Mostrar($"No hay productos ni proveedores que coincidan con «{texto}».", InfoBarSeverity.Warning);
            return;
        }

        await AplicarSugerenciaAsync(primera);
        sender.Text = primera.TextoCaja;
    }

    private void CambiarProveedor_Click(object sender, RoutedEventArgs e)
    {
        _proveedorActivo = null;
        _productoElegidoId = null;
        _marcadas.Clear();
        CajaBusqueda.Text = string.Empty;
        CajaBusqueda.ItemsSource = null;
        RefrescarLista();
    }

    private void ActualizarSugerencias(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length < 1)
        {
            CajaBusqueda.ItemsSource = null;
            return;
        }

        CajaBusqueda.ItemsSource = ConstruirSugerencias(limpio);
    }

    private List<SugerenciaOrdenCompra> ConstruirSugerencias(string texto)
    {
        var repo = App.Instance.Repositorio;
        var origen = repo.Buscar(string.Empty, _sinArea ? null : _unidad, _sinArea).ToList();
        var sugerencias = new List<SugerenciaOrdenCompra>();

        var proveedores = origen
            .Select(f => f.Proveedor?.Trim() ?? string.Empty)
            .Where(p => p.Length > 0)
            .GroupBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .Where(g => g.Key.Contains(texto, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(6)
            .Select(g => new SugerenciaOrdenCompra(
                true,
                g.Key,
                g.Count() == 1 ? "Proveedor · 1 producto" : $"Proveedor · {g.Count()} productos",
                g.Key,
                null,
                g.Key));
        sugerencias.AddRange(proveedores);

        var productos = origen
            .Where(f =>
                Contiene(f.Codigo, texto)
                || Contiene(f.Nombre, texto)
                || Contiene(f.Marca, texto))
            .OrderBy(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .Select(f =>
            {
                var proveedor = string.IsNullOrWhiteSpace(f.Proveedor) ? "Sin proveedor" : f.Proveedor.Trim();
                var codigo = string.IsNullOrWhiteSpace(f.Codigo) ? "—" : f.Codigo.Trim();
                return new SugerenciaOrdenCompra(
                    false,
                    $"{codigo} · {f.Nombre}",
                    $"Producto · {proveedor}",
                    proveedor,
                    f.Id,
                    $"{codigo} · {f.Nombre}");
            });
        sugerencias.AddRange(productos);
        return sugerencias;
    }

    private static bool Contiene(string? valor, string texto) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.Contains(texto, StringComparison.CurrentCultureIgnoreCase);

    private async Task AplicarSugerenciaAsync(SugerenciaOrdenCompra sugerencia)
    {
        var proveedor = sugerencia.Proveedor?.Trim() ?? string.Empty;
        if (proveedor.Length == 0 || proveedor == "Sin proveedor")
        {
            if (sugerencia.EsProveedor || sugerencia.FichaId is not Guid fichaId)
            {
                Mostrar("Ese producto no tiene proveedor asignado.", InfoBarSeverity.Warning);
                return;
            }

            var ficha = App.Instance.Repositorio.Obtener(fichaId);
            if (ficha is null)
            {
                Mostrar("No se encontró el producto en el inventario.", InfoBarSeverity.Error);
                return;
            }

            Mostrar(
                $"«{ficha.Nombre}» no tiene proveedor. Regístrelo para continuar la orden.",
                InfoBarSeverity.Informational);
            var nuevo = await ProveedorEditorDialogo.MostrarAsync(XamlRoot);
            if (nuevo is null)
            {
                Mostrar("Debe registrar un proveedor para armar la orden con este producto.", InfoBarSeverity.Warning);
                return;
            }

            ProveedorFichaSync.AsignarAFicha(ficha, nuevo);
            proveedor = nuevo.Nombre.Trim();
            Mostrar($"Proveedor «{proveedor}» guardado y asignado a «{ficha.Nombre}».", InfoBarSeverity.Success);
            sugerencia = new SugerenciaOrdenCompra(
                false,
                sugerencia.Titulo,
                $"Producto · {proveedor}",
                proveedor,
                ficha.Id,
                sugerencia.TextoCaja);
        }

        var mismo = string.Equals(_proveedorActivo, proveedor, StringComparison.CurrentCultureIgnoreCase);
        if (!mismo)
        {
            _marcadas.Clear();
        }

        _proveedorActivo = proveedor;
        _productoElegidoId = sugerencia.EsProveedor ? null : sugerencia.FichaId;
        CajaBusqueda.ItemsSource = null;
        RefrescarLista();

        if (_productoElegidoId is Guid id)
        {
            var item = _visibles.FirstOrDefault(v => v.Id == id);
            if (item is not null && !item.EstaMarcada)
            {
                item.EstaMarcada = true;
                _marcadas.Add(item.Id);
                ActualizarResumenMarcas();
            }
        }
    }

    private void Filtros_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        RefrescarLista();
    }

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void RangoMeses_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        var desde = ComboMesDesde.SelectedIndex;
        var hasta = ComboMesHasta.SelectedIndex;
        if (desde < 0 || hasta < 0)
        {
            return;
        }

        var inicio = Math.Min(desde, hasta) + 1;
        var fin = Math.Max(desde, hasta) + 1;
        _meses.Clear();
        _actualizandoFiltros = true;
        foreach (var chip in PanelMeses.Children.OfType<ToggleButton>())
        {
            if (chip.Tag is not int mes)
            {
                continue;
            }

            var activo = mes >= inicio && mes <= fin;
            chip.IsChecked = activo;
            if (activo)
            {
                _meses.Add(mes);
            }
        }

        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void LimpiarFiltros_Click(object sender, RoutedEventArgs e)
    {
        _actualizandoFiltros = true;
        CajaBusqueda.Text = string.Empty;
        CajaBusqueda.ItemsSource = null;
        _proveedorActivo = null;
        _productoElegidoId = null;
        _marcadas.Clear();
        _unidad = null;
        _sinArea = false;
        ComboStock.SelectedIndex = 0;
        ComboCaducidad.SelectedIndex = 0;
        ComboOrden.SelectedIndex = OrdenFichas.IndicePredeterminado;
        ComboAnio.SelectedIndex = 0;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _meses.Clear();
        _atajoMeses = null;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        ChipUltimos3.IsChecked = false;
        ChipUltimos6.IsChecked = false;
        SeleccionarPrimero(ComboCategoria);
        SeleccionarPrimero(ComboUbicacion);
        SeleccionarPrimero(ComboLote);
        CargarOpcionesArea();
        _actualizandoFiltros = false;
        RefrescarLista();
    }

    private static void SeleccionarPrimero(ComboBox combo)
    {
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void Area_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_actualizandoFiltros)
        {
            return;
        }

        AplicarAreaSeleccionada();
        RefrescarLista();
    }

    private void CargarOpcionesArea()
    {
        var repo = App.Instance.Repositorio;
        var anterior = ComboArea.SelectedIndex >= 0 && ComboArea.SelectedIndex < _opcionesArea.Count
            ? _opcionesArea[ComboArea.SelectedIndex]
            : null;
        var criticos = repo.Todas.Count(f => f.EstaStockCritico);
        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();
        _opcionesArea =
        [
            new($"Todas las unidades ({repo.Todas.Count})", null, false)
        ];
        _opcionesArea.AddRange(AreasOperativas.Todas.Select(a =>
            new OpcionAreaOrden($"{a.Nombre} ({repo.Contar(a.Id)})", a.Id, false)));
        _opcionesArea.Add(new($"Sin área ({repo.Contar(null)})", null, true));
        _opcionesArea.Add(new($"Stock crítico ({criticos})", null, false, FiltroStockCatalogo.Critico));
        _opcionesArea.Add(new($"Vencidos ({vencidos})", null, false, null, FiltroCaducidadCatalogo.Vencidas));
        _opcionesArea.Add(new($"Próximos 30 días ({proximos})", null, false, null, FiltroCaducidadCatalogo.Proximas));

        var previo = _actualizandoFiltros;
        _actualizandoFiltros = true;
        ComboArea.ItemsSource = _opcionesArea.Select(o => o.Texto).ToList();
        var indice = anterior is null
            ? 0
            : _opcionesArea.FindIndex(o => MismaOpcionArea(o, anterior));
        if (indice < 0)
        {
            indice = !_unidad.HasValue && !_sinArea
                ? 0
                : _opcionesArea.FindIndex(o =>
                    !o.EsFiltroRapido && o.SinArea == _sinArea && o.Id == _unidad);
        }

        ComboArea.SelectedIndex = indice >= 0 ? indice : 0;
        AplicarAreaSeleccionada();
        _actualizandoFiltros = previo;
    }

    private static bool MismaOpcionArea(OpcionAreaOrden a, OpcionAreaOrden b) =>
        a.SinArea == b.SinArea &&
        a.Id == b.Id &&
        a.Stock == b.Stock &&
        a.Caducidad == b.Caducidad;

    private void AplicarAreaSeleccionada()
    {
        if (ComboArea.SelectedIndex < 0 || ComboArea.SelectedIndex >= _opcionesArea.Count)
        {
            _unidad = null;
            _sinArea = false;
            return;
        }

        var opcion = _opcionesArea[ComboArea.SelectedIndex];
        if (opcion.EsFiltroRapido)
        {
            _unidad = null;
            _sinArea = false;
            var previo = _actualizandoFiltros;
            _actualizandoFiltros = true;
            ComboStock.SelectedIndex = (int)(opcion.Stock ?? FiltroStockCatalogo.Todos);
            ComboCaducidad.SelectedIndex = (int)(opcion.Caducidad ?? FiltroCaducidadCatalogo.Todas);
            _actualizandoFiltros = previo;
            return;
        }

        _unidad = opcion.SinArea ? null : opcion.Id;
        _sinArea = opcion.SinArea;
    }

    private void ConstruirMeses()
    {
        PanelMeses.Children.Clear();
        for (var mes = 1; mes <= 12; mes++)
        {
            var chip = new ToggleButton
            {
                Content = FiltroPeriodoIngresos.NombresMes[mes - 1],
                Tag = mes,
                MinWidth = 48
            };
            chip.Click += ChipMes_Click;
            PanelMeses.Children.Add(chip);
        }
    }

    private void ChipMes_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip || chip.Tag is not int mes)
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _meses.Add(mes);
        }
        else
        {
            _meses.Remove(mes);
        }

        _actualizandoFiltros = true;
        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void TodosMeses_Click(object sender, RoutedEventArgs e)
    {
        _meses.Clear();
        _actualizandoFiltros = true;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        ComboMesDesde.SelectedIndex = -1;
        ComboMesHasta.SelectedIndex = -1;
        _actualizandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista();
    }

    private void AtajoMeses_Click(object sender, RoutedEventArgs e)
    {
        if (_actualizandoFiltros || sender is not ToggleButton chip ||
            !int.TryParse(chip.Tag?.ToString(), out var meses))
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _atajoMeses = meses;
            _actualizandoFiltros = true;
            ComboAnio.SelectedIndex = 0;
            ComboMesDesde.SelectedIndex = -1;
            ComboMesHasta.SelectedIndex = -1;
            _meses.Clear();
            foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
            {
                hijo.IsChecked = false;
            }

            _actualizandoFiltros = false;
        }
        else if (_atajoMeses == meses)
        {
            _atajoMeses = null;
        }

        ActualizarAtajos();
        RefrescarLista();
    }

    private void QuitarAtajoMeses()
    {
        if (_atajoMeses is null)
        {
            return;
        }

        _atajoMeses = null;
        ActualizarAtajos();
    }

    private void ActualizarAtajos()
    {
        _actualizandoFiltros = true;
        ChipUltimos3.IsChecked = _atajoMeses == 3;
        ChipUltimos6.IsChecked = _atajoMeses == 6;
        _actualizandoFiltros = false;
    }

    private void CargarAnios()
    {
        var actual = ComboAnio.SelectedItem as string;
        var opciones = new List<string> { "Todos los años" };
        opciones.AddRange(App.Instance.Registro.Anios().Select(a => a.ToString(Cultura)));
        ComboAnio.ItemsSource = opciones;
        if (actual is not null && opciones.Contains(actual))
        {
            ComboAnio.SelectedItem = actual;
        }
        else
        {
            ComboAnio.SelectedIndex = 0;
        }
    }

    private int? AnioActual()
    {
        if (ComboAnio.SelectedIndex <= 0 || ComboAnio.SelectedItem is not string texto)
        {
            return null;
        }

        return int.TryParse(texto, out var anio) ? anio : null;
    }

    private DateTimeOffset? DesdeActual() =>
        _atajoMeses is int n ? FiltroPeriodoIngresos.InicioVentanaMeses(n) : null;

    private int? AnioParaFiltro() => _atajoMeses is null ? AnioActual() : null;

    private IReadOnlySet<int> MesesParaFiltro() => _atajoMeses is null ? _meses : [];

    private string PeriodoActual() =>
        FiltroPeriodoIngresos.Describir(AnioParaFiltro(), MesesParaFiltro(), DesdeActual(), _atajoMeses);

    public void AplicarCambioInventario()
    {
        if (_marcadas.Count > 0)
        {
            return;
        }

        CargarOpcionesArea();
        RefrescarLista();
    }

    private void RefrescarLista()
    {
        if (ComboOrden.SelectedIndex < 0 || ComboStock.SelectedIndex < 0 || ComboCaducidad.SelectedIndex < 0)
        {
            return;
        }

        var repo = App.Instance.Repositorio;
        var registro = App.Instance.Registro;
        var precios = App.Instance.HistorialPrecios;
        var origenArea = repo.Buscar(string.Empty, _sinArea ? null : _unidad, _sinArea);
        RellenarListas(origenArea);

        _actualizandoFiltros = true;
        CargarAnios();
        _actualizandoFiltros = false;
        TextoPeriodo.Text = PeriodoActual();
        ActualizarTextosFiltrosRapidos();

        var hayProveedor = !string.IsNullOrWhiteSpace(_proveedorActivo);
        BotonCambiarProveedor.Visibility = hayProveedor ? Visibility.Visible : Visibility.Collapsed;
        if (!hayProveedor)
        {
            _visibles = [];
            ListaProductos.ItemsSource = _visibles;
            PanelLista.Visibility = Visibility.Collapsed;
            PanelVacio.Visibility = Visibility.Visible;
            TituloLista.Text = "Productos";
            ActualizarResumenMarcas();
            return;
        }

        PanelVacio.Visibility = Visibility.Collapsed;
        PanelLista.Visibility = Visibility.Visible;

        var anio = AnioParaFiltro();
        var meses = MesesParaFiltro();
        var desde = DesdeActual();
        var atajo = _atajoMeses;
        var fichas = origenArea
            .Where(f => string.Equals(
                (f.Proveedor ?? string.Empty).Trim(),
                _proveedorActivo,
                StringComparison.CurrentCultureIgnoreCase))
            .Where(f => FiltrosCatalogo.Coincide(
                f,
                (FiltroStockCatalogo)ComboStock.SelectedIndex,
                (FiltroCaducidadCatalogo)ComboCaducidad.SelectedIndex,
                ComboCategoria.SelectedItem as string,
                FiltrosCatalogo.TodosProveedores,
                ComboUbicacion.SelectedItem as string,
                ComboLote.SelectedItem as string));
        var criterio = OrdenFichas.Opciones[ComboOrden.SelectedIndex].Criterio;
        _visibles = OrdenFichas.Aplicar(fichas, criterio)
            .Select(ficha =>
            {
                var consumo = CalculoOrdenCompra.ConsumoEnPeriodo(registro.PorFicha(ficha.Id), anio, meses, desde, atajo);
                var ultimo = precios.Ultimo(ficha.Id);
                var unidad = string.IsNullOrWhiteSpace(ficha.UnidadMedida) ? "" : $" {ficha.UnidadMedida}";
                return new OrdenProductoItem
                {
                    Id = ficha.Id,
                    Ficha = ficha,
                    Codigo = string.IsNullOrWhiteSpace(ficha.Codigo) ? "—" : ficha.Codigo,
                    Nombre = ficha.Nombre,
                    Resumen = ResumenProducto(ficha),
                    StockTexto = $"Stock {ficha.Existencia.ToString("0.##", Cultura)}{unidad}",
                    ConsumoTexto = $"Cons. {consumo.ToString("0.##", Cultura)}{unidad}",
                    PrecioTexto = ultimo is null || ultimo.Monto <= 0
                        ? "Sin precio"
                        : MonedaPrecio.Formato(ultimo.Monto, ultimo.Moneda),
                    EstaMarcada = _marcadas.Contains(ficha.Id)
                };
            })
            .ToList();

        ListaProductos.ItemsSource = _visibles;
        _anclaSeleccion = -1;
        TituloLista.Text = $"Proveedor «{_proveedorActivo}» ({_visibles.Count})";
        ActualizarResumenMarcas();
    }

    private void ActualizarTextosFiltrosRapidos()
    {
        if (_opcionesArea.Count == 0)
        {
            return;
        }

        var seleccionado = ComboArea.SelectedIndex >= 0 && ComboArea.SelectedIndex < _opcionesArea.Count
            ? _opcionesArea[ComboArea.SelectedIndex]
            : null;
        var repo = App.Instance.Repositorio;
        var criticos = repo.Todas.Count(f => f.EstaStockCritico);
        var vencidos = repo.PartidasVencidas().Count();
        var proximos = repo.PartidasProximas(30).Count();
        for (var i = 0; i < _opcionesArea.Count; i++)
        {
            var opcion = _opcionesArea[i];
            if (opcion.Stock == FiltroStockCatalogo.Critico)
            {
                _opcionesArea[i] = opcion with { Texto = $"Stock crítico ({criticos})" };
            }
            else if (opcion.Caducidad == FiltroCaducidadCatalogo.Vencidas)
            {
                _opcionesArea[i] = opcion with { Texto = $"Vencidos ({vencidos})" };
            }
            else if (opcion.Caducidad == FiltroCaducidadCatalogo.Proximas)
            {
                _opcionesArea[i] = opcion with { Texto = $"Próximos 30 días ({proximos})" };
            }
            else if (!opcion.EsFiltroRapido && !opcion.SinArea && opcion.Id is null)
            {
                _opcionesArea[i] = opcion with { Texto = $"Todas las unidades ({repo.Todas.Count})" };
            }
            else if (opcion.SinArea)
            {
                _opcionesArea[i] = opcion with { Texto = $"Sin área ({repo.Contar(null)})" };
            }
            else if (opcion.Id is Guid id)
            {
                var nombre = AreasOperativas.Titulo(id);
                _opcionesArea[i] = opcion with { Texto = $"{nombre} ({repo.Contar(id)})" };
            }
        }

        var previo = _actualizandoFiltros;
        _actualizandoFiltros = true;
        ComboArea.ItemsSource = _opcionesArea.Select(o => o.Texto).ToList();
        if (seleccionado is not null)
        {
            var indice = _opcionesArea.FindIndex(o => MismaOpcionArea(o, seleccionado));
            ComboArea.SelectedIndex = indice >= 0 ? indice : 0;
        }

        _actualizandoFiltros = previo;
    }

    private static string ResumenProducto(FichaTecnica ficha)
    {
        var proveedor = string.IsNullOrWhiteSpace(ficha.Proveedor) ? "Sin proveedor" : ficha.Proveedor.Trim();
        return $"{ficha.AreaTitulo} · {ficha.Categoria} · {proveedor}".Trim(' ', '·');
    }

    private void RellenarListas(IReadOnlyList<FichaTecnica> origen)
    {
        var baseLista = string.IsNullOrWhiteSpace(_proveedorActivo)
            ? origen
            : origen
                .Where(f => string.Equals(
                    (f.Proveedor ?? string.Empty).Trim(),
                    _proveedorActivo,
                    StringComparison.CurrentCultureIgnoreCase))
                .ToList();
        _actualizandoFiltros = true;
        AsignarLista(ComboCategoria, FiltrosCatalogo.ValoresUnicos(baseLista, f => f.Categoria, FiltrosCatalogo.TodasCategorias));
        AsignarLista(ComboUbicacion, FiltrosCatalogo.ValoresUnicos(baseLista, f => f.Ubicacion, FiltrosCatalogo.TodasUbicaciones));
        AsignarLista(ComboLote, FiltrosCatalogo.LotesUnicos(baseLista));
        _actualizandoFiltros = false;
    }

    private static void AsignarLista(ComboBox combo, IReadOnlyList<string> items)
    {
        var actual = combo.SelectedItem as string;
        combo.ItemsSource = items;
        if (actual is not null && items.Any(i => string.Equals(i, actual, StringComparison.CurrentCultureIgnoreCase)))
        {
            combo.SelectedItem = items.First(i => string.Equals(i, actual, StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            combo.SelectedIndex = items.Count > 0 ? 0 : -1;
        }
    }

    private void ListaProductos_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_ignorarClicLista)
        {
            _ignorarClicLista = false;
            return;
        }

        if (e.ClickedItem is not OrdenProductoItem item ||
            !SeleccionListaFichas.DebeMarcarSinAbrir(_modoSeleccionar))
        {
            return;
        }

        AplicarClicConProveedor(item);
        ActualizarResumenMarcas();
    }

    private void ListaProductos_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.A && SeleccionListaFichas.CtrlIzquierdoPulsado)
        {
            SeleccionarMismoProveedor();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
            ActualizarResumenMarcas();
            e.Handled = true;
        }
    }

    private void ModoSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        _modoSeleccionar = BotonModoSeleccionar.IsChecked == true;
        TextoModoSeleccionar.Text = _modoSeleccionar ? "Seleccionando" : "Seleccionar";
    }

    private void SeleccionarTodas_Click(object sender, RoutedEventArgs e) =>
        SeleccionarMismoProveedor();

    private void DeseleccionarTodas_Click(object sender, RoutedEventArgs e)
    {
        SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
        ActualizarResumenMarcas();
    }

    private void SeleccionarCruzada_Click(object sender, RoutedEventArgs e) =>
        SeleccionarPatron(i => i % 2 == 0);

    private void SeleccionarArmonia_Click(object sender, RoutedEventArgs e) =>
        SeleccionarPatron(i => i % 3 != 2);

    private void MarcaFicha_Tapped(object sender, TappedRoutedEventArgs e) =>
        e.Handled = true;

    private void MarcaFicha_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja)
        {
            return;
        }

        _ignorarClicLista = true;
        var item = caja.DataContext as OrdenProductoItem
                   ?? _visibles.FirstOrDefault(f => Equals(caja.Tag, f.Id));
        if (item is null)
        {
            return;
        }

        if (caja.IsChecked == true)
        {
            if (!PuedeAgregar(item, out var mensaje))
            {
                item.EstaMarcada = false;
                caja.IsChecked = false;
                Mostrar(mensaje, InfoBarSeverity.Error);
                MostrarTipProveedor();
                ActualizarResumenMarcas();
                return;
            }

            item.EstaMarcada = true;
            _marcadas.Add(item.Id);
        }
        else
        {
            item.EstaMarcada = false;
            _marcadas.Remove(item.Id);
        }

        ActualizarResumenMarcas();
    }

    private void AplicarClicConProveedor(OrdenProductoItem item)
    {
        var indice = _visibles.FindIndex(v => v.Id == item.Id);
        if (indice < 0)
        {
            return;
        }

        var ctrl = SeleccionListaFichas.CtrlIzquierdoPulsado;
        var mayus = SeleccionListaFichas.MayusPulsado;

        if (mayus && _anclaSeleccion >= 0 && _anclaSeleccion < _visibles.Count)
        {
            var desde = Math.Min(_anclaSeleccion, indice);
            var hasta = Math.Max(_anclaSeleccion, indice);
            if (!ctrl)
            {
                SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
            }

            var rechazados = 0;
            for (var i = desde; i <= hasta; i++)
            {
                var candidato = _visibles[i];
                if (!PuedeAgregar(candidato, out _))
                {
                    rechazados++;
                    continue;
                }

                candidato.EstaMarcada = true;
                _marcadas.Add(candidato.Id);
            }

            if (rechazados > 0)
            {
                Mostrar(
                    "Solo se marcaron productos del mismo proveedor. Los demás se omitieron.",
                    InfoBarSeverity.Error);
                MostrarTipProveedor();
            }

            return;
        }

        if (ctrl)
        {
            if (item.EstaMarcada)
            {
                item.EstaMarcada = false;
                _marcadas.Remove(item.Id);
            }
            else if (!PuedeAgregar(item, out var mensaje))
            {
                Mostrar(mensaje, InfoBarSeverity.Error);
                MostrarTipProveedor();
            }
            else
            {
                item.EstaMarcada = true;
                _marcadas.Add(item.Id);
            }

            _anclaSeleccion = indice;
            return;
        }

        SeleccionListaFichas.DeseleccionarTodas(_visibles, _marcadas);
        item.EstaMarcada = true;
        _marcadas.Add(item.Id);
        _anclaSeleccion = indice;
    }

    private void SeleccionarMismoProveedor()
    {
        var proveedor = ProveedorDeSeleccion() ?? (_visibles.Count > 0 ? ClaveProveedor(_visibles[0].Ficha) : null);
        if (proveedor is null)
        {
            return;
        }

        var omitidos = 0;
        foreach (var item in _visibles)
        {
            if (MismoProveedor(ClaveProveedor(item.Ficha), proveedor))
            {
                item.EstaMarcada = true;
                _marcadas.Add(item.Id);
            }
            else
            {
                if (item.EstaMarcada)
                {
                    item.EstaMarcada = false;
                    _marcadas.Remove(item.Id);
                }

                omitidos++;
            }
        }

        ActualizarResumenMarcas();
        if (omitidos > 0)
        {
            Mostrar(
                $"Solo se marcaron productos de «{EtiquetaProveedor(proveedor)}». Los de otro proveedor se omitieron.",
                InfoBarSeverity.Warning);
            MostrarTipProveedor();
        }
    }

    private void SeleccionarPatron(Func<int, bool> incluir)
    {
        var proveedor = ProveedorDeSeleccion();
        var omitidos = 0;
        for (var i = 0; i < _visibles.Count; i++)
        {
            var item = _visibles[i];
            var debe = incluir(i);
            if (!debe)
            {
                item.EstaMarcada = false;
                _marcadas.Remove(item.Id);
                continue;
            }

            var clave = ClaveProveedor(item.Ficha);
            if (proveedor is null)
            {
                proveedor = clave;
            }

            if (!MismoProveedor(clave, proveedor))
            {
                item.EstaMarcada = false;
                _marcadas.Remove(item.Id);
                omitidos++;
                continue;
            }

            item.EstaMarcada = true;
            _marcadas.Add(item.Id);
        }

        ActualizarResumenMarcas();
        if (omitidos > 0)
        {
            Mostrar(
                "La selección solo incluye un proveedor. Los productos de otro proveedor se omitieron.",
                InfoBarSeverity.Warning);
            MostrarTipProveedor();
        }
    }

    private bool PuedeAgregar(OrdenProductoItem item, out string mensaje)
    {
        mensaje = string.Empty;
        if (_marcadas.Count == 0 || _marcadas.Contains(item.Id))
        {
            return true;
        }

        var proveedorActual = ProveedorDeSeleccion();
        if (proveedorActual is null)
        {
            return true;
        }

        var nuevo = ClaveProveedor(item.Ficha);
        if (MismoProveedor(nuevo, proveedorActual))
        {
            return true;
        }

        mensaje =
            $"Solo se puede sacar órdenes de compra con un solo proveedor. Ya hay productos de «{EtiquetaProveedor(proveedorActual)}».";
        return false;
    }

    private string? ProveedorDeSeleccion()
    {
        foreach (var id in _marcadas)
        {
            var item = _visibles.FirstOrDefault(v => v.Id == id);
            if (item is not null)
            {
                return ClaveProveedor(item.Ficha);
            }

            var ficha = App.Instance.Repositorio.Obtener(id);
            if (ficha is not null)
            {
                return ClaveProveedor(ficha);
            }
        }

        return null;
    }

    private static string ClaveProveedor(FichaTecnica ficha) =>
        string.IsNullOrWhiteSpace(ficha.Proveedor) ? string.Empty : ficha.Proveedor.Trim();

    private static bool MismoProveedor(string a, string b) =>
        string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);

    private static string EtiquetaProveedor(string clave) =>
        string.IsNullOrWhiteSpace(clave) ? "Sin proveedor" : clave;

    private void MostrarTipProveedor(bool forzar = true)
    {
        if (!forzar && _tipProveedorMostrado)
        {
            return;
        }

        TipProveedorUnico.IsOpen = false;
        TipProveedorUnico.Target = PanelLista.Visibility == Visibility.Visible ? ResumenMarcas : TituloLista;
        TipProveedorUnico.IsOpen = true;
        _tipProveedorMostrado = true;
    }

    private void TipProveedorUnico_CloseButtonClick(TeachingTip sender, object args) =>
        CerrarTipProveedor();

    private void TipProveedorUnico_Closed(TeachingTip sender, TeachingTipClosedEventArgs args) =>
        CerrarTipProveedor();

    private void CerrarTipProveedor()
    {
        TipProveedorUnico.IsOpen = false;
        TipProveedorUnico.Target = null;
    }

    private void ActualizarResumenMarcas()
    {
        if (string.IsNullOrWhiteSpace(_proveedorActivo))
        {
            return;
        }

        var marcadas = _visibles.Count(f => f.EstaMarcada);
        var proveedor = ProveedorDeSeleccion() ?? _proveedorActivo;
        if (marcadas == 0)
        {
            ResumenMarcas.Text =
                $"Productos de «{EtiquetaProveedor(proveedor)}». Marque los que entrarán en la orden.";
            return;
        }

        ResumenMarcas.Text =
            $"{marcadas} seleccionada(s) · {EtiquetaProveedor(proveedor)}. La orden usa este periodo para el consumo promedio.";
    }

    private void VerEnCatalogo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Guid id })
        {
            return;
        }

        App.Instance.MainAppWindow?.IrACatalogo(id);
    }

    private async void GenerarOrden_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proveedorActivo))
        {
            Mostrar("Primero busque y elija un producto o proveedor.", InfoBarSeverity.Warning);
            return;
        }

        var fichas = _visibles.Where(i => i.EstaMarcada).Select(i => i.Ficha).ToList();
        if (fichas.Count == 0)
        {
            Mostrar("Marque al menos un producto para generar la orden de compra.", InfoBarSeverity.Warning);
            return;
        }

        var proveedores = fichas
            .Select(ClaveProveedor)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (proveedores.Count > 1)
        {
            Mostrar(
                "Solo se puede sacar órdenes de compra con un solo proveedor. Quite los productos de otro proveedor.",
                InfoBarSeverity.Error);
            MostrarTipProveedor();
            return;
        }

        var lineas = OrdenCompraArmado.DesdeSeleccion(
            fichas, AnioParaFiltro(), MesesParaFiltro(), DesdeActual(), _atajoMeses);
        if (!OrdenCompraArmado.CumpleAcuerdoContrato(lineas, out var mensajeContrato))
        {
            var dialogo = new ContentDialog
            {
                Title = "Cantidad mínima por contrato",
                Content = mensajeContrato,
                PrimaryButtonText = "Usar cantidad mínima",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            OrdenCompraArmado.AjustarACantidadContrato(lineas);
        }

        Frame.Navigate(typeof(OrdenCompraWindow), lineas);
    }

    private void EditarBorradores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(BorradoresOrdenPage));

    private void VerAnteriores_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(RegistroOrdenesWindow));

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraAviso.Severity = severidad;
        BarraAviso.Message = mensaje;
        BarraAviso.IsOpen = true;
    }

    private sealed record OpcionAreaOrden(
        string Texto,
        Guid? Id,
        bool SinArea,
        FiltroStockCatalogo? Stock = null,
        FiltroCaducidadCatalogo? Caducidad = null)
    {
        public bool EsFiltroRapido => Stock is not null || Caducidad is not null;
    }
}

public sealed class SugerenciaOrdenCompra
{
    public SugerenciaOrdenCompra(
        bool esProveedor,
        string titulo,
        string detalle,
        string proveedor,
        Guid? fichaId,
        string textoCaja)
    {
        EsProveedor = esProveedor;
        Titulo = titulo;
        Detalle = detalle;
        Proveedor = proveedor;
        FichaId = fichaId;
        TextoCaja = textoCaja;
    }

    public bool EsProveedor { get; }
    public string Titulo { get; }
    public string Detalle { get; }
    public string Proveedor { get; }
    public Guid? FichaId { get; }
    public string TextoCaja { get; }

    public override string ToString() => Titulo;
}

public sealed class OrdenProductoItem : INotifyPropertyChanged, IItemMarcable
{
    private bool _estaMarcada;

    public Guid Id { get; init; }
    public required FichaTecnica Ficha { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required string Resumen { get; init; }
    public required string StockTexto { get; init; }
    public required string ConsumoTexto { get; init; }
    public required string PrecioTexto { get; init; }

    public bool EstaMarcada
    {
        get => _estaMarcada;
        set
        {
            if (_estaMarcada == value)
            {
                return;
            }

            _estaMarcada = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

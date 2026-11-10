using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed class ProveedorFila : INotifyPropertyChanged
{
    private bool _estaMarcada;
    private bool _mostrarMarca;

    public required string IdTexto { get; init; }
    public required string Nombre { get; init; }
    public required string Dato1 { get; init; }
    public required string Dato2 { get; init; }
    public required string Placeholder1 { get; init; }
    public required string Placeholder2 { get; init; }
    public required string Notas { get; init; }
    public required string Calificacion { get; init; }
    public required string FechaTexto { get; init; }
    public required string DetalleTexto { get; init; }
    public required string TextoBusqueda { get; init; }
    public IReadOnlyList<CanalDatoOpcion> Canales => CanalDatoProveedor.Opciones;
    public IReadOnlyList<string> Calificaciones => CalificacionProveedor.Todas;

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

    public bool MostrarMarca
    {
        get => _mostrarMarca;
        set
        {
            if (_mostrarMarca == value)
            {
                return;
            }

            _mostrarMarca = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

public sealed partial class ProveedoresPage : Page, IVistaInventario
{
    private readonly HashSet<int> _meses = [];
    private readonly HashSet<Guid> _marcados = [];
    private readonly HashSet<Guid> _omitidosSimilitud = [];
    private Dictionary<string, int>? _usosCache;
    private List<ProveedorFila> _visibles = [];
    private ReversionSimilitud? _ultimaReversion;
    private int? _atajoMeses;
    private bool _aplicandoFiltros;
    private bool _modoSeleccionar;
    private int _listaSuspendida;
    private int _total;
    private bool _syncPendiente;

    private sealed class ReversionSimilitud
    {
        public required IReadOnlyList<ProveedorRegistro> Proveedores { get; init; }
        public required IReadOnlyList<ProveedorFichaSync.InstantaneaContactoFicha> Fichas { get; init; }
    }

    public ProveedoresPage()
    {
        InitializeComponent();
        ConstruirMeses();
        _aplicandoFiltros = true;
        ComboCalificacion.ItemsSource = new List<string> { "Todas las calificaciones" }
            .Concat(CalificacionProveedor.Todas)
            .ToList();
        ComboCalificacion.SelectedIndex = 0;
        _aplicandoFiltros = false;
        ActualizarBotonRevertir();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        // Lista primero (rápida); sync de fichas en segundo plano para no bloquear la entrada.
        InvalidarUsos();
        RefrescarLista(recargarAnios: true);
        ActualizarBotonRevertir();
        _ = SincronizarEnSegundoPlanoAsync();
    }

    private async Task SincronizarEnSegundoPlanoAsync()
    {
        if (_syncPendiente)
        {
            return;
        }

        _syncPendiente = true;
        try
        {
            var agregados = await Task.Run(SincronizarFichas).ConfigureAwait(true);
            if (agregados > 0)
            {
                InvalidarUsos();
                RefrescarLista(true);
                Mostrar(
                    InfoBarSeverity.Informational,
                    agregados == 1
                        ? "Se agregó 1 proveedor desde las fichas técnicas, con sus redes, web y notas."
                        : $"Se agregaron {agregados} proveedores desde las fichas técnicas, con sus redes, web y notas.");
            }
        }
        finally
        {
            _syncPendiente = false;
        }
    }

    private void CajaBusqueda_TextChanged(object sender, TextChangedEventArgs e) => RefrescarLista(false);

    private void Filtro_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoFiltros)
        {
            return;
        }

        RefrescarLista(false);
    }

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_aplicandoFiltros)
        {
            return;
        }

        QuitarAtajoMeses();
        RefrescarLista(false);
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
        if (_aplicandoFiltros || sender is not ToggleButton chip || chip.Tag is not int mes)
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

        QuitarAtajoMeses();
        RefrescarLista(false);
    }

    private void TodosMeses_Click(object sender, RoutedEventArgs e)
    {
        _meses.Clear();
        _aplicandoFiltros = true;
        foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
        {
            hijo.IsChecked = false;
        }

        _aplicandoFiltros = false;
        QuitarAtajoMeses();
        RefrescarLista(false);
    }

    private void AtajoMeses_Click(object sender, RoutedEventArgs e)
    {
        if (_aplicandoFiltros || sender is not ToggleButton chip ||
            !int.TryParse(chip.Tag?.ToString(), out var meses))
        {
            return;
        }

        if (chip.IsChecked == true)
        {
            _atajoMeses = meses;
            _aplicandoFiltros = true;
            ComboAnio.SelectedIndex = 0;
            _meses.Clear();
            foreach (var hijo in PanelMeses.Children.OfType<ToggleButton>())
            {
                hijo.IsChecked = false;
            }

            _aplicandoFiltros = false;
        }
        else if (_atajoMeses == meses)
        {
            _atajoMeses = null;
        }

        ActualizarAtajos();
        RefrescarLista(false);
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
        _aplicandoFiltros = true;
        ChipUltimos3.IsChecked = _atajoMeses == 3;
        ChipUltimos6.IsChecked = _atajoMeses == 6;
        ChipUltimos12.IsChecked = _atajoMeses == 12;
        _aplicandoFiltros = false;
    }

    private void CargarAnios()
    {
        var actual = ComboAnio.SelectedItem as string;
        var opciones = new List<string> { "Todos los años" };
        foreach (var anio in App.Instance.Proveedores.Anios())
        {
            opciones.Add(anio.ToString());
        }

        var hoy = DateTime.Now.Year.ToString();
        if (!opciones.Contains(hoy))
        {
            opciones.Insert(1, hoy);
        }

        _aplicandoFiltros = true;
        ComboAnio.ItemsSource = opciones;
        if (actual is not null && opciones.Contains(actual))
        {
            ComboAnio.SelectedItem = actual;
        }
        else
        {
            ComboAnio.SelectedIndex = 0;
        }

        _aplicandoFiltros = false;
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

    private string? CalificacionActual()
    {
        if (ComboCalificacion.SelectedIndex <= 0 || ComboCalificacion.SelectedItem is not string texto)
        {
            return null;
        }

        return texto;
    }

    public void AplicarCambioInventario()
    {
        InvalidarUsos();
        _ = SincronizarEnSegundoPlanoAsync();
        RefrescarLista(false);
        AnimacionLista.Revelar(Lista);
    }

    private void RefrescarLista(bool recargarAnios)
    {
        if (recargarAnios)
        {
            CargarAnios();
        }

        var busqueda = (CajaBusqueda.Text ?? string.Empty).Trim();
        var calificacion = CalificacionActual();
        var anio = _atajoMeses is null ? AnioActual() : null;
        IReadOnlySet<int> meses = _atajoMeses is null ? _meses : new HashSet<int>();
        var desde = DesdeActual();
        var todos = App.Instance.Proveedores.Listar();
        _total = todos.Count;

        var usos = UsosPorNombre();
        _visibles = todos
            .Where(p => Pasa(p, busqueda, calificacion, anio, meses, desde))
            .Select(p => CrearFila(p, usos.TryGetValue(p.Nombre, out var n) ? n : 0))
            .ToList();

        SuspenderLista();
        Lista.ItemsSource = _visibles;
        ReanudarLista();

        var hayFiltro = busqueda.Length > 0 || calificacion is not null || anio is not null || meses.Count > 0 || desde is not null;
        TextoVacio.Visibility = _visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TextoVacio.Text = _total == 0
            ? "Aún no hay proveedores. Agregue uno o actualice desde las fichas técnicas."
            : "Ningún proveedor coincide con la búsqueda o el periodo.";
        TextoConteo.Text = hayFiltro
            ? $"{TextoCantidad(_visibles.Count)} de {TextoCantidad(_total)} · {FiltroPeriodoIngresos.Describir(anio, meses, desde, _atajoMeses)}"
            : $"{TextoCantidad(_total)} · {FiltroPeriodoIngresos.Describir(anio, meses, desde, _atajoMeses)}";
        ActualizarBarraSeleccion();
    }

    private static bool Pasa(
        ProveedorRegistro proveedor,
        string busqueda,
        string? calificacion,
        int? anio,
        IReadOnlySet<int> meses,
        DateTimeOffset? desde)
    {
        if (calificacion is not null &&
            !string.Equals(proveedor.Calificacion, calificacion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!FiltroPeriodoIngresos.Pasa(proveedor.FechaAgregado, anio, meses, desde))
        {
            return false;
        }

        if (busqueda.Length == 0)
        {
            return true;
        }

        return TextoBusqueda(proveedor).Contains(busqueda, StringComparison.CurrentCultureIgnoreCase);
    }

    private static string TextoBusqueda(ProveedorRegistro proveedor) =>
        string.Join(
            ' ',
            proveedor.Nombre,
            proveedor.RazonSocial,
            proveedor.Ruc,
            proveedor.Contacto,
            proveedor.Dato1,
            proveedor.Dato2,
            proveedor.Notas,
            proveedor.Calificacion,
            proveedor.Banco,
            proveedor.BancoOtro,
            proveedor.NumeroCuenta,
            proveedor.Cci,
            CanalDatoProveedor.De(proveedor.Canal1).Titulo,
            CanalDatoProveedor.De(proveedor.Canal2).Titulo);

    private Dictionary<string, int> UsosPorNombre()
    {
        if (_usosCache is not null)
        {
            return _usosCache;
        }

        _usosCache = App.Instance.Repositorio.Todas
            .Select(f => (f.Proveedor ?? string.Empty).Trim())
            .Where(nombre => nombre.Length > 0)
            .GroupBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return _usosCache;
    }

    private void InvalidarUsos() => _usosCache = null;

    private ProveedorFila CrearFila(ProveedorRegistro proveedor, int usos) => new()
    {
        IdTexto = proveedor.Id.ToString(),
        Nombre = proveedor.Nombre,
        Dato1 = proveedor.Dato1,
        Dato2 = proveedor.Dato2,
        Placeholder1 = CanalDatoProveedor.De(proveedor.Canal1).Placeholder,
        Placeholder2 = CanalDatoProveedor.De(proveedor.Canal2).Placeholder,
        Notas = proveedor.Notas,
        Calificacion = proveedor.Calificacion,
        FechaTexto = proveedor.FechaAgregado.ToLocalTime().ToString("dd/MM/yyyy"),
        DetalleTexto = ConstruirDetalle(proveedor, usos),
        TextoBusqueda = TextoBusqueda(proveedor),
        EstaMarcada = _marcados.Contains(proveedor.Id),
        MostrarMarca = _modoSeleccionar
    };

    private static string ConstruirDetalle(ProveedorRegistro proveedor, int usos)
    {
        var partes = new List<string>
        {
            usos == 0 ? "Sin fichas" : usos == 1 ? "1 ficha" : $"{usos} fichas"
        };

        if (proveedor.Ruc.Length > 0 || proveedor.RazonSocial.Length > 0)
        {
            partes.Add(proveedor.TextoEmpresa);
        }
        else
        {
            partes.Add("Sin RUC");
        }

        if (proveedor.TiempoEntregaDias > 0)
        {
            partes.Add(proveedor.TiempoEntregaDias == 1
                ? "Entrega 1 día"
                : $"Entrega {proveedor.TiempoEntregaDias} días");
        }
        else
        {
            partes.Add("Sin entrega");
        }

        if (proveedor.AcuerdoContrato && proveedor.CantidadContrato > 0)
        {
            var cantidad = proveedor.CantidadContrato.ToString(
                "0.##",
                System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            var unidad = proveedor.UnidadContrato.Length == 0 ? "unidades" : proveedor.UnidadContrato;
            partes.Add($"Mín. {cantidad} {unidad}");
        }

        partes.Add(BancoProveedor.Normalizar(proveedor.Banco).Length > 0
            ? proveedor.TextoBanco
            : "Sin banco");

        return string.Join(" · ", partes);
    }

    private static string TextoCantidad(int cantidad) =>
        cantidad == 1 ? "1 proveedor" : $"{cantidad} proveedores";

    private void ModoSeleccionar_Click(object sender, RoutedEventArgs e)
    {
        _modoSeleccionar = BotonModoSeleccionar.IsChecked == true;
        BotonModoSeleccionar.Content = _modoSeleccionar ? "Seleccionando" : "Seleccionar";
        var vis = _modoSeleccionar ? Visibility.Visible : Visibility.Collapsed;
        BotonSeleccionarTodos.Visibility = vis;
        BotonDeseleccionarTodos.Visibility = vis;
        BotonEliminarSeleccionados.Visibility = vis;
        if (!_modoSeleccionar)
        {
            _marcados.Clear();
        }

        foreach (var fila in _visibles)
        {
            fila.MostrarMarca = _modoSeleccionar;
            if (!_modoSeleccionar)
            {
                fila.EstaMarcada = false;
            }
        }

        ActualizarBarraSeleccion();
    }

    private void SeleccionarTodos_Click(object sender, RoutedEventArgs e)
    {
        if (!_modoSeleccionar)
        {
            return;
        }

        foreach (var fila in _visibles)
        {
            if (!Guid.TryParse(fila.IdTexto, out var id))
            {
                continue;
            }

            fila.EstaMarcada = true;
            _marcados.Add(id);
        }

        ActualizarBarraSeleccion();
    }

    private void DeseleccionarTodos_Click(object sender, RoutedEventArgs e)
    {
        if (!_modoSeleccionar)
        {
            return;
        }

        _marcados.Clear();
        foreach (var fila in _visibles)
        {
            fila.EstaMarcada = false;
        }

        ActualizarBarraSeleccion();
    }

    private void MarcaProveedor_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox caja || !TryId(caja, out var id))
        {
            return;
        }

        if (caja.IsChecked == true)
        {
            _marcados.Add(id);
        }
        else
        {
            _marcados.Remove(id);
        }

        var fila = _visibles.FirstOrDefault(f => Guid.TryParse(f.IdTexto, out var fid) && fid == id);
        if (fila is not null)
        {
            fila.EstaMarcada = caja.IsChecked == true;
        }

        ActualizarBarraSeleccion();
    }

    private void ActualizarBarraSeleccion()
    {
        var n = _marcados.Count;
        BotonEliminarSeleccionados.IsEnabled = _modoSeleccionar && n > 0;
        BotonEliminarSeleccionados.Content = n == 0
            ? "Eliminar seleccionados"
            : n == 1
                ? "Eliminar 1 seleccionado"
                : $"Eliminar {n} seleccionados";
    }

    private async void EliminarSeleccionados_Click(object sender, RoutedEventArgs e)
    {
        var ids = _marcados.ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var nombres = ids
            .Select(id => App.Instance.Proveedores.Obtener(id)?.Nombre)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList();
        var dialogo = new ContentDialog
        {
            Title = ids.Count == 1 ? "Eliminar proveedor" : "Eliminar proveedores",
            Content = ids.Count == 1
                ? $"¿Eliminar «{nombres.FirstOrDefault()}»? Se quita de esta lista, del historial y de las fichas que lo usan."
                : $"¿Eliminar {ids.Count} proveedores seleccionados? Se quitan de esta lista, del historial y de las fichas que los usan.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var borrados = 0;
        foreach (var id in ids)
        {
            if (App.Instance.Proveedores.Obtener(id) is not { } proveedor)
            {
                continue;
            }

            ProveedorFichaSync.Quitar(proveedor.Nombre);
            if (App.Instance.Proveedores.Eliminar(id))
            {
                borrados++;
            }
        }

        _marcados.Clear();
        InvalidarUsos();
        RefrescarLista(true);
        Mostrar(
            InfoBarSeverity.Success,
            borrados == 1
                ? "Proveedor eliminado de la lista y de las fichas técnicas."
                : $"{borrados} proveedores eliminados de la lista y de las fichas técnicas.");
    }

    private async void Agregar_Click(object sender, RoutedEventArgs e) =>
        await EditarAsync(null);

    private async void Editar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Proveedores.Obtener(id) is not { } proveedor)
        {
            return;
        }

        await EditarAsync(proveedor);
    }

    private async Task EditarAsync(ProveedorRegistro? origen)
    {
        var esNuevo = origen is null;
        var guardado = await ProveedorEditorDialogo.MostrarAsync(XamlRoot, origen);
        if (guardado is null)
        {
            return;
        }

        InvalidarUsos();
        RefrescarLista(true);
        Mostrar(
            InfoBarSeverity.Success,
            esNuevo
                ? "Proveedor agregado. También queda en el historial de la ficha técnica."
                : "Proveedor actualizado en esta lista y en las fichas que lo usan.");
    }

    private async void Eliminar_Click(object sender, RoutedEventArgs e)
    {
        if (!TryId(sender, out var id) || App.Instance.Proveedores.Obtener(id) is not { } proveedor)
        {
            return;
        }

        var fichas = ProveedorFichaSync.Contar(proveedor.Nombre);
        var detalle = fichas == 0
            ? "Se quita de esta lista y del historial de la ficha técnica."
            : fichas == 1
                ? "Se quita de 1 ficha técnica y del historial. El producto conserva el resto de sus datos."
                : $"Se quita de {fichas} fichas técnicas y del historial. Los productos conservan el resto de sus datos.";
        var dialogo = new ContentDialog
        {
            Title = "Eliminar proveedor",
            Content = $"¿Eliminar «{proveedor.Nombre}»? {detalle} También se borran sus notas y su calificación.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ProveedorFichaSync.Quitar(proveedor.Nombre);
        App.Instance.Proveedores.Eliminar(id);
        _marcados.Remove(id);
        InvalidarUsos();
        RefrescarLista(true);
        Mostrar(InfoBarSeverity.Success, "Proveedor eliminado de la lista y de las fichas técnicas.");
    }

    private void Notas_LostFocus(object sender, RoutedEventArgs e)
    {
        if (ListaSuspendida || sender is not TextBox caja || !TryId(caja, out var id))
        {
            return;
        }

        App.Instance.Proveedores.ActualizarNotas(id, caja.Text);
    }

    private void Canal1_Loaded(object sender, RoutedEventArgs e) => CargarCanal(sender, 1);

    private void Canal2_Loaded(object sender, RoutedEventArgs e) => CargarCanal(sender, 2);

    private void CargarCanal(object sender, int indice)
    {
        if (sender is not ComboBox combo || !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var canal = CanalDatoProveedor.De(indice == 2 ? proveedor.Canal2 : proveedor.Canal1);
        var indiceOpcion = 0;
        for (var i = 0; i < CanalDatoProveedor.Opciones.Count; i++)
        {
            if (CanalDatoProveedor.Opciones[i].Id == canal.Id)
            {
                indiceOpcion = i;
                break;
            }
        }

        SuspenderLista();
        combo.SelectedIndex = indiceOpcion;
        ReanudarLista();
        if (combo.Parent is Grid grid)
        {
            foreach (var hijo in grid.Children.OfType<TextBox>())
            {
                hijo.PlaceholderText = canal.Placeholder;
            }
        }
    }

    private void Canal1_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GuardarCanal(sender, 1);

    private void Canal2_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GuardarCanal(sender, 2);

    private void GuardarCanal(object sender, int indice)
    {
        if (ListaSuspendida || sender is not ComboBox combo || combo.SelectedItem is not CanalDatoOpcion opcion ||
            !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var actual = indice == 2 ? proveedor.Canal2 : proveedor.Canal1;
        if (string.Equals(actual, opcion.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (combo.Parent is Grid grid)
        {
            foreach (var hijo in grid.Children.OfType<TextBox>())
            {
                hijo.PlaceholderText = opcion.Placeholder;
            }
        }

        var nombre = proveedor.Nombre;
        if (!App.Instance.Proveedores.ActualizarContacto(id, indice, opcion.Id, null, cambiarCanal: true, cambiarDato: false))
        {
            return;
        }

        if (App.Instance.Proveedores.Obtener(id) is { } actualizado)
        {
            ProveedorFichaSync.Publicar(nombre, actualizado);
        }
    }

    private void Dato1_LostFocus(object sender, RoutedEventArgs e) => GuardarDato(sender, 1);

    private void Dato2_LostFocus(object sender, RoutedEventArgs e) => GuardarDato(sender, 2);

    private void GuardarDato(object sender, int indice)
    {
        if (ListaSuspendida || sender is not TextBox caja || !TryId(caja, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        var actual = indice == 2 ? proveedor.Dato2 : proveedor.Dato1;
        if (string.Equals(actual, caja.Text.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        var nombre = proveedor.Nombre;
        if (!App.Instance.Proveedores.ActualizarContacto(id, indice, null, caja.Text, cambiarCanal: false, cambiarDato: true))
        {
            return;
        }

        if (App.Instance.Proveedores.Obtener(id) is { } actualizado)
        {
            ProveedorFichaSync.Publicar(nombre, actualizado);
        }
    }

    private async void AbrirCanal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Parent: Grid grid })
        {
            return;
        }

        var combo = grid.Children.OfType<ComboBox>().FirstOrDefault();
        var caja = grid.Children.OfType<TextBox>().FirstOrDefault();
        var canal = combo?.SelectedItem is CanalDatoOpcion opcion
            ? opcion.Id
            : CanalDatoProveedor.Web;
        var dato = caja?.Text ?? string.Empty;
        var uri = CanalDatoProveedor.Uri(canal, dato);
        if (uri is null)
        {
            var titulo = CanalDatoProveedor.De(canal).Titulo;
            Mostrar(
                InfoBarSeverity.Warning,
                titulo == "WhatsApp"
                    ? "Indique un celular peruano de 9 dígitos (empieza en 9)."
                    : $"Escriba un {titulo.ToLowerInvariant()} válido.");
            return;
        }

        if (!await Windows.System.Launcher.LaunchUriAsync(uri))
        {
            Mostrar(InfoBarSeverity.Error, $"No se pudo abrir {CanalDatoProveedor.De(canal).Titulo}.");
        }
    }

    private void ComboCalificacion_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null)
        {
            return;
        }

        SuspenderLista();
        combo.SelectedItem = CalificacionProveedor.Normalizar(proveedor.Calificacion);
        ReanudarLista();
    }

    private void Calificacion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaSuspendida || sender is not ComboBox combo || combo.SelectedItem is not string elegida ||
            !TryId(combo, out var id))
        {
            return;
        }

        var proveedor = App.Instance.Proveedores.Obtener(id);
        if (proveedor is null ||
            string.Equals(proveedor.Calificacion, elegida, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        App.Instance.Proveedores.ActualizarCalificacion(id, elegida);
        var filtro = CalificacionActual();
        if (filtro is not null && !string.Equals(filtro, elegida, StringComparison.OrdinalIgnoreCase))
        {
            RefrescarLista(false);
        }
    }

    private void TraerProductos_Click(object sender, RoutedEventArgs e)
    {
        var agregados = SincronizarFichas();
        InvalidarUsos();
        RefrescarLista(true);
        Mostrar(
            agregados > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Informational,
            agregados > 0
                ? (agregados == 1
                    ? "Se agregó 1 proveedor desde las fichas técnicas."
                    : $"Se agregaron {agregados} proveedores desde las fichas técnicas.")
                : "Los proveedores ya coinciden con las fichas técnicas.");
    }

    private async void BuscarSimilitudes_Click(object sender, RoutedEventArgs e) =>
        await MostrarSimilitudesAsync();

    private async Task MostrarSimilitudesAsync()
    {
        var omitidosGrupos = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var grupos = SimilitudProveedores.Buscar(App.Instance.Proveedores.Listar())
                .Select(FiltrarOmitidosIndividuales)
                .Where(g => g.Proveedores.Count > 1 && !omitidosGrupos.Contains(ClaveGrupoSimilitud(g)))
                .ToList();
            if (grupos.Count == 0)
            {
                Mostrar(
                    InfoBarSeverity.Informational,
                    omitidosGrupos.Count == 0 && _omitidosSimilitud.Count == 0
                        ? "No hay nombres parecidos entre proveedores. Si agrega variantes nuevas, vuelva a buscar."
                        : "No quedan más grupos por revisar. Los omitidos se dejaron sin cambiar.");
                return;
            }

            var usos = UsosPorNombre();
            var elegidos = new Dictionary<int, Guid?>();
            var personalizados = new Dictionary<int, TextBox>();
            var radiosPersonalizados = new Dictionary<int, RadioButton>();
            var omitidosLocales = new Dictionary<int, HashSet<Guid>>();
            var listaGrupos = new StackPanel { Spacing = 12, MinWidth = 480 };
            listaGrupos.Children.Add(new TextBlock
            {
                Text = "Elija un nombre existente, escriba uno nuevo en la caja, o «No reemplazar» si son distintos. «Omitir» deja fuera a un proveedor del reemplazo.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.85
            });

            for (var i = 0; i < grupos.Count; i++)
            {
                var indiceGrupo = i;
                var grupo = grupos[i];
                omitidosLocales[indiceGrupo] = [];
                var tarjeta = new StackPanel { Spacing = 6 };
                tarjeta.Children.Add(new TextBlock
                {
                    Text = grupos.Count == 1
                        ? "Nombres parecidos"
                        : $"Grupo {i + 1} de {grupos.Count}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                var omitirGrupo = new RadioButton
                {
                    Content = "No reemplazar (dejarlos como están)",
                    Tag = null,
                    GroupName = $"similitud-{indiceGrupo}",
                    Margin = new Thickness(0, 0, 0, 2),
                    IsChecked = true
                };
                omitirGrupo.Checked += (_, _) =>
                {
                    if (omitirGrupo.IsChecked == true)
                    {
                        elegidos[indiceGrupo] = null;
                    }
                };
                elegidos[indiceGrupo] = null;
                tarjeta.Children.Add(omitirGrupo);

                foreach (var proveedor in grupo.Proveedores)
                {
                    usos.TryGetValue(proveedor.Nombre, out var n);
                    var detalle = n == 0
                        ? "sin fichas"
                        : n == 1 ? "1 ficha" : $"{n} fichas";
                    var extra = proveedor.Ruc.Length == 11 ? $" · RUC {proveedor.Ruc}" : string.Empty;
                    var fila = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 2) };
                    fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var radio = new RadioButton
                    {
                        Content = $"Usar «{proveedor.Nombre}» ({detalle}{extra})",
                        Tag = proveedor.Id,
                        GroupName = $"similitud-{indiceGrupo}",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    radio.Checked += (_, _) =>
                    {
                        if (radio.IsChecked == true && radio.Tag is Guid id)
                        {
                            elegidos[indiceGrupo] = id;
                        }
                    };
                    Grid.SetColumn(radio, 0);
                    fila.Children.Add(radio);

                    var botonOmitir = new Button
                    {
                        Content = "Omitir",
                        Tag = proveedor.Id,
                        Padding = new Thickness(10, 2, 10, 2),
                        MinHeight = 28,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ToolTipService.SetToolTip(botonOmitir, "No reemplazar este proveedor; se deja fuera del grupo.");
                    var idOmitir = proveedor.Id;
                    var nombreOmitir = proveedor.Nombre;
                    botonOmitir.Click += (_, _) =>
                    {
                        _omitidosSimilitud.Add(idOmitir);
                        omitidosLocales[indiceGrupo].Add(idOmitir);
                        fila.Opacity = 0.45;
                        botonOmitir.IsEnabled = false;
                        botonOmitir.Content = "Omitido";
                        radio.IsEnabled = false;
                        if (radio.IsChecked == true)
                        {
                            omitirGrupo.IsChecked = true;
                        }

                        Mostrar(
                            InfoBarSeverity.Informational,
                            $"«{nombreOmitir}» se omitió del reemplazo. Puede seguir eligiendo entre los demás.");
                    };
                    Grid.SetColumn(botonOmitir, 1);
                    fila.Children.Add(botonOmitir);

                    tarjeta.Children.Add(fila);
                }

                var sugerido = grupo.Proveedores
                    .OrderByDescending(p => usos.TryGetValue(p.Nombre, out var n) ? n : 0)
                    .Select(p => p.Nombre)
                    .FirstOrDefault() ?? string.Empty;
                var cajaNombre = new TextBox
                {
                    PlaceholderText = "Escriba el nombre con el que quiere unificar este grupo",
                    Text = sugerido,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                var radioPersonalizado = new RadioButton
                {
                    Content = "Usar el nombre escrito abajo",
                    GroupName = $"similitud-{indiceGrupo}",
                    Margin = new Thickness(0, 4, 0, 0)
                };
                radioPersonalizado.Checked += (_, _) =>
                {
                    if (radioPersonalizado.IsChecked == true)
                    {
                        elegidos[indiceGrupo] = Guid.Empty;
                    }
                };
                cajaNombre.GotFocus += (_, _) =>
                {
                    radioPersonalizado.IsChecked = true;
                };
                cajaNombre.TextChanged += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(cajaNombre.Text))
                    {
                        radioPersonalizado.IsChecked = true;
                    }
                };
                personalizados[indiceGrupo] = cajaNombre;
                radiosPersonalizados[indiceGrupo] = radioPersonalizado;
                tarjeta.Children.Add(radioPersonalizado);
                tarjeta.Children.Add(cajaNombre);

                listaGrupos.Children.Add(tarjeta);
            }

            var dialogo = new ContentDialog
            {
                Title = "Similitudes entre proveedores",
                Content = new ScrollViewer
                {
                    Content = listaGrupos,
                    MaxHeight = 520
                },
                PrimaryButtonText = "Aplicar",
                CloseButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var planes = new List<(int Indice, HashSet<Guid> Omitidos, Guid? IdConservar, string? NombrePersonalizado)>();
            var omitidosAhora = 0;
            for (var i = 0; i < grupos.Count; i++)
            {
                omitidosLocales.TryGetValue(i, out var omitidosDelGrupo);
                omitidosDelGrupo ??= [];
                elegidos.TryGetValue(i, out var idConservar);
                var usaPersonalizado = idConservar == Guid.Empty
                    || (radiosPersonalizados.TryGetValue(i, out var radioPers) && radioPers.IsChecked == true);
                var nombrePersonalizado = personalizados.TryGetValue(i, out var caja)
                    ? (caja.Text ?? string.Empty).Trim()
                    : string.Empty;

                if (idConservar is null && !usaPersonalizado)
                {
                    omitidosGrupos.Add(ClaveGrupoSimilitud(grupos[i]));
                    omitidosAhora++;
                    continue;
                }

                if (usaPersonalizado)
                {
                    if (nombrePersonalizado.Length == 0)
                    {
                        omitidosGrupos.Add(ClaveGrupoSimilitud(grupos[i]));
                        omitidosAhora++;
                        continue;
                    }

                    planes.Add((i, omitidosDelGrupo, null, nombrePersonalizado));
                    continue;
                }

                if (idConservar is null || omitidosDelGrupo.Contains(idConservar.Value))
                {
                    omitidosGrupos.Add(ClaveGrupoSimilitud(grupos[i]));
                    omitidosAhora++;
                    continue;
                }

                planes.Add((i, omitidosDelGrupo, idConservar, null));
            }

            if (planes.Count == 0)
            {
                Mostrar(
                    InfoBarSeverity.Informational,
                    omitidosAhora > 0 || _omitidosSimilitud.Count > 0
                        ? "No se reemplazó ningún grupo. Los omitidos o marcados como distintos se dejaron igual."
                        : "No se aplicó ningún reemplazo.");
                continue;
            }

            var idsAfectados = new HashSet<Guid>();
            var nombresAfectados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in planes)
            {
                foreach (var p in grupos[plan.Indice].Proveedores)
                {
                    if (plan.Omitidos.Contains(p.Id) || _omitidosSimilitud.Contains(p.Id))
                    {
                        continue;
                    }

                    idsAfectados.Add(p.Id);
                    if (!string.IsNullOrWhiteSpace(p.Nombre))
                    {
                        nombresAfectados.Add(p.Nombre.Trim());
                    }
                }

                if (!string.IsNullOrWhiteSpace(plan.NombrePersonalizado))
                {
                    nombresAfectados.Add(plan.NombrePersonalizado!);
                    var destinoExistente = App.Instance.Proveedores.Listar()
                        .FirstOrDefault(p => string.Equals(p.Nombre, plan.NombrePersonalizado, StringComparison.OrdinalIgnoreCase));
                    if (destinoExistente is not null)
                    {
                        idsAfectados.Add(destinoExistente.Id);
                    }
                }
                else if (plan.IdConservar is Guid idCons && App.Instance.Proveedores.Obtener(idCons) is { } cons)
                {
                    idsAfectados.Add(cons.Id);
                    if (!string.IsNullOrWhiteSpace(cons.Nombre))
                    {
                        nombresAfectados.Add(cons.Nombre.Trim());
                    }
                }
            }

            var reversion = CapturarReversion(idsAfectados, nombresAfectados);
            var reemplazos = 0;
            var quitados = 0;
            foreach (var plan in planes)
            {
                if (!string.IsNullOrWhiteSpace(plan.NombrePersonalizado))
                {
                    quitados += UnificarGrupoConNombre(
                        grupos[plan.Indice].Proveedores,
                        plan.NombrePersonalizado!,
                        plan.Omitidos,
                        ref reemplazos);
                    continue;
                }

                if (plan.IdConservar is not Guid idConservar ||
                    App.Instance.Proveedores.Obtener(idConservar) is not { } conservar)
                {
                    continue;
                }

                foreach (var otro in grupos[plan.Indice].Proveedores.Where(p => p.Id != idConservar))
                {
                    if (plan.Omitidos.Contains(otro.Id) || _omitidosSimilitud.Contains(otro.Id))
                    {
                        continue;
                    }

                    var actual = App.Instance.Proveedores.Obtener(otro.Id);
                    if (actual is null)
                    {
                        continue;
                    }

                    ProveedorFichaSync.Publicar(actual.Nombre, conservar);
                    if (App.Instance.Proveedores.Eliminar(actual.Id))
                    {
                        quitados++;
                        reemplazos++;
                    }
                }
            }

            InvalidarUsos();
            RefrescarLista(true);
            if (reemplazos == 0)
            {
                Mostrar(
                    InfoBarSeverity.Informational,
                    omitidosAhora > 0 || _omitidosSimilitud.Count > 0
                        ? "No se reemplazó ningún grupo. Los omitidos o marcados como distintos se dejaron igual."
                        : "No se aplicó ningún reemplazo.");
                return;
            }

            _ultimaReversion = reversion;
            ActualizarBotonRevertir();
            Mostrar(
                InfoBarSeverity.Success,
                quitados == 1
                    ? "Se unificó 1 proveedor con el nombre elegido. Puede revertir el último cambio si se equivocó."
                    : $"Se unificaron {quitados} proveedores con los nombres elegidos. Puede revertir el último cambio si se equivocó.");

            var quedan = SimilitudProveedores.Buscar(App.Instance.Proveedores.Listar())
                .Select(FiltrarOmitidosIndividuales)
                .Count(g => g.Proveedores.Count > 1 && !omitidosGrupos.Contains(ClaveGrupoSimilitud(g)));
            if (quedan == 0)
            {
                return;
            }
        }
    }

    private void ActualizarBotonRevertir() =>
        BotonRevertirSimilitud.IsEnabled = _ultimaReversion is not null;

    private async void RevertirUltimoCambio_Click(object sender, RoutedEventArgs e)
    {
        if (_ultimaReversion is null)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Revertir último cambio",
            Content = "Se deshará el último reemplazo de similitudes: vuelven los proveedores eliminados o renombrados y las fichas a como estaban.",
            PrimaryButtonText = "Revertir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        foreach (var proveedor in _ultimaReversion.Proveedores)
        {
            App.Instance.Proveedores.Restaurar(ProveedoresRepository.Copiar(proveedor));
        }

        ProveedorFichaSync.RestaurarFichas(_ultimaReversion.Fichas);
        foreach (var proveedor in _ultimaReversion.Proveedores)
        {
            AlmacenHistorialProveedores.ReemplazarPorNombre(
                proveedor.Nombre,
                ContactoProveedor.Crear(
                    proveedor.Nombre,
                    proveedor.Canal1,
                    proveedor.Dato1,
                    proveedor.Canal2,
                    proveedor.Dato2));
        }

        _ultimaReversion = null;
        ActualizarBotonRevertir();
        InvalidarUsos();
        RefrescarLista(true);
        Mostrar(InfoBarSeverity.Success, "Se revirtió el último cambio de similitudes.");
    }

    private static ReversionSimilitud CapturarReversion(
        IReadOnlyCollection<Guid> idsProveedores,
        IReadOnlyCollection<string> nombresProveedores)
    {
        var proveedores = App.Instance.Proveedores.Listar()
            .Where(p => idsProveedores.Contains(p.Id))
            .Select(ProveedoresRepository.Copiar)
            .ToList();

        var nombres = new HashSet<string>(nombresProveedores, StringComparer.OrdinalIgnoreCase);
        foreach (var p in proveedores)
        {
            if (!string.IsNullOrWhiteSpace(p.Nombre))
            {
                nombres.Add(p.Nombre.Trim());
            }
        }

        var fichas = App.Instance.Repositorio.Todas
            .Where(f => nombres.Contains((f.Proveedor ?? string.Empty).Trim()))
            .Select(ProveedorFichaSync.CapturarFicha)
            .ToList();

        return new ReversionSimilitud
        {
            Proveedores = proveedores,
            Fichas = fichas
        };
    }

    private int UnificarGrupoConNombre(
        IReadOnlyList<ProveedorRegistro> miembros,
        string nombreFinal,
        HashSet<Guid> omitidosDelGrupo,
        ref int reemplazos)
    {
        var activos = miembros
            .Where(p => !omitidosDelGrupo.Contains(p.Id) && !_omitidosSimilitud.Contains(p.Id))
            .Select(p => App.Instance.Proveedores.Obtener(p.Id))
            .Where(p => p is not null)
            .Cast<ProveedorRegistro>()
            .ToList();
        if (activos.Count == 0)
        {
            return 0;
        }

        var usos = UsosPorNombre();
        var destino = App.Instance.Proveedores.Listar()
            .FirstOrDefault(p =>
                string.Equals(p.Nombre, nombreFinal, StringComparison.OrdinalIgnoreCase));
        if (destino is null)
        {
            destino = activos
                .OrderByDescending(p => usos.TryGetValue(p.Nombre, out var n) ? n : 0)
                .ThenBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .First();
            var nombreAnterior = destino.Nombre;
            if (!string.Equals(nombreAnterior, nombreFinal, StringComparison.OrdinalIgnoreCase))
            {
                destino.Nombre = nombreFinal;
                if (!App.Instance.Proveedores.Guardar(destino))
                {
                    destino.Nombre = nombreAnterior;
                    return 0;
                }

                ProveedorFichaSync.Publicar(nombreAnterior, destino);
                reemplazos++;
            }
        }

        var quitados = 0;
        foreach (var otro in activos.Where(p => p.Id != destino.Id))
        {
            var actual = App.Instance.Proveedores.Obtener(otro.Id);
            if (actual is null)
            {
                continue;
            }

            ProveedorFichaSync.Publicar(actual.Nombre, destino);
            if (App.Instance.Proveedores.Eliminar(actual.Id))
            {
                quitados++;
                reemplazos++;
            }
        }

        return quitados;
    }

    private GrupoSimilitudProveedores FiltrarOmitidosIndividuales(GrupoSimilitudProveedores grupo)
    {
        if (_omitidosSimilitud.Count == 0)
        {
            return grupo;
        }

        return new GrupoSimilitudProveedores
        {
            Proveedores = grupo.Proveedores.Where(p => !_omitidosSimilitud.Contains(p.Id)).ToList()
        };
    }

    private static string ClaveGrupoSimilitud(GrupoSimilitudProveedores grupo) =>
        string.Join("|", grupo.Proveedores.Select(p => p.Id).OrderBy(id => id));

    private static int SincronizarFichas() =>
        App.Instance.Proveedores.SincronizarDesdeFichas(
            App.Instance.Repositorio.Todas,
            AlmacenHistorialProveedores.Guardados);

    private bool ListaSuspendida => _listaSuspendida > 0;

    private void SuspenderLista() => _listaSuspendida++;

    private void ReanudarLista() => _listaSuspendida = Math.Max(0, _listaSuspendida - 1);

    private static bool TryId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is FrameworkElement { Tag: string texto } && Guid.TryParse(texto, out id);
    }

    private void Mostrar(InfoBarSeverity gravedad, string mensaje)
    {
        Barra.Severity = gravedad;
        Barra.Message = mensaje;
        Barra.IsOpen = true;
    }
}

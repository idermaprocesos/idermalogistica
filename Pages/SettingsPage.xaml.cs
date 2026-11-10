using Iderma.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace IdermaFichas.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private IReadOnlyList<ListaDesplegable> _listasArea = [];

    private bool _cargandoPreferencias;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        CargarIconosArea();
        ActualizarResumen();
        CargarAreas();
        CargarListasDelArea();
        CargarPreferencias();
        CargarListaCopias();
        ActualizarResumenMedicacion();
    }

    private async void ComprobarBase_Click(object sender, RoutedEventArgs e)
    {
        BotonComprobarBase.IsEnabled = false;
        AnilloBase.IsActive = true;
        AnilloBase.Visibility = Visibility.Visible;
        BarraBase.IsOpen = false;
        TextoErrorBase.Visibility = Visibility.Collapsed;
        TextoSolucionBase.Visibility = Visibility.Collapsed;

        try
        {
            var resultado = await DiagnosticoAccesoBase.EjecutarAsync();
            BarraBase.Severity = resultado.Correcto ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            BarraBase.Title = "Base de datos";
            BarraBase.Message = resultado.Resumen;
            BarraBase.IsOpen = true;

            if (!string.IsNullOrWhiteSpace(resultado.Error))
            {
                TextoErrorBase.Text = "Error: " + resultado.Error;
                TextoErrorBase.Visibility = Visibility.Visible;
            }

            if (!string.IsNullOrWhiteSpace(resultado.Solucion))
            {
                TextoSolucionBase.Text = resultado.Correcto
                    ? resultado.Solucion
                    : "Cómo solucionarlo: " + resultado.Solucion;
                TextoSolucionBase.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            BarraBase.Severity = InfoBarSeverity.Error;
            BarraBase.Title = "Base de datos";
            BarraBase.Message = "La comprobación no pudo terminar.";
            BarraBase.IsOpen = true;
            TextoErrorBase.Text = "Error: " + ex.Message;
            TextoErrorBase.Visibility = Visibility.Visible;
            TextoSolucionBase.Text = "Cierra logística, ábrela de nuevo y repite la comprobación. Si el mensaje se repite, compártelo con quien administra Iderma.";
            TextoSolucionBase.Visibility = Visibility.Visible;
        }
        finally
        {
            AnilloBase.IsActive = false;
            AnilloBase.Visibility = Visibility.Collapsed;
            BotonComprobarBase.IsEnabled = true;
        }
    }

    public void RecargarAreas() => CargarAreas();

    public void RecargarDesplegables() => CargarListasDelArea();

    private void CargarAreas(Guid? seleccionarArea = null, Guid? seleccionarCategoria = null)
    {
        var categoriaLista = seleccionarCategoria
            ?? (ListaCategorias.SelectedItem as CategoriaMenuDefinicion)?.Id;
        var categorias = AreasOperativas.Categorias.ToList();
        ListaCategorias.ItemsSource = categorias;
        ComboCategoriaArea.ItemsSource = categorias;

        var areaLista = seleccionarArea
            ?? (ListaAreas.SelectedItem as AreaListaVista)?.Id
            ?? (ComboAreaListas.SelectedItem as AreaDefinicion)?.Id;
        var areas = AreasOperativas.Todas.ToList();
        var vistas = areas.Select(a => new AreaListaVista(
            a.Id,
            a.Nombre,
            a.Glifo,
            AreasOperativas.TituloCategoria(a.CategoriaId),
            a)).ToList();
        ListaAreas.ItemsSource = vistas;
        ComboAreaListas.ItemsSource = areas;

        if (categoriaLista is Guid idCat)
        {
            var cat = categorias.FirstOrDefault(c => c.Id == idCat);
            if (cat is not null)
            {
                ListaCategorias.SelectedItem = cat;
            }
        }
        else if (categorias.Count > 0 && ListaCategorias.SelectedIndex < 0)
        {
            ListaCategorias.SelectedIndex = 0;
        }

        if (areaLista is Guid id)
        {
            var coincidencia = vistas.FirstOrDefault(a => a.Id == id);
            var areaCombo = areas.FirstOrDefault(a => a.Id == id);
            if (coincidencia is not null)
            {
                ListaAreas.SelectedItem = coincidencia;
            }

            if (areaCombo is not null)
            {
                ComboAreaListas.SelectedItem = areaCombo;
                return;
            }
        }

        if (areas.Count > 0 && ComboAreaListas.SelectedIndex < 0)
        {
            ComboAreaListas.SelectedIndex = 0;
        }
    }

    private Guid? AreaActual =>
        ComboAreaListas.SelectedItem is AreaDefinicion area ? area.Id : AreasOperativas.Todas.FirstOrDefault()?.Id;

    private void ComboAreaListas_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CargarListasDelArea();

    private void ListaCategorias_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaCategorias.SelectedItem is not CategoriaMenuDefinicion categoria)
        {
            return;
        }

        CajaNombreCategoria.Text = categoria.Nombre;
    }

    private void ListaAreas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaListaVista vista)
        {
            return;
        }

        var area = vista.Area;
        CajaNombreArea.Text = area.Nombre;
        CajaDescripcionArea.Text = area.Descripcion;
        CajaPrefijoArea.Text = area.Prefijo;
        ComboPlantillaArea.SelectedIndex = area.Plantilla switch
        {
            AreaOperativa.Oficina => 1,
            AreaOperativa.Limpieza => 2,
            _ => 0
        };
        SeleccionarIcono(area.Glifo);
        ComboCategoriaArea.SelectedItem = AreasOperativas.Categorias.FirstOrDefault(c => c.Id == area.CategoriaId);
    }

    private void CargarIconosArea()
    {
        CuadriculaIconoArea.ItemsSource = IconosArea.Opciones;
        if (CuadriculaIconoArea.SelectedIndex < 0)
        {
            CuadriculaIconoArea.SelectedIndex = 0;
        }
    }

    private void SeleccionarIcono(string glifo)
    {
        var indice = IconosArea.IndiceDe(IconosArea.Opciones, glifo);
        if (indice >= 0)
        {
            CuadriculaIconoArea.ItemsSource = IconosArea.Opciones;
            CuadriculaIconoArea.SelectedIndex = indice;
            return;
        }

        var extra = IconosArea.Opciones.ToList();
        extra.Insert(0, new IconoAreaOpcion("Icono actual", glifo));
        CuadriculaIconoArea.ItemsSource = extra;
        CuadriculaIconoArea.SelectedIndex = 0;
    }

    private string GlifoElegido() =>
        CuadriculaIconoArea.SelectedItem is IconoAreaOpcion opcion
            ? opcion.Glifo
            : "\uE8F1";

    private Guid? CategoriaElegida() =>
        ComboCategoriaArea.SelectedItem is CategoriaMenuDefinicion categoria
            ? categoria.Id
            : AreasOperativas.Categorias.FirstOrDefault()?.Id;

    private AreaOperativa PlantillaElegida() =>
        ComboPlantillaArea.SelectedItem is ComboBoxItem { Tag: string etiqueta } &&
        Enum.TryParse<AreaOperativa>(etiqueta, out var plantilla)
            ? plantilla
            : AreaOperativa.Clinica;

    private void AgregarCategoria_Click(object sender, RoutedEventArgs e)
    {
        var nombre = CajaNombreCategoria.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("Escriba el nombre de la nueva categoría.", InfoBarSeverity.Warning);
            return;
        }

        var creada = AreasOperativas.AgregarCategoria(nombre, string.Empty);
        RefrescarAreasEnApp(seleccionarCategoria: creada.Id);
        Mostrar($"Se agregó la categoría «{nombre}» al menú lateral.", InfoBarSeverity.Success);
    }

    private void GuardarCategoria_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCategorias.SelectedItem is not CategoriaMenuDefinicion categoria)
        {
            Mostrar("Seleccione una categoría de la lista.", InfoBarSeverity.Warning);
            return;
        }

        var nombre = CajaNombreCategoria.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("El nombre de la categoría no puede quedar vacío.", InfoBarSeverity.Warning);
            return;
        }

        AreasOperativas.ActualizarCategoria(categoria.Id, nombre, string.Empty);
        RefrescarAreasEnApp(seleccionarCategoria: categoria.Id);
        Mostrar($"Se guardó la categoría «{nombre}».", InfoBarSeverity.Success);
    }

    private async void BorrarCategoria_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCategorias.SelectedItem is not CategoriaMenuDefinicion categoria)
        {
            Mostrar("Seleccione una categoría para borrarla.", InfoBarSeverity.Warning);
            return;
        }

        var secciones = AreasOperativas.DeCategoria(categoria.Id).Count;
        if (secciones > 0)
        {
            Mostrar(
                $"Mueva las {secciones} sección(es) a otra categoría antes de borrar «{categoria.Nombre}».",
                InfoBarSeverity.Warning);
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Borrar categoría",
            Content = $"Se eliminará «{categoria.Nombre}» del menú.",
            PrimaryButtonText = "Borrar categoría",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var nombreBorrado = categoria.Nombre;
        AreasOperativas.EliminarCategoria(categoria.Id);
        RefrescarAreasEnApp();
        Mostrar($"Se borró la categoría «{nombreBorrado}».", InfoBarSeverity.Success);
    }

    private void SubirCategoria_Click(object sender, RoutedEventArgs e) => MoverCategoriaSeleccionada(-1);

    private void BajarCategoria_Click(object sender, RoutedEventArgs e) => MoverCategoriaSeleccionada(1);

    private void MoverCategoriaSeleccionada(int desplazamiento)
    {
        if (ListaCategorias.SelectedItem is not CategoriaMenuDefinicion categoria)
        {
            Mostrar("Seleccione una categoría para cambiar su orden.", InfoBarSeverity.Warning);
            return;
        }

        if (!AreasOperativas.MoverCategoria(categoria.Id, desplazamiento))
        {
            Mostrar(desplazamiento < 0
                ? "Esa categoría ya está al inicio."
                : "Esa categoría ya está al final.", InfoBarSeverity.Informational);
            return;
        }

        RefrescarAreasEnApp(seleccionarCategoria: categoria.Id);
        Mostrar($"La categoría «{categoria.Nombre}» cambió de posición en el menú.", InfoBarSeverity.Success);
    }

    private void AgregarArea_Click(object sender, RoutedEventArgs e)
    {
        var nombre = CajaNombreArea.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("Escriba el nombre de la nueva sección.", InfoBarSeverity.Warning);
            return;
        }

        if (CategoriaElegida() is null)
        {
            Mostrar("Elija una categoría para la sección.", InfoBarSeverity.Warning);
            return;
        }

        var creada = AreasOperativas.Agregar(
            nombre,
            CajaDescripcionArea.Text ?? string.Empty,
            CajaPrefijoArea.Text ?? string.Empty,
            PlantillaElegida(),
            GlifoElegido(),
            CategoriaElegida());
        RefrescarAreasEnApp(creada.Id);
        Mostrar($"Se agregó la sección «{nombre}» (código {creada.Prefijo}_001) al menú lateral.", InfoBarSeverity.Success);
    }

    private void GuardarArea_Click(object sender, RoutedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaListaVista vista)
        {
            Mostrar("Seleccione una sección de la lista para guardarla.", InfoBarSeverity.Warning);
            return;
        }

        var nombre = CajaNombreArea.Text?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            Mostrar("El nombre de la sección no puede quedar vacío.", InfoBarSeverity.Warning);
            return;
        }

        AreasOperativas.Actualizar(
            vista.Id,
            nombre,
            CajaDescripcionArea.Text ?? string.Empty,
            CajaPrefijoArea.Text ?? string.Empty,
            PlantillaElegida(),
            GlifoElegido(),
            CategoriaElegida());
        var guardada = AreasOperativas.Obtener(vista.Id);
        App.Instance.Repositorio.ActualizarIndiceBusqueda();
        RefrescarAreasEnApp(vista.Id);
        var prefijo = guardada?.Prefijo ?? string.Empty;
        var categoria = AreasOperativas.TituloCategoria(guardada?.CategoriaId);
        Mostrar($"Se guardó «{nombre}» en «{categoria}» con prefijo {prefijo}.", InfoBarSeverity.Success);
    }

    private async void BorrarArea_Click(object sender, RoutedEventArgs e)
    {
        if (ListaAreas.SelectedItem is not AreaListaVista vista)
        {
            Mostrar("Seleccione una sección para borrarla.", InfoBarSeverity.Warning);
            return;
        }

        var productos = App.Instance.Repositorio.Contar(vista.Id);
        var dialogo = new ContentDialog
        {
            Title = "Borrar sección",
            Content = productos == 0
                ? $"Se eliminará «{vista.Nombre}» del menú."
                : $"Se eliminará «{vista.Nombre}». {productos} producto(s) quedarán sin área y seguirán en el catálogo.",
            PrimaryButtonText = "Borrar sección",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var nombreBorrado = vista.Nombre;
        App.Instance.Repositorio.ReasignarArea(vista.Id, null);
        AreasOperativas.Eliminar(vista.Id);
        RefrescarAreasEnApp();
        Mostrar($"Se borró «{nombreBorrado}». Los productos de esa lista quedaron sin área.", InfoBarSeverity.Success);
    }

    private void SubirArea_Click(object sender, RoutedEventArgs e) => MoverAreaSeleccionada(-1);

    private void BajarArea_Click(object sender, RoutedEventArgs e) => MoverAreaSeleccionada(1);

    private void MoverAreaSeleccionada(int desplazamiento)
    {
        if (ListaAreas.SelectedItem is not AreaListaVista vista)
        {
            Mostrar("Seleccione una sección para cambiar su orden.", InfoBarSeverity.Warning);
            return;
        }

        if (!AreasOperativas.Mover(vista.Id, desplazamiento))
        {
            Mostrar(desplazamiento < 0
                ? "Esa sección ya está al inicio de su categoría."
                : "Esa sección ya está al final de su categoría.", InfoBarSeverity.Informational);
            return;
        }

        RefrescarAreasEnApp(vista.Id);
        Mostrar($"El orden de «{vista.Nombre}» dentro de su categoría se actualizó.", InfoBarSeverity.Success);
    }

    private void RefrescarAreasEnApp(Guid? seleccionar = null, Guid? seleccionarCategoria = null)
    {
        CargarAreas(seleccionar, seleccionarCategoria);
        CargarListasDelArea();
        ActualizarResumen();
        App.Instance.MainAppWindow?.ActualizarMenuAreas();
    }

    private sealed record AreaListaVista(
        Guid Id,
        string Nombre,
        string Glifo,
        string CategoriaTitulo,
        AreaDefinicion Area);

    private void CargarPreferencias()
    {
        _cargandoPreferencias = true;
        var preferencias = App.Instance.Preferencias.Actual;
        ToggleAutoguardado.IsOn = preferencias.AutoguardadoActivo;
        ComboIntervalo.SelectedIndex = preferencias.IntervaloAutoguardadoMinutos switch
        {
            5 => 0,
            20 => 2,
            30 => 3,
            _ => 1
        };
        ComboIntervalo.IsEnabled = preferencias.AutoguardadoActivo;
        ToggleCopias.IsOn = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsOn = preferencias.CopiasIncluyenImagenes;
        ComboIntervaloCopia.SelectedIndex = preferencias.IntervaloCopiasHoras switch
        {
            1 => 0,
            12 => 2,
            24 => 3,
            _ => 1
        };
        ComboMaximoCopias.SelectedIndex = preferencias.MaximoCopias switch
        {
            5 => 0,
            10 => 1,
            40 => 3,
            _ => 2
        };
        ComboIntervaloCopia.IsEnabled = preferencias.CopiasActivas;
        ComboMaximoCopias.IsEnabled = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsEnabled = preferencias.CopiasActivas;
        CargarListaCopias();
        _cargandoPreferencias = false;
    }

    private void Copias_Cambiado(object sender, RoutedEventArgs e)
    {
        if (_cargandoPreferencias)
        {
            return;
        }

        var preferencias = App.Instance.Preferencias.Actual;
        preferencias.CopiasActivas = ToggleCopias.IsOn;
        preferencias.CopiasIncluyenImagenes = ToggleImagenesCopia.IsOn;
        ComboIntervaloCopia.IsEnabled = preferencias.CopiasActivas;
        ComboMaximoCopias.IsEnabled = preferencias.CopiasActivas;
        ToggleImagenesCopia.IsEnabled = preferencias.CopiasActivas;
        if (ComboIntervaloCopia.SelectedItem is ComboBoxItem { Tag: string horasTexto }
            && int.TryParse(horasTexto, out var horas))
        {
            preferencias.IntervaloCopiasHoras = horas;
        }

        if (ComboMaximoCopias.SelectedItem is ComboBoxItem { Tag: string maxTexto }
            && int.TryParse(maxTexto, out var maximo))
        {
            preferencias.MaximoCopias = maximo;
        }

        App.Instance.Preferencias.Guardar();
        App.Instance.Autoguardado.AplicarPreferencias();
        var estado = preferencias.CopiasActivas
            ? $"Copias automáticas cada {preferencias.IntervaloCopiasHoras} hora(s). Se conservan {preferencias.MaximoCopias}."
            : "Copias automáticas desactivadas. Puede crear una manualmente.";
        Mostrar(estado, InfoBarSeverity.Success);
    }

    private void CargarListaCopias()
    {
        var copias = App.Instance.Copias.Listar();
        ListaCopias.ItemsSource = copias;
        var ultima = App.Instance.Preferencias.Actual.UltimaCopiaUtc;
        TextoUltimaCopia.Text = ultima is DateTimeOffset fecha
            ? $"Última copia: {fecha.ToLocalTime():dd/MM/yyyy HH:mm}. Carpeta: {App.Instance.Copias.CarpetaRaiz}"
            : $"Aún no hay copias automáticas. Carpeta: {App.Instance.Copias.CarpetaRaiz}";
    }

    private void CrearCopia_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var copia = App.Instance.Copias.Crear("manual");
            CargarListaCopias();
            ActualizarResumen();
            Mostrar($"Copia creada ({copia.Titulo}): {copia.Fichas} ficha(s), {copia.Movimientos} movimiento(s) y {copia.Areas} área(s).", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo crear la copia: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void RestaurarCopia_Click(object sender, RoutedEventArgs e)
    {
        if (ListaCopias.SelectedItem is not CopiaSeguridadInfo copia)
        {
            Mostrar("Seleccione una copia de la lista para restaurarla.", InfoBarSeverity.Warning);
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = "Restaurar copia de seguridad",
            Content = $"Se reemplazarán fichas, características, áreas, desplegables, RR. HH., el registro de ingresos y salidas y los recibos por la copia del {copia.Titulo}. Antes se crea otra copia del estado actual.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            App.Instance.Copias.Crear("antes-de-restaurar");
            App.Instance.Copias.Restaurar(copia.Carpeta);
            CargarAreas();
            CargarListasDelArea();
            CargarListaCopias();
            ActualizarResumen();
            Mostrar($"Se restauró la copia del {copia.Titulo}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo restaurar: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void AbrirCarpetaCopias_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.Instance.Copias.CarpetaRaiz);
        await Windows.System.Launcher.LaunchFolderPathAsync(App.Instance.Copias.CarpetaRaiz);
    }

    private void Autoguardado_Cambiado(object sender, RoutedEventArgs e)
    {
        if (_cargandoPreferencias)
        {
            return;
        }

        var preferencias = App.Instance.Preferencias.Actual;
        preferencias.AutoguardadoActivo = ToggleAutoguardado.IsOn;
        ComboIntervalo.IsEnabled = preferencias.AutoguardadoActivo;
        if (ComboIntervalo.SelectedItem is ComboBoxItem { Tag: string etiqueta }
            && int.TryParse(etiqueta, out var minutos))
        {
            preferencias.IntervaloAutoguardadoMinutos = minutos;
        }

        App.Instance.Preferencias.Guardar();
        App.Instance.Autoguardado.AplicarPreferencias();
        var estado = preferencias.AutoguardadoActivo
            ? $"Autoguardado cada {preferencias.IntervaloAutoguardadoMinutos} minutos."
            : "Autoguardado desactivado.";
        Mostrar(estado, InfoBarSeverity.Success);
    }

    private ListaDesplegable? ListaActual =>
        ComboListas.SelectedIndex >= 0 && ComboListas.SelectedIndex < _listasArea.Count
            ? _listasArea[ComboListas.SelectedIndex]
            : null;

    private void CargarListasDelArea()
    {
        if (ComboListas is null)
        {
            return;
        }

        _listasArea = Catalogos.ListasDe(AreaActual);
        ComboListas.ItemsSource = _listasArea.Select(l => l.Titulo).ToList();
        ComboListas.SelectedIndex = _listasArea.Count > 0 ? 0 : -1;
        CargarValores();
    }

    private void ActualizarResumen()
    {
        var repo = App.Instance.Repositorio;
        RutaDatos.Text = repo.RutaArchivo;
        ResumenDatos.Text = $"{repo.Todas.Count} fichas en catálogo.";
        RutaCopias.Text = $"Copias de seguridad: {App.Instance.Copias.CarpetaRaiz}";
    }

    private void ComboListas_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        CargarValores();

    private List<string> ValoresEditables()
    {
        if (ListaActual is null)
        {
            return [];
        }

        var propia = Catalogos.Copia(ListaActual.Clave);
        if (propia.Count > 0)
        {
            return propia;
        }

        var separador = ListaActual.Clave.IndexOf('_');
        if (separador < 0)
        {
            return propia;
        }

        return Catalogos.Obtener(AreaActual, ListaActual.Clave[(separador + 1)..]).ToList();
    }

    private void CargarValores()
    {
        if (ListaActual is null)
        {
            return;
        }

        ListaValores.ItemsSource = ValoresEditables();
        CajaValor.Text = string.Empty;
    }

    private void ListaValores_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaValores.SelectedItem is string valor)
        {
            CajaValor.Text = valor;
        }
    }

    private void AgregarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null)
        {
            return;
        }

        var valor = CajaValor.Text.Trim();
        if (valor.Length == 0)
        {
            Mostrar("Escriba un valor para agregarlo.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        if (lista.Any(v => v.Equals(valor, StringComparison.CurrentCultureIgnoreCase)))
        {
            Mostrar("Ese valor ya existe en la lista.", InfoBarSeverity.Warning);
            return;
        }

        lista.Add(valor);
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor agregado al desplegable.", InfoBarSeverity.Success);
    }

    private void EditarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null || ListaValores.SelectedItem is not string anterior)
        {
            Mostrar("Seleccione un valor de la lista para editarlo.", InfoBarSeverity.Warning);
            return;
        }

        var nuevo = CajaValor.Text.Trim();
        if (nuevo.Length == 0)
        {
            Mostrar("El valor no puede quedar vacío.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        var indice = lista.FindIndex(v => v.Equals(anterior, StringComparison.CurrentCultureIgnoreCase));
        if (indice < 0)
        {
            return;
        }

        lista[indice] = nuevo;
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor actualizado.", InfoBarSeverity.Success);
    }

    private void BorrarValor_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null || ListaValores.SelectedItem is not string valor)
        {
            Mostrar("Seleccione un valor para borrarlo.", InfoBarSeverity.Warning);
            return;
        }

        var lista = ValoresEditables();
        lista.RemoveAll(v => v.Equals(valor, StringComparison.CurrentCultureIgnoreCase));
        Catalogos.Reemplazar(ListaActual.Clave, lista);
        CargarValores();
        Mostrar("Valor eliminado del desplegable.", InfoBarSeverity.Success);
    }

    private void RestaurarLista_Click(object sender, RoutedEventArgs e)
    {
        if (ListaActual is null)
        {
            return;
        }

        Catalogos.Restaurar(ListaActual.Clave);
        CargarValores();
        Mostrar($"Se restauró la lista «{ListaActual.Titulo}».", InfoBarSeverity.Success);
    }

    private void SeleccionarTodoExportar_Click(object sender, RoutedEventArgs e) =>
        MarcarExportacion(true);

    private void QuitarTodoExportar_Click(object sender, RoutedEventArgs e) =>
        MarcarExportacion(false);

    private void MarcarExportacion(bool marcado)
    {
        CheckExportarFichas.IsChecked = marcado;
        CheckExportarCaracteristicas.IsChecked = marcado;
        CheckExportarAreas.IsChecked = marcado;
        CheckExportarDesplegables.IsChecked = marcado;
        CheckExportarRh.IsChecked = marcado;
        CheckExportarRegistro.IsChecked = marcado;
        CheckExportarRecibos.IsChecked = marcado;
        CheckExportarFotos.IsChecked = marcado;
        CheckExportarPreferencias.IsChecked = marcado;
    }

    private async void ExportarSeleccion_Click(object sender, RoutedEventArgs e)
    {
        var opciones = new ExportacionOpciones
        {
            Fichas = CheckExportarFichas.IsChecked == true,
            Caracteristicas = CheckExportarCaracteristicas.IsChecked == true,
            Areas = CheckExportarAreas.IsChecked == true,
            Desplegables = CheckExportarDesplegables.IsChecked == true,
            RecursosHumanos = CheckExportarRh.IsChecked == true,
            Registro = CheckExportarRegistro.IsChecked == true,
            Recibos = CheckExportarRecibos.IsChecked == true,
            Fotos = CheckExportarFotos.IsChecked == true,
            Preferencias = CheckExportarPreferencias.IsChecked == true
        };
        if (!opciones.HaySeleccion)
        {
            Mostrar("Marque al menos un tipo de información, o pulse Seleccionar todo.", InfoBarSeverity.Warning);
            return;
        }

        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("Archivo ZIP", [".zip"]);
        selector.DefaultFileExtension = ".zip";
        selector.SuggestedFileName = $"iderma-exportacion-{DateTime.Now:yyyyMMdd-HHmm}";

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return;
        }

        var temporal = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.zip");
        try
        {
            App.Instance.Repositorio.Guardar();
            App.Instance.Rh.Guardar();
            App.Instance.PersonalMedico.Guardar();
            App.Instance.Preferencias.Guardar();
            App.Instance.HistorialPrecios.Guardar();
            await Task.Run(() => ExportacionDatosService.CrearZip(temporal, opciones));
            await ExcelUi.CopiarArchivoAsync(temporal, archivo);

            await ExcelUi.OfrecerAbrirAsync(XamlRoot, new ResultadoExportacion(archivo, ".zip"));
            Mostrar("Se exportó la información seleccionada.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo exportar: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                try
                {
                    File.Delete(temporal);
                }
                catch
                {
                }
            }
        }
    }

    private async void Exportar_Click(object sender, RoutedEventArgs e)
    {
        var selector = new FileSavePicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeChoices.Add("JSON", [".json"]);
        selector.DefaultFileExtension = ".json";
        selector.SuggestedFileName = "fichas-tecnicas-iderma";

        var archivo = await selector.PickSaveFileAsync();
        if (archivo is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(archivo, App.Instance.Repositorio.ExportarJson());
        Mostrar("Inventario exportado.", InfoBarSeverity.Success);
    }

    private async void Importar_Click(object sender, RoutedEventArgs e)
    {
        var selector = new FileOpenPicker();
        VentanaHelper.AsociarSelector(selector);
        VentanaHelper.ConfigurarInicio(selector);
        selector.FileTypeFilter.Add(".json");

        var archivo = await selector.PickSingleFileAsync();
        if (archivo is null)
        {
            return;
        }

        try
        {
            var json = await FileIO.ReadTextAsync(archivo);
            var cantidad = App.Instance.Repositorio.ImportarJson(json);
            ActualizarResumen();
            Mostrar($"Se importaron {cantidad} fichas.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo importar el archivo: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportarExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.ImportarAsync();
            if (resultado is null)
            {
                return;
            }

            ActualizarResumen();
            Mostrar(resultado.Mensaje, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo importar el Excel: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void PlantillaExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await ExcelUi.GuardarPlantillaAsync())
            {
                Mostrar("Se guardó la plantilla con los productos del inventario.", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo descargar la plantilla: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void Restaurar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new ContentDialog
        {
            Title = "Restaurar ejemplos",
            Content = "Se reemplazará el catálogo actual por las fichas de ejemplo de Iderma Capilar.",
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        App.Instance.Repositorio.RestaurarEjemplos();
        ActualizarResumen();
        Mostrar("Se restauraron las fichas de ejemplo.", InfoBarSeverity.Success);
    }

    private async void BorrarBase_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new ContentDialog
        {
            Title = "Borrar base de datos",
            Content = "Se vaciará el catálogo de fichas. Antes se crea una copia completa (fichas, áreas, desplegables y RR. HH.).",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var copia = App.Instance.Repositorio.VaciarConCopiaSeguridad();
        CargarListaCopias();
        ActualizarResumen();
        Mostrar($"Base vaciada. Copia: {Path.GetFileName(copia)}", InfoBarSeverity.Success);
    }

    private void ActualizarResumenMedicacion()
    {
        var n = App.Instance.PersonalMedico.MedicacionesOrdenadas().Count;
        ResumenMedicacionComun.Text = n == 0
            ? "Aún no hay medicación común. Descargue la plantilla e impórtela."
            : n == 1
                ? "1 medicamento común listo para sugerencias en Recetas."
                : $"{n} medicamentos comunes listos para sugerencias en Recetas.";
    }

    private async void PlantillaMedicacion_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var resultado = await ExcelUi.GuardarExcelAsync(
                "plantilla-medicacion-comun",
                ExcelMedicacionComunService.CrearPlantilla);
            if (resultado is null)
            {
                return;
            }

            Mostrar("Se guardó la plantilla de medicación común.", InfoBarSeverity.Success);
            await ExcelUi.OfrecerAbrirAsync(XamlRoot, resultado);
        }
        catch (Exception ex)
        {
            Mostrar($"No se pudo descargar la plantilla: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportarMedicacion_Click(object sender, RoutedEventArgs e)
    {
        var temporal = await ExcelUi.ElegirExcelTemporalAsync();
        if (temporal is null)
        {
            return;
        }

        try
        {
            var filas = ExcelMedicacionComunService.Leer(temporal);
            var n = App.Instance.PersonalMedico.ImportarMedicaciones(filas);
            ActualizarResumenMedicacion();
            Mostrar(
                n == 0
                    ? "No se encontraron medicamentos en el Excel."
                    : $"Se cargaron {n} medicamentos comunes. En Recetas aparecerán al escribir el nombre.",
                n == 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo leer el Excel: " + ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    private void IniciarTourTrabajador_Click(object sender, RoutedEventArgs e)
    {
        App.Instance.MainAppWindow?.IniciarTourTrabajadorNuevo();
    }

    private void IniciarGuiaSeccion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        App.Instance.MainAppWindow?.IniciarGuiaSeccion(id);
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraEstado.Severity = severidad;
        BarraEstado.Message = mensaje;
        BarraEstado.IsOpen = true;
    }
}

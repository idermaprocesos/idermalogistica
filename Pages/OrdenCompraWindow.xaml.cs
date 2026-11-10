using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using IdermaFichas.Helpers;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Pages;

public sealed partial class OrdenCompraWindow : Page
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private readonly ObservableCollection<LineaOrdenCompra> _lineas = [];
    private Guid _ordenId = Guid.NewGuid();
    private DateTimeOffset? _fechaRegistro;
    private string _correlativo = string.Empty;
    private bool _exportando;
    private bool _cargada;
    private bool _bancoDesdeDocumento;

    public OrdenCompraWindow()
    {
        InitializeComponent();
        PrepararComboBanco();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (_cargada)
        {
            return;
        }

        IEnumerable<LineaOrdenCompra>? lineas = e.Parameter switch
        {
            OrdenCompraEdicion edicion => PrepararEdicion(edicion),
            IEnumerable<LineaOrdenCompra> nuevas => nuevas,
            _ => null
        };
        if (lineas is null)
        {
            if (Frame?.CanGoBack == true)
            {
                Frame.GoBack();
            }

            return;
        }

        _cargada = true;
        foreach (var linea in lineas)
        {
            Agregar(linea, refrescarBanco: false);
        }

        if (e.Parameter is not OrdenCompraEdicion)
        {
            PrepararEncabezado();
        }

        if (_bancoDesdeDocumento)
        {
            var proveedor = ProveedorDeOrden();
            if (proveedor is not null)
            {
                TextoProveedorBanco.Text = string.IsNullOrWhiteSpace(proveedor.Ruc)
                    ? proveedor.Nombre
                    : $"{proveedor.Nombre} · RUC {proveedor.Ruc}";
            }
        }
        else
        {
            CargarDatosBancariosDesdeProveedor();
        }

        Lista.ItemsSource = _lineas;
        ResumenProductos.Text = _lineas.Count == 1
            ? "1 producto"
            : $"{_lineas.Count} productos";
        ActualizarTotal();
    }

    private void PrepararComboBanco()
    {
        var items = new List<string> { "Sin banco" };
        items.AddRange(BancoProveedor.Todas);
        ComboBanco.ItemsSource = items;
        ComboBanco.SelectedIndex = 0;
        ActualizarVisibilidadBancoOtro();
    }

    private void Volver_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private IEnumerable<LineaOrdenCompra> PrepararEdicion(OrdenCompraEdicion edicion)
    {
        _ordenId = edicion.Id == Guid.Empty ? Guid.NewGuid() : edicion.Id;
        _fechaRegistro = edicion.FechaRegistro;
        var documento = edicion.Documento;
        if (documento is null)
        {
            PrepararEncabezado();
            return edicion.Lineas;
        }

        CajaMes.Text = documento.MesAnio;
        CajaCobertura.Text = documento.PeriodoCobertura;
        CajaElaborado.Text = documento.ElaboradoPor;
        CajaRevisado.Text = documento.RevisadoPor;
        _correlativo = documento.Correlativo?.Trim() ?? string.Empty;
        if (DateTime.TryParseExact(documento.FechaElaboracion, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            AsignarFechaElaboracion(fecha);
        }
        else
        {
            AsignarFechaElaboracion(DateTime.Today);
        }

        ComboEstado.ItemsSource = CalculoOrdenCompra.EstadosDocumento;
        var indice = Array.FindIndex(
            CalculoOrdenCompra.EstadosDocumento,
            estado => estado.Equals(documento.Estado, StringComparison.OrdinalIgnoreCase));
        ComboEstado.SelectedIndex = indice >= 0 ? indice : 0;
        CajaFlete.Text = documento.FletePrecio;
        ComboFlete.ItemsSource = CalculoOrdenCompra.OpcionesFlete;
        var indiceFlete = Array.FindIndex(
            CalculoOrdenCompra.OpcionesFlete,
            o => o.Equals(documento.FleteEstado, StringComparison.OrdinalIgnoreCase));
        ComboFlete.SelectedIndex = indiceFlete >= 0 ? indiceFlete : 1;
        ComboCondicionPago.ItemsSource = CalculoOrdenCompra.CondicionesPago;
        var indicePago = Array.FindIndex(
            CalculoOrdenCompra.CondicionesPago,
            o => o.Equals(documento.CondicionPago, StringComparison.OrdinalIgnoreCase));
        ComboCondicionPago.SelectedIndex = indicePago >= 0 ? indicePago : 0;
        ActualizarTextoFlete();
        if (!string.IsNullOrWhiteSpace(documento.BancoProveedor)
            || !string.IsNullOrWhiteSpace(documento.NumeroCuentaProveedor)
            || !string.IsNullOrWhiteSpace(documento.CciProveedor))
        {
            _bancoDesdeDocumento = true;
            AsignarDatosBancariosUi(
                documento.BancoProveedor,
                documento.NumeroCuentaProveedor,
                documento.CciProveedor,
                documento.ProveedorUnico);
        }

        return edicion.Lineas;
    }

    private void Agregar(LineaOrdenCompra linea, bool refrescarBanco = true)
    {
        linea.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LineaOrdenCompra.CantidadPedir))
            {
                ActualizarTotal();
            }
        };
        _lineas.Add(linea);
        ResumenProductos.Text = _lineas.Count == 1 ? "1 producto" : $"{_lineas.Count} productos";
        ActualizarTotal();
        if (refrescarBanco)
        {
            CargarDatosBancariosDesdeProveedor();
        }
    }

    private void BuscarInventario_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var texto = sender.Text?.Trim() ?? string.Empty;
        sender.ItemsSource = texto.Length < 2
            ? null
            : App.Instance.Repositorio.BuscarLimitado(texto, 12)
                .Select(f => $"{f.Codigo}  {f.Nombre}")
                .ToList();
    }

    private async void BuscarInventario_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var texto = args.SelectedItem as string ?? string.Empty;
        var codigo = texto.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var ficha = App.Instance.Repositorio.Todas.FirstOrDefault(f =>
            f.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));
        if (ficha is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ficha.Proveedor))
        {
            Mostrar(
                $"«{ficha.Nombre}» no tiene proveedor. Regístrelo para agregarlo a la orden.",
                InfoBarSeverity.Informational);
            var nuevo = await ProveedorEditorDialogo.MostrarAsync(XamlRoot);
            if (nuevo is null)
            {
                Mostrar("Debe registrar un proveedor para agregar este producto.", InfoBarSeverity.Warning);
                sender.Text = string.Empty;
                return;
            }

            ProveedorFichaSync.AsignarAFicha(ficha, nuevo);
            Mostrar($"Proveedor «{nuevo.Nombre}» guardado y asignado a «{ficha.Nombre}».", InfoBarSeverity.Success);
        }

        var existente = _lineas.FirstOrDefault(l => l.FichaId == ficha.Id);
        if (existente is null)
        {
            Agregar(OrdenCompraArmado.DesdeFicha(ficha, 1));
        }
        else
        {
            existente.CantidadPedir += 1;
        }

        sender.Text = string.Empty;
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e)
    {
        if (_lineas.Count == 0)
        {
            Mostrar("Agregue al menos un producto antes de guardar.", InfoBarSeverity.Warning);
            return;
        }

        if (!await ValidarContratoAsync())
        {
            return;
        }

        if (ComboEstado.SelectedIndex < 0 && ComboEstado.Items.Count > 0)
        {
            ComboEstado.SelectedIndex = 0;
        }

        try
        {
            var documento = Capturar();
            Registrar(documento);
            Mostrar($"La orden quedó guardada como {documento.Estado}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Mostrar("No se pudo guardar: " + ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Mostrar(string mensaje, InfoBarSeverity severidad)
    {
        BarraAviso.Severity = severidad;
        BarraAviso.Message = mensaje;
        BarraAviso.IsOpen = true;
    }

    private void PrepararEncabezado()
    {
        var hoy = DateTime.Today;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        CajaMes.Text = hoy.ToString("MM / yyyy");
        CajaCobertura.Text = $"{inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}";
        AsignarFechaElaboracion(hoy);
        ComboEstado.ItemsSource = CalculoOrdenCompra.EstadosDocumento;
        ComboEstado.SelectedIndex = 0;
        ComboFlete.ItemsSource = CalculoOrdenCompra.OpcionesFlete;
        ComboFlete.SelectedIndex = 1;
        ComboCondicionPago.ItemsSource = CalculoOrdenCompra.CondicionesPago;
        ComboCondicionPago.SelectedIndex = 0;
        CajaFlete.Text = string.Empty;
        ActualizarTextoFlete();
    }

    private void AsignarFechaElaboracion(DateTime fecha) =>
        TextoFechaElaboracion.Text = fecha.ToString("dd/MM/yyyy", Cultura);

    private void Flete_Changed(object sender, TextChangedEventArgs e) => ActualizarTextoFlete();

    private void Flete_Changed(object sender, SelectionChangedEventArgs e) => ActualizarTextoFlete();

    private void ActualizarTextoFlete()
    {
        var estado = ComboFlete.SelectedItem as string ?? "No incluido";
        var precio = (CajaFlete.Text ?? string.Empty).Trim();
        TextoFlete.Text = string.IsNullOrWhiteSpace(precio)
            ? $"Flete: {estado.ToLowerInvariant()}. Indique el monto si aplica."
            : $"Flete {estado.ToLowerInvariant()}: {precio}";
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

        if (_lineas.Count == 0)
        {
            Mostrar("Agregue al menos un producto antes de exportar.", InfoBarSeverity.Warning);
            return;
        }

        if (!await ValidarContratoAsync())
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
            var bancoCompletado = CompletarDatosBancariosVaciosSiCorresponde();
            var baseMsg = pdf
                ? "Se generó el PDF y la orden quedó en Órdenes anteriores."
                : "Se generó el Excel y la orden quedó en Órdenes anteriores.";
            BarraAviso.Severity = InfoBarSeverity.Success;
            BarraAviso.Message = bancoCompletado
                ? baseMsg + " También se guardaron en el proveedor los datos bancarios que aún estaban vacíos."
                : baseMsg;
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

    private async Task<bool> ValidarContratoAsync()
    {
        if (OrdenCompraArmado.CumpleAcuerdoContrato(_lineas.ToList(), out var mensaje))
        {
            return true;
        }

        var dialogo = new ContentDialog
        {
            Title = "Cantidad mínima por contrato",
            Content = mensaje,
            CloseButtonText = "Entendido",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        await dialogo.ShowAsync();
        return false;
    }

    private OrdenCompraDocumento Capturar()
    {
        var fechaTexto = (TextoFechaElaboracion.Text ?? string.Empty).Trim();
        if (!DateTime.TryParseExact(fechaTexto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            fecha = DateTime.Today;
            AsignarFechaElaboracion(fecha);
        }

        if (string.IsNullOrWhiteSpace(_correlativo))
        {
            _correlativo = App.Instance.OrdenesCompra.SiguienteCorrelativo(fecha.Year, _ordenId);
        }

        var lineas = _lineas.Select(l =>
        {
            l.Ruc = OrdenCompraArmado.RucDe(l.Proveedor);
            return l.Exportar();
        }).ToList();
        var (banco, cuenta, cci) = LeerDatosBancariosUi();
        return new OrdenCompraDocumento
        {
            Correlativo = _correlativo,
            MesAnio = (CajaMes.Text ?? string.Empty).Trim(),
            FechaElaboracion = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ElaboradoPor = (CajaElaborado.Text ?? string.Empty).Trim(),
            RevisadoPor = (CajaRevisado.Text ?? string.Empty).Trim(),
            PeriodoCobertura = (CajaCobertura.Text ?? string.Empty).Trim(),
            Estado = ComboEstado.SelectedItem as string ?? "Borrador",
            FletePrecio = (CajaFlete.Text ?? string.Empty).Trim(),
            FleteEstado = ComboFlete.SelectedItem as string ?? "No incluido",
            CondicionPago = CalculoOrdenCompra.NormalizarCondicionPago(
                ComboCondicionPago.SelectedItem as string),
            BancoProveedor = string.IsNullOrWhiteSpace(banco) ? "—" : banco,
            NumeroCuentaProveedor = string.IsNullOrWhiteSpace(cuenta) ? "—" : cuenta,
            CciProveedor = string.IsNullOrWhiteSpace(cci) ? "—" : cci,
            LugarEntrega = CalculoOrdenCompra.LugarEntrega,
            Lineas = lineas
        };
    }

    private void ComboBanco_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ActualizarVisibilidadBancoOtro();

    private void ActualizarVisibilidadBancoOtro()
    {
        var elegido = ComboBanco.SelectedItem as string ?? string.Empty;
        CajaBancoOtro.Visibility = BancoProveedor.EsOtras(elegido)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CargarDatosBancariosDesdeProveedor()
    {
        var proveedor = ProveedorDeOrden();
        if (proveedor is null)
        {
            TextoProveedorBanco.Text = "Sin proveedor en la orden";
            AsignarDatosBancariosUi(string.Empty, string.Empty, string.Empty, null);
            return;
        }

        TextoProveedorBanco.Text = string.IsNullOrWhiteSpace(proveedor.Ruc)
            ? proveedor.Nombre
            : $"{proveedor.Nombre} · RUC {proveedor.Ruc}";
        AsignarDatosBancariosUi(
            NombreBancoMostrar(proveedor),
            proveedor.NumeroCuenta,
            proveedor.Cci,
            proveedor.Nombre);
    }

    private void AsignarDatosBancariosUi(string? banco, string? cuenta, string? cci, string? nombreProveedor)
    {
        var bancoLimpio = (banco ?? string.Empty).Trim();
        if (bancoLimpio is "—" or "Sin banco")
        {
            bancoLimpio = string.Empty;
        }

        var normal = BancoProveedor.Normalizar(bancoLimpio);
        if (bancoLimpio.Length == 0)
        {
            ComboBanco.SelectedItem = "Sin banco";
            CajaBancoOtro.Text = string.Empty;
        }
        else if (BancoProveedor.Todas.Any(b => string.Equals(b, bancoLimpio, StringComparison.OrdinalIgnoreCase)))
        {
            ComboBanco.SelectedItem = BancoProveedor.Normalizar(bancoLimpio);
            CajaBancoOtro.Text = string.Empty;
        }
        else
        {
            ComboBanco.SelectedItem = BancoProveedor.Otras;
            CajaBancoOtro.Text = BancoProveedor.EsOtras(normal) && !string.Equals(bancoLimpio, BancoProveedor.Otras, StringComparison.OrdinalIgnoreCase)
                ? bancoLimpio
                : string.Empty;
        }

        CajaCuenta.Text = ValorOVacio(cuenta);
        CajaCci.Text = ValorOVacio(cci);
        if (!string.IsNullOrWhiteSpace(nombreProveedor) && nombreProveedor != "—")
        {
            TextoProveedorBanco.Text = nombreProveedor.Trim();
        }

        ActualizarVisibilidadBancoOtro();
    }

    private (string Banco, string Cuenta, string Cci) LeerDatosBancariosUi()
    {
        var elegido = ComboBanco.SelectedItem as string ?? "Sin banco";
        string banco;
        if (string.Equals(elegido, "Sin banco", StringComparison.OrdinalIgnoreCase) || elegido.Length == 0)
        {
            banco = string.Empty;
        }
        else if (BancoProveedor.EsOtras(elegido))
        {
            banco = string.IsNullOrWhiteSpace(CajaBancoOtro.Text) ? BancoProveedor.Otras : CajaBancoOtro.Text.Trim();
        }
        else
        {
            banco = elegido.Trim();
        }

        return (
            banco,
            (CajaCuenta.Text ?? string.Empty).Trim(),
            ProveedorRegistro.NormalizarCci(CajaCci.Text));
    }

    /// <summary>
    /// Al exportar: rellena en el proveedor solo banco/cuenta/CCI que estén vacíos en BD.
    /// No reemplaza datos ya guardados (para eso está «Actualizar proveedor»).
    /// </summary>
    private bool CompletarDatosBancariosVaciosSiCorresponde()
    {
        var proveedor = ProveedorDeOrden();
        if (proveedor is null)
        {
            return false;
        }

        var (banco, cuenta, cci) = LeerDatosBancariosUi();
        var bancoOk = TryResolverBanco(banco, out var bancoGuardar, out var bancoOtro, out _);

        var cambio = false;
        if (bancoOk
            && string.IsNullOrWhiteSpace(proveedor.Banco)
            && string.IsNullOrWhiteSpace(proveedor.BancoOtro)
            && bancoGuardar.Length > 0)
        {
            proveedor.Banco = bancoGuardar;
            proveedor.BancoOtro = bancoOtro;
            cambio = true;
        }

        if (string.IsNullOrWhiteSpace(proveedor.NumeroCuenta) && cuenta.Length > 0)
        {
            proveedor.NumeroCuenta = cuenta;
            cambio = true;
        }

        if (string.IsNullOrWhiteSpace(proveedor.Cci) && cci.Length == 20)
        {
            proveedor.Cci = cci;
            cambio = true;
        }

        if (!cambio)
        {
            return false;
        }

        App.Instance.Proveedores.Guardar(proveedor);
        return true;
    }

    private static bool TryResolverBanco(
        string bancoUi,
        out string bancoGuardar,
        out string bancoOtro,
        out string? error)
    {
        bancoGuardar = bancoUi.Length == 0
            ? string.Empty
            : BancoProveedor.Todas.Any(b => string.Equals(b, bancoUi, StringComparison.OrdinalIgnoreCase))
                ? BancoProveedor.Normalizar(bancoUi)
                : BancoProveedor.Otras;
        bancoOtro = bancoGuardar == BancoProveedor.Otras
            ? (string.IsNullOrWhiteSpace(bancoUi) ? string.Empty : bancoUi.Trim())
            : string.Empty;
        if (bancoGuardar == BancoProveedor.Otras && bancoOtro.Length == 0)
        {
            error = "Indique el nombre de la entidad bancaria.";
            return false;
        }

        error = null;
        return true;
    }

    private async void ActualizarBancoProveedor_Click(object sender, RoutedEventArgs e)
    {
        var proveedor = ProveedorDeOrden();
        if (proveedor is null)
        {
            Mostrar("No hay un proveedor en la orden para actualizar.", InfoBarSeverity.Warning);
            return;
        }

        var (banco, cuenta, cci) = LeerDatosBancariosUi();
        if (cci.Length > 0 && cci.Length != 20)
        {
            Mostrar("El CCI debe tener 20 dígitos.", InfoBarSeverity.Warning);
            return;
        }

        if (!TryResolverBanco(banco, out var bancoGuardar, out var bancoOtro, out var errorBanco))
        {
            Mostrar(errorBanco ?? "Datos de banco incompletos.", InfoBarSeverity.Warning);
            return;
        }

        var hayDatosPrevios =
            !string.IsNullOrWhiteSpace(proveedor.Banco)
            || !string.IsNullOrWhiteSpace(proveedor.BancoOtro)
            || !string.IsNullOrWhiteSpace(proveedor.NumeroCuenta)
            || !string.IsNullOrWhiteSpace(proveedor.Cci);
        var cambio =
            !string.Equals(proveedor.Banco ?? string.Empty, bancoGuardar, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(proveedor.BancoOtro ?? string.Empty, bancoOtro, StringComparison.Ordinal)
            || !string.Equals((proveedor.NumeroCuenta ?? string.Empty).Trim(), cuenta, StringComparison.Ordinal)
            || !string.Equals(ProveedorRegistro.NormalizarCci(proveedor.Cci), cci, StringComparison.Ordinal);
        if (!cambio)
        {
            Mostrar("Los datos bancarios ya están actualizados en el proveedor.", InfoBarSeverity.Informational);
            return;
        }

        if (hayDatosPrevios)
        {
            var dialogo = new ContentDialog
            {
                Title = "Reemplazar datos bancarios",
                Content =
                    $"El proveedor «{proveedor.Nombre}» ya tiene datos bancarios guardados.\n\n" +
                    $"Actual: {proveedor.TextoBanco}\n\n¿Desea reemplazarlos con los de esta orden?",
                PrimaryButtonText = "Reemplazar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        proveedor.Banco = bancoGuardar;
        proveedor.BancoOtro = bancoOtro;
        proveedor.NumeroCuenta = cuenta;
        proveedor.Cci = cci;
        App.Instance.Proveedores.Guardar(proveedor);
        TextoProveedorBanco.Text = string.IsNullOrWhiteSpace(proveedor.Ruc)
            ? proveedor.Nombre
            : $"{proveedor.Nombre} · RUC {proveedor.Ruc}";
        Mostrar(
            hayDatosPrevios
                ? "Se reemplazaron los datos bancarios del proveedor."
                : "Se guardaron los datos bancarios del proveedor.",
            InfoBarSeverity.Success);
    }

    private ProveedorRegistro? ProveedorDeOrden()
    {
        var ruc = _lineas
            .Select(l => ProveedorRegistro.NormalizarRuc(
                string.IsNullOrWhiteSpace(l.Ruc) ? OrdenCompraArmado.RucDe(l.Proveedor) : l.Ruc))
            .FirstOrDefault(r => r.Length > 0) ?? string.Empty;
        var nombre = _lineas
            .Select(l => l.Proveedor?.Trim())
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && p != "—") ?? string.Empty;
        return OrdenCompraArmado.BuscarProveedor(nombre)
               ?? App.Instance.Proveedores.Listar().FirstOrDefault(p =>
                   ruc.Length > 0 && ProveedorRegistro.NormalizarRuc(p.Ruc) == ruc);
    }

    private static string NombreBancoMostrar(ProveedorRegistro proveedor)
    {
        var banco = BancoProveedor.Normalizar(proveedor.Banco);
        if (banco.Length == 0)
        {
            return string.Empty;
        }

        return BancoProveedor.EsOtras(banco)
            ? (string.IsNullOrWhiteSpace(proveedor.BancoOtro) ? banco : proveedor.BancoOtro.Trim())
            : banco;
    }

    private static string ValorOVacio(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        return limpio is "—" ? string.Empty : limpio;
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
        _ = SolicitudesIncidenciasNube.SincronizarEstadoOrdenAsync(_ordenId, documento.Estado);
    }
}

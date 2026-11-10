using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas.Helpers;

/// <summary>
/// Diálogo de alta/edición de proveedor compartido (sección Proveedores y órdenes de compra).
/// </summary>
public static class ProveedorEditorDialogo
{
    public static async Task<ProveedorRegistro?> MostrarAsync(XamlRoot xamlRoot, ProveedorRegistro? origen = null)
    {
        var esNuevo = origen is null;
        var id = origen?.Id ?? Guid.NewGuid();
        var nombre = new TextBox
        {
            Header = "Nombre",
            Text = origen?.Nombre ?? string.Empty,
            PlaceholderText = "Nombre con el que aparece en las fichas"
        };
        var ruc = new TextBox
        {
            Header = "RUC",
            Text = origen?.Ruc ?? string.Empty,
            PlaceholderText = "11 dígitos",
            MaxLength = 11
        };
        var razonSocial = new TextBox
        {
            Header = "Nombre de empresa",
            Text = origen?.RazonSocial ?? string.Empty,
            PlaceholderText = "Razón social"
        };
        var tiempoEntrega = new NumberBox
        {
            Header = "Tiempo de entrega (días)",
            PlaceholderText = "Días que indica el proveedor",
            Minimum = 0,
            Maximum = 365,
            SmallChange = 1,
            LargeChange = 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = origen?.TiempoEntregaDias ?? 0
        };
        var acuerdo = new CheckBox
        {
            Content = "Acuerdo por contrato: hay que comprar una cantidad fija",
            IsChecked = origen?.AcuerdoContrato == true
        };
        var cantidadContrato = new NumberBox
        {
            Header = "Cantidad que se debe comprar",
            Minimum = 0,
            SmallChange = 1,
            LargeChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Value = (double)(origen?.CantidadContrato ?? 0),
            IsEnabled = origen?.AcuerdoContrato == true
        };
        var unidadContrato = new TextBox
        {
            Header = "Unidad de esa cantidad",
            Text = origen?.UnidadContrato ?? string.Empty,
            PlaceholderText = "unidades, cajas, litros…",
            IsEnabled = origen?.AcuerdoContrato == true
        };
        acuerdo.Checked += (_, _) =>
        {
            cantidadContrato.IsEnabled = true;
            unidadContrato.IsEnabled = true;
        };
        acuerdo.Unchecked += (_, _) =>
        {
            cantidadContrato.IsEnabled = false;
            unidadContrato.IsEnabled = false;
        };
        var banco = new ComboBox
        {
            Header = "Banco",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Seleccione el banco"
        };
        banco.Items.Add("Sin banco");
        foreach (var item in BancoProveedor.Todas)
        {
            banco.Items.Add(item);
        }

        var bancoActual = BancoProveedor.Normalizar(origen?.Banco);
        banco.SelectedItem = bancoActual.Length == 0 ? "Sin banco" : bancoActual;
        var bancoOtro = new TextBox
        {
            Header = "Nombre de la entidad",
            Text = origen?.BancoOtro ?? string.Empty,
            PlaceholderText = "Escriba el banco u otra entidad",
            Visibility = BancoProveedor.EsOtras(bancoActual) ? Visibility.Visible : Visibility.Collapsed
        };
        banco.SelectionChanged += (_, _) =>
        {
            var elegido = banco.SelectedItem as string ?? string.Empty;
            bancoOtro.Visibility = BancoProveedor.EsOtras(elegido) ? Visibility.Visible : Visibility.Collapsed;
        };
        var numeroCuenta = new TextBox
        {
            Header = "Número de cuenta",
            Text = origen?.NumeroCuenta ?? string.Empty,
            PlaceholderText = "Número de cuenta bancaria"
        };
        var cci = new TextBox
        {
            Header = "CCI",
            Text = origen?.Cci ?? string.Empty,
            PlaceholderText = "Código de cuenta interbancario (20 dígitos)",
            MaxLength = 20
        };
        var canal1 = CrearComboCanal(origen?.Canal1);
        var dato1 = new TextBox
        {
            Header = "Dato",
            Text = origen?.Dato1 ?? string.Empty,
            PlaceholderText = CanalDatoProveedor.De(origen?.Canal1).Placeholder
        };
        var canal2 = CrearComboCanal(origen?.Canal2 ?? CanalDatoProveedor.Web);
        var dato2 = new TextBox
        {
            Header = "Dato",
            Text = origen?.Dato2 ?? string.Empty,
            PlaceholderText = CanalDatoProveedor.De(origen?.Canal2).Placeholder
        };
        canal1.SelectionChanged += (_, _) =>
        {
            if (canal1.SelectedItem is CanalDatoOpcion opcion)
            {
                dato1.PlaceholderText = opcion.Placeholder;
            }
        };
        canal2.SelectionChanged += (_, _) =>
        {
            if (canal2.SelectedItem is CanalDatoOpcion opcion)
            {
                dato2.PlaceholderText = opcion.Placeholder;
            }
        };
        var notas = new TextBox
        {
            Header = "Notas",
            Text = origen?.Notas ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
            MaxHeight = 96
        };
        var calificacion = new ComboBox
        {
            Header = "Calificación",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var item in CalificacionProveedor.Todas)
        {
            calificacion.Items.Add(item);
        }

        calificacion.SelectedItem = CalificacionProveedor.Normalizar(origen?.Calificacion);
        var error = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["VencidoBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        static void Estirar(FrameworkElement control)
        {
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.VerticalAlignment = VerticalAlignment.Bottom;
        }

        static Grid FilaDos(FrameworkElement izquierda, FrameworkElement derecha)
        {
            Estirar(izquierda);
            Estirar(derecha);
            var fila = new Grid { ColumnSpacing = 12 };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(derecha, 1);
            fila.Children.Add(izquierda);
            fila.Children.Add(derecha);
            return fila;
        }

        Border Seccion(string titulo, params UIElement[] hijos)
        {
            var cuerpo = new StackPanel { Spacing = 10 };
            cuerpo.Children.Add(new TextBlock
            {
                Text = titulo,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 13,
                Opacity = 0.88
            });
            foreach (var hijo in hijos)
            {
                cuerpo.Children.Add(hijo);
            }

            return new Border
            {
                Style = (Style)Application.Current.Resources["TarjetaKpiStyle"],
                Padding = new Thickness(14, 12, 14, 12),
                Child = cuerpo
            };
        }

        var filaContrato = FilaDos(cantidadContrato, unidadContrato);
        filaContrato.Visibility = acuerdo.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        acuerdo.Checked += (_, _) => filaContrato.Visibility = Visibility.Visible;
        acuerdo.Unchecked += (_, _) => filaContrato.Visibility = Visibility.Collapsed;

        var panel = new StackPanel
        {
            Spacing = 12,
            Width = 700,
            Padding = new Thickness(2, 0, 2, 4)
        };
        panel.Children.Add(Seccion(
            "Identificación",
            nombre,
            FilaDos(ruc, razonSocial),
            FilaDos(tiempoEntrega, calificacion)));
        Estirar(cci);
        Estirar(bancoOtro);
        panel.Children.Add(Seccion(
            "Datos bancarios",
            FilaDos(banco, numeroCuenta),
            cci,
            bancoOtro));
        panel.Children.Add(Seccion(
            "Contrato y contacto",
            acuerdo,
            filaContrato,
            FilaDos(canal1, dato1),
            FilaDos(canal2, dato2),
            notas));
        panel.Children.Add(error);

        var dialogo = new ContentDialog
        {
            Title = esNuevo ? "Agregar proveedor" : "Editar proveedor",
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 560,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 4, 0)
            },
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };
        dialogo.Resources["ContentDialogMinWidth"] = 760d;
        dialogo.Resources["ContentDialogMaxWidth"] = 820d;
        if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out var estiloDialogo) &&
            estiloDialogo is Style estilo)
        {
            dialogo.Style = estilo;
        }

        dialogo.PrimaryButtonClick += (_, args) =>
        {
            var limpio = nombre.Text.Trim();
            if (limpio.Length == 0)
            {
                args.Cancel = true;
                error.Text = "Escriba el nombre del proveedor.";
                error.Visibility = Visibility.Visible;
                return;
            }

            if (App.Instance.Proveedores.ExisteNombre(limpio, id))
            {
                args.Cancel = true;
                error.Text = "Ya existe un proveedor con ese nombre.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var rucLimpio = ProveedorRegistro.NormalizarRuc(ruc.Text);
            if (rucLimpio.Length > 0 && rucLimpio.Length != 11)
            {
                args.Cancel = true;
                error.Text = "El RUC debe tener 11 dígitos.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var conAcuerdo = acuerdo.IsChecked == true;
            if (conAcuerdo && (double.IsNaN(cantidadContrato.Value) || cantidadContrato.Value <= 0))
            {
                args.Cancel = true;
                error.Text = "Indique la cantidad que el contrato obliga a comprar.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var bancoElegido = banco.SelectedItem as string ?? string.Empty;
            if (BancoProveedor.EsOtras(bancoElegido) && bancoOtro.Text.Trim().Length == 0)
            {
                args.Cancel = true;
                error.Text = "Escriba el nombre de la entidad bancaria.";
                error.Visibility = Visibility.Visible;
                return;
            }

            var cciLimpio = ProveedorRegistro.NormalizarCci(cci.Text);
            if (cciLimpio.Length > 0 && cciLimpio.Length != 20)
            {
                args.Cancel = true;
                error.Text = "El CCI debe tener 20 dígitos.";
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var bancoGuardar = banco.SelectedItem as string ?? string.Empty;
        if (string.Equals(bancoGuardar, "Sin banco", StringComparison.OrdinalIgnoreCase))
        {
            bancoGuardar = string.Empty;
        }

        var registro = new ProveedorRegistro
        {
            Id = id,
            Nombre = nombre.Text,
            Canal1 = (canal1.SelectedItem as CanalDatoOpcion)?.Id ?? CanalDatoProveedor.WhatsApp,
            Dato1 = dato1.Text,
            Canal2 = (canal2.SelectedItem as CanalDatoOpcion)?.Id ?? CanalDatoProveedor.Web,
            Dato2 = dato2.Text,
            Notas = notas.Text,
            Calificacion = calificacion.SelectedItem as string ?? CalificacionProveedor.SinCalificar,
            Ruc = ruc.Text,
            RazonSocial = razonSocial.Text,
            Banco = bancoGuardar,
            BancoOtro = BancoProveedor.EsOtras(bancoGuardar) ? bancoOtro.Text : string.Empty,
            NumeroCuenta = numeroCuenta.Text,
            Cci = cci.Text,
            TiempoEntregaDias = double.IsNaN(tiempoEntrega.Value) ? 0 : (int)Math.Round(tiempoEntrega.Value),
            AcuerdoContrato = acuerdo.IsChecked == true,
            CantidadContrato = acuerdo.IsChecked == true && !double.IsNaN(cantidadContrato.Value)
                ? (decimal)cantidadContrato.Value
                : 0,
            UnidadContrato = acuerdo.IsChecked == true ? unidadContrato.Text : string.Empty,
            FechaAgregado = origen?.FechaAgregado ?? DateTimeOffset.Now,
            FechaContacto = DateTimeOffset.Now
        };
        if (!App.Instance.Proveedores.Guardar(registro))
        {
            return null;
        }

        var guardado = App.Instance.Proveedores.Obtener(id) ?? registro;
        ProveedorFichaSync.Publicar(origen?.Nombre ?? guardado.Nombre, guardado);
        return guardado;
    }

    private static ComboBox CrearComboCanal(string? canalId)
    {
        var combo = new ComboBox
        {
            Header = "Red o web",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            DisplayMemberPath = "Titulo"
        };
        foreach (var opcion in CanalDatoProveedor.Opciones)
        {
            combo.Items.Add(opcion);
        }

        var elegido = CanalDatoProveedor.De(canalId);
        combo.SelectedItem = combo.Items.OfType<CanalDatoOpcion>().First(o => o.Id == elegido.Id);
        return combo;
    }
}

using Iderma.Core.Services;
using IdermaFichas.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace IdermaFichas.Services;

public static class AvisoWindowsSolicitudes
{
    private const string Accion = "action";
    private const string IdSolicitud = "id";
    private static bool _registrado;
    private static DispatcherQueue? _ui;

    public static void Preparar(DispatcherQueue? ui = null)
    {
        if (ui is not null)
        {
            _ui = ui;
        }

        if (_registrado)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.NotificationInvoked += AlInvocar;
            AppNotificationManager.Default.Register();
            _registrado = true;
        }
        catch
        {
            _registrado = false;
        }
    }

    public static void Mostrar(SolicitudClinica solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!NotificationPrefs.AllowLogistica())
        {
            return;
        }

        try
        {
            Preparar();
            var titulo = solicitud.Urgente
                ? "Solicitud urgente de clínica"
                : "Solicitud nueva de clínica";
            var detalle = $"{solicitud.Folio} · {solicitud.Enfermera} · {solicitud.Resumen}";
            var aviso = new AppNotificationBuilder()
                .AddText(titulo)
                .AddText(detalle)
                .AddText(solicitud.Detalle)
                .AddArgument(Accion, "ver")
                .AddArgument(IdSolicitud, solicitud.Id)
                .AddButton(new AppNotificationButton("Aceptar")
                    .AddArgument(Accion, "aceptar")
                    .AddArgument(IdSolicitud, solicitud.Id))
                .AddButton(new AppNotificationButton("Denegar")
                    .AddArgument(Accion, "denegar")
                    .AddArgument(IdSolicitud, solicitud.Id))
                .BuildNotification();
            AppNotificationManager.Default.Show(aviso);
        }
        catch
        {
        }
    }

    private static void AlInvocar(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        var accion = args.Arguments.TryGetValue(Accion, out var a) ? a : "ver";
        var id = args.Arguments.TryGetValue(IdSolicitud, out var i) ? i : string.Empty;
        EnUi(() => _ = ProcesarAsync(accion, id));
    }

    private static async Task ProcesarAsync(string accion, string id)
    {
        var ventana = App.Instance.MainAppWindow;
        ventana?.Activate();

        var solicitud = SolicitudesClinicaNube.Items
            .FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        // Cualquier clic en el toast abre Notificaciones; aceptar/denegar actúa ahí mismo.
        ventana?.IrANotificaciones();

        switch (accion)
        {
            case "aceptar" when solicitud is { EstaPendiente: true }:
                try
                {
                    await SolicitudesClinicaNube.AceptarAsync(solicitud);
                }
                catch (Exception ex)
                {
                    await MostrarDialogoAsync("No se pudo aceptar", ex.Message);
                }

                break;

            case "denegar" when solicitud is { EstaPendiente: true }:
                await DenegarConMotivoAsync(solicitud);
                break;
        }
    }

    private static async Task DenegarConMotivoAsync(SolicitudClinica solicitud)
    {
        var raiz = App.Instance.MainAppWindow?.Content?.XamlRoot;
        if (raiz is null)
        {
            App.Instance.MainAppWindow?.IrANotificaciones();
            return;
        }

        var motivo = new TextBox
        {
            PlaceholderText = "Escribe el motivo de la denegación",
            AcceptsReturn = true,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            MinHeight = 100,
            MaxLength = 500
        };

        var dialogo = new ContentDialog
        {
            Title = $"Denegar {solicitud.Folio}",
            Content = motivo,
            PrimaryButtonText = "Denegar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = raiz
        };

        if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await SolicitudesClinicaNube.DenegarAsync(solicitud, motivo.Text);
        }
        catch (Exception ex)
        {
            await MostrarDialogoAsync("No se pudo denegar", ex.Message);
        }
    }

    private static async Task MostrarDialogoAsync(string titulo, string mensaje)
    {
        var raiz = App.Instance.MainAppWindow?.Content?.XamlRoot;
        if (raiz is null)
        {
            return;
        }

        var dialogo = new ContentDialog
        {
            Title = titulo,
            Content = mensaje,
            CloseButtonText = "Cerrar",
            XamlRoot = raiz
        };
        await dialogo.ShowAsync();
    }

    private static void EnUi(Action accion)
    {
        var cola = _ui ?? DispatcherQueue.GetForCurrentThread();
        if (cola is null || cola.HasThreadAccess)
        {
            accion();
            return;
        }

        cola.TryEnqueue(() => accion());
    }
}

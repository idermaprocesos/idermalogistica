using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace IdermaFichas.Helpers;

public static class VentanaHelper
{
    public static void AsociarSelector(object selector)
    {
        var ventana = App.Instance.MainAppWindow;
        if (ventana is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(ventana);
        InitializeWithWindow.Initialize(selector, hwnd);
    }
}

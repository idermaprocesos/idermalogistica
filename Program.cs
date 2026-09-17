using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IdermaFichas;

public static class Program
{
    [DllImport("Microsoft.WindowsAppRuntime.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int WindowsAppRuntime_EnsureIsLoaded();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetEnvironmentVariable(string lpName, string lpValue);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBoxW(nint hWnd, string lpText, string lpCaption, uint uType);

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY_PID", GetCurrentProcessId().ToString());
            _ = WindowsAppRuntime_EnsureIsLoaded();

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            MessageBoxW(0, ex.ToString(), "Iderma Capilar App", 0x10);
        }
    }
}

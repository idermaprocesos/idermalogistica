using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IdermaFichas.Pages;

public sealed partial class AcercaDePage : Page
{
    public AcercaDePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var version = typeof(App).Assembly.GetName().Version;
        TextoEquipo.Text = Environment.MachineName;
        TextoUsuario.Text = $"{Environment.UserDomainName}\\{Environment.UserName}";
        TextoSistema.Text = RuntimeInformation.OSDescription;
        TextoRam.Text = FormatoRam();
        TextoProcesadores.Text = $"{Environment.ProcessorCount} lógicos · {RuntimeInformation.OSArchitecture}";
        TextoVersion.Text = version is null ? "—" : version.ToString();
    }

    private static string FormatoRam()
    {
        var estado = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref estado) || estado.ullTotalPhys == 0)
        {
            return "No disponible";
        }

        var totalGb = estado.ullTotalPhys / (1024d * 1024d * 1024d);
        var disponibleGb = estado.ullAvailPhys / (1024d * 1024d * 1024d);
        return $"{totalGb:0.0} GB  ·  {disponibleGb:0.0} GB libres";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

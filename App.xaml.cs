using Microsoft.UI.Xaml;
using IdermaFichas.Models;
using IdermaFichas.Services;

namespace IdermaFichas;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        try
        {
            Catalogos.Cargar();
            AlmacenHistorialProveedores.Cargar();
            AreasOperativas.Cargar();
        }
        catch
        {
        }
        try
        {
            Repositorio.Cargar();
        }
        catch
        {
            // El archivo dañado ya se aparta en Cargar; no se sustituye por ejemplos.
        }

        try
        {
            Rh.Cargar();
        }
        catch
        {
            Rh = new RhRepository();
            Rh.Cargar();
        }

        try
        {
            Preferencias.Cargar();
        }
        catch
        {
        }
        try
        {
            Registro.Cargar();
        }
        catch
        {
            Registro = new RegistroInventarioRepository();
            Registro.Cargar();
        }

        try
        {
            HistorialPrecios.Cargar();
        }
        catch
        {
            HistorialPrecios = new HistorialPreciosRepository();
            HistorialPrecios.Cargar();
        }

        try
        {
            OrdenesCompra.Cargar();
        }
        catch
        {
            OrdenesCompra = new OrdenesCompraRepository();
            OrdenesCompra.Cargar();
        }

        try
        {
            PersonalMedico.Cargar();
        }
        catch
        {
            PersonalMedico = new PersonalMedicoRepository();
            PersonalMedico.Cargar();
        }

        ActualizacionService.AplicarCopiaTrasActualizacion();
    }

    public static App Instance => (App)Current;

    public MainWindow? MainAppWindow => _window as MainWindow;

    public FichaRepository Repositorio { get; } = new();

    public RegistroInventarioRepository Registro { get; private set; } = new();

    public HistorialPreciosRepository HistorialPrecios { get; private set; } = new();

    public OrdenesCompraRepository OrdenesCompra { get; private set; } = new();

    public RhRepository Rh { get; private set; } = new();

    public PersonalMedicoRepository PersonalMedico { get; private set; } = new();

    public PreferenciasService Preferencias { get; } = new();

    public AutoguardadoService Autoguardado { get; } = new();

    public BackupService Copias { get; } = new();

    public static void RecargarDatos()
    {
        Catalogos.Cargar();
        AlmacenHistorialProveedores.Cargar();
        AreasOperativas.Cargar();
        try
        {
            Instance.Repositorio.Cargar();
        }
        catch
        {
        }
        try
        {
            Instance.Rh.Cargar();
        }
        catch
        {
            Instance.Rh = new RhRepository();
            Instance.Rh.Cargar();
        }

        try
        {
            Instance.Registro.Cargar();
        }
        catch
        {
            Instance.Registro = new RegistroInventarioRepository();
            Instance.Registro.Cargar();
        }

        try
        {
            Instance.HistorialPrecios.Cargar();
        }
        catch
        {
            Instance.HistorialPrecios = new HistorialPreciosRepository();
            Instance.HistorialPrecios.Cargar();
        }

        try
        {
            Instance.OrdenesCompra.Cargar();
        }
        catch
        {
            Instance.OrdenesCompra = new OrdenesCompraRepository();
            Instance.OrdenesCompra.Cargar();
        }

        try
        {
            Instance.PersonalMedico.Cargar();
        }
        catch
        {
            Instance.PersonalMedico = new PersonalMedicoRepository();
            Instance.PersonalMedico.Cargar();
        }

        Instance.MainAppWindow?.ActualizarMenuAreas();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}

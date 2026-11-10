using IdermaFichas.Helpers;
using IdermaFichas.Models;

namespace IdermaFichas.Services;

public static class ProveedorFichaSync
{
    public static int Contar(string nombre) => Coinciden(nombre).Count();

    public static void Publicar(string nombreAnterior, ProveedorRegistro proveedor)
    {
        var fichas = Coinciden(nombreAnterior).ToList();
        foreach (var ficha in fichas)
        {
            AplicarDatos(ficha, proveedor);
        }

        if (fichas.Count > 0)
        {
            App.Instance.Repositorio.GuardarFichas(fichas, registrarIngresoAutomatico: false);
        }

        AlmacenHistorialProveedores.ReemplazarPorNombre(
            nombreAnterior,
            ContactoProveedor.Crear(
                proveedor.Nombre,
                proveedor.Canal1,
                proveedor.Dato1,
                proveedor.Canal2,
                proveedor.Dato2));
    }

    /// <summary>
    /// Asigna el proveedor a una ficha concreta (p. ej. producto sin proveedor en orden de compra).
    /// </summary>
    public static void AsignarAFicha(FichaTecnica ficha, ProveedorRegistro proveedor)
    {
        AplicarDatos(ficha, proveedor);
        App.Instance.Repositorio.GuardarFicha(ficha, registrarIngresoAutomatico: false);
        AlmacenHistorialProveedores.ReemplazarPorNombre(
            proveedor.Nombre,
            ContactoProveedor.Crear(
                proveedor.Nombre,
                proveedor.Canal1,
                proveedor.Dato1,
                proveedor.Canal2,
                proveedor.Dato2));
    }

    private static void AplicarDatos(FichaTecnica ficha, ProveedorRegistro proveedor)
    {
        ficha.Proveedor = proveedor.Nombre.Trim();
        ficha.ContactoCanal1 = proveedor.Canal1;
        ficha.ContactoDato1 = proveedor.Dato1;
        ficha.ContactoCanal2 = proveedor.Canal2;
        ficha.ContactoDato2 = proveedor.Dato2;
        ficha.ContactosProveedorDefinidos = true;
        ficha.SincronizarCamposProveedorLegados();
    }

    public static int Quitar(string nombre)
    {
        var fichas = Coinciden(nombre).ToList();
        foreach (var ficha in fichas)
        {
            ficha.Proveedor = string.Empty;
            ficha.ContactoCanal1 = CanalDatoProveedor.WhatsApp;
            ficha.ContactoDato1 = string.Empty;
            ficha.ContactoCanal2 = CanalDatoProveedor.Web;
            ficha.ContactoDato2 = string.Empty;
            ficha.ContactoCanal3 = CanalDatoProveedor.Web;
            ficha.ContactoDato3 = string.Empty;
            ficha.ContactosProveedorDefinidos = true;
            ficha.SincronizarCamposProveedorLegados();
        }

        if (fichas.Count > 0)
        {
            App.Instance.Repositorio.GuardarFichas(fichas, registrarIngresoAutomatico: false);
        }

        AlmacenHistorialProveedores.QuitarPorNombre(nombre);
        return fichas.Count;
    }

    public static InstantaneaContactoFicha CapturarFicha(FichaTecnica ficha) => new()
    {
        FichaId = ficha.Id,
        Proveedor = ficha.Proveedor ?? string.Empty,
        ContactoCanal1 = ficha.ContactoCanal1,
        ContactoDato1 = ficha.ContactoDato1 ?? string.Empty,
        ContactoCanal2 = ficha.ContactoCanal2,
        ContactoDato2 = ficha.ContactoDato2 ?? string.Empty,
        ContactoCanal3 = ficha.ContactoCanal3,
        ContactoDato3 = ficha.ContactoDato3 ?? string.Empty,
        ContactosProveedorDefinidos = ficha.ContactosProveedorDefinidos
    };

    public static void RestaurarFichas(IReadOnlyList<InstantaneaContactoFicha> instantaneas)
    {
        if (instantaneas.Count == 0)
        {
            return;
        }

        var porId = App.Instance.Repositorio.Todas.ToDictionary(f => f.Id);
        var tocadas = new List<FichaTecnica>();
        foreach (var shot in instantaneas)
        {
            if (!porId.TryGetValue(shot.FichaId, out var ficha))
            {
                continue;
            }

            ficha.Proveedor = shot.Proveedor;
            ficha.ContactoCanal1 = shot.ContactoCanal1;
            ficha.ContactoDato1 = shot.ContactoDato1;
            ficha.ContactoCanal2 = shot.ContactoCanal2;
            ficha.ContactoDato2 = shot.ContactoDato2;
            ficha.ContactoCanal3 = shot.ContactoCanal3;
            ficha.ContactoDato3 = shot.ContactoDato3;
            ficha.ContactosProveedorDefinidos = shot.ContactosProveedorDefinidos;
            ficha.SincronizarCamposProveedorLegados();
            tocadas.Add(ficha);
        }

        if (tocadas.Count > 0)
        {
            App.Instance.Repositorio.GuardarFichas(tocadas, registrarIngresoAutomatico: false);
        }
    }

    public sealed class InstantaneaContactoFicha
    {
        public Guid FichaId { get; init; }
        public string Proveedor { get; init; } = string.Empty;
        public string ContactoCanal1 { get; init; } = string.Empty;
        public string ContactoDato1 { get; init; } = string.Empty;
        public string ContactoCanal2 { get; init; } = string.Empty;
        public string ContactoDato2 { get; init; } = string.Empty;
        public string ContactoCanal3 { get; init; } = string.Empty;
        public string ContactoDato3 { get; init; } = string.Empty;
        public bool ContactosProveedorDefinidos { get; init; }
    }

    private static IEnumerable<FichaTecnica> Coinciden(string nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return [];
        }

        return App.Instance.Repositorio.Todas.Where(f =>
            string.Equals((f.Proveedor ?? string.Empty).Trim(), limpio, StringComparison.OrdinalIgnoreCase));
    }
}

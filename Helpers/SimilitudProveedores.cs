using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IdermaFichas.Models;

namespace IdermaFichas.Helpers;

public sealed class GrupoSimilitudProveedores
{
    public required IReadOnlyList<ProveedorRegistro> Proveedores { get; init; }
}

public static partial class SimilitudProveedores
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly string[] Sufijos =
    [
        "sociedad anonima cerrada",
        "sociedad anonima",
        "empresa individual de responsabilidad limitada",
        "s a c",
        "s a",
        "sac",
        "sa",
        "eirl",
        "srl",
        "s r l",
        "ltda",
        "cia",
        "company",
        "co"
    ];

    public static IReadOnlyList<GrupoSimilitudProveedores> Buscar(IReadOnlyList<ProveedorRegistro> proveedores)
    {
        var lista = proveedores
            .Where(p => !string.IsNullOrWhiteSpace(p.Nombre))
            .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (lista.Count < 2)
        {
            return [];
        }

        var padre = Enumerable.Range(0, lista.Count).ToArray();
        int Raiz(int i)
        {
            while (padre[i] != i)
            {
                padre[i] = padre[padre[i]];
                i = padre[i];
            }

            return i;
        }

        void Unir(int a, int b)
        {
            var ra = Raiz(a);
            var rb = Raiz(b);
            if (ra != rb)
            {
                padre[rb] = ra;
            }
        }

        var claves = lista.Select(ClaveComparacion).ToArray();
        var rucs = lista.Select(p => ProveedorRegistro.NormalizarRuc(p.Ruc)).ToArray();

        for (var i = 0; i < lista.Count; i++)
        {
            for (var j = i + 1; j < lista.Count; j++)
            {
                if (Parecidos(lista[i].Nombre, lista[j].Nombre, claves[i], claves[j], rucs[i], rucs[j]))
                {
                    Unir(i, j);
                }
            }
        }

        return padre
            .Select((p, i) => (Raiz: Raiz(i), Indice: i))
            .GroupBy(x => x.Raiz)
            .Where(g => g.Count() > 1)
            .Select(g => new GrupoSimilitudProveedores
            {
                Proveedores = g
                    .Select(x => lista[x.Indice])
                    .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            })
            .OrderBy(g => g.Proveedores[0].Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool Parecidos(
        string nombreA,
        string nombreB,
        string claveA,
        string claveB,
        string rucA,
        string rucB)
    {
        if (string.Equals(nombreA.Trim(), nombreB.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rucA.Length == 11 && rucA == rucB)
        {
            return true;
        }

        if (claveA.Length == 0 || claveB.Length == 0)
        {
            return false;
        }

        if (claveA == claveB)
        {
            return true;
        }

        var corta = claveA.Length <= claveB.Length ? claveA : claveB;
        var larga = claveA.Length <= claveB.Length ? claveB : claveA;
        // Evita unir solo por una palabra corta contenida (p. ej. «andes» ⊂ «laboratorio andes»).
        if (corta.Length >= 8 && larga.Contains(corta, StringComparison.Ordinal))
        {
            var margen = larga.Length - corta.Length;
            if (margen <= Math.Max(2, corta.Length / 3))
            {
                return true;
            }
        }

        var maxLen = Math.Max(claveA.Length, claveB.Length);
        if (maxLen < 5)
        {
            return false;
        }

        var distancia = Distancia(claveA, claveB);
        var ratio = 1.0 - (double)distancia / maxLen;
        if (ratio >= 0.84)
        {
            return true;
        }

        return TokensParecidos(claveA, claveB);
    }

    private static readonly HashSet<string> TokensComunes = new(StringComparer.Ordinal)
    {
        "sa", "sac", "srl", "eirl", "ltda", "cia", "co", "company",
        "peru", "lima", "medical", "medica", "medico", "lab", "laboratorio",
        "clinica", "dental", "capilar", "salud", "pharma", "farma", "group",
        "grupo", "corp", "corporacion", "internacional", "nacional", "del", "de",
        "la", "las", "los", "el", "y", "e", "en", "para", "the", "and", "of"
    };

    private static bool TokensParecidos(string a, string b)
    {
        var ta = TokensSignificativos(a);
        var tb = TokensSignificativos(b);
        if (ta.Count == 0 || tb.Count == 0)
        {
            return false;
        }

        var inter = ta.Count(t => tb.Contains(t));
        if (inter < 2)
        {
            return false;
        }

        var union = ta.Count + tb.Count - inter;
        if (union == 0)
        {
            return false;
        }

        var jaccard = (double)inter / union;
        if (jaccard >= 0.67)
        {
            return true;
        }

        var min = Math.Min(ta.Count, tb.Count);
        return min >= 2 && inter == min;
    }

    private static HashSet<string> TokensSignificativos(string texto) =>
        texto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4 && !TokensComunes.Contains(t))
            .ToHashSet(StringComparer.Ordinal);

    private static string ClaveComparacion(ProveedorRegistro proveedor)
    {
        var texto = QuitarAcentos((proveedor.Nombre ?? string.Empty).Trim().ToLower(Cultura));
        texto = SoloLetrasNumerosEspacios().Replace(texto, " ");
        texto = Espacios().Replace(texto, " ").Trim();
        foreach (var sufijo in Sufijos.OrderByDescending(s => s.Length))
        {
            if (texto.EndsWith(" " + sufijo, StringComparison.Ordinal) || texto == sufijo)
            {
                texto = texto[..^sufijo.Length].Trim();
            }
        }

        return Espacios().Replace(texto, " ").Trim();
    }

    private static string QuitarAcentos(string valor)
    {
        var formD = valor.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int Distancia(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var j = 0; j <= m; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + costo);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SoloLetrasNumerosEspacios();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}

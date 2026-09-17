using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using IdermaFichas.Models;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using ZXing.Rendering;

namespace IdermaFichas.Services;

public static class SimboloEtiquetaService
{
    public static string Titulo(TipoSimboloEtiqueta tipo) => tipo switch
    {
        TipoSimboloEtiqueta.Qr => "Código QR",
        TipoSimboloEtiqueta.Code128 => "Código de barras (Code 128)",
        TipoSimboloEtiqueta.Code39 => "Code 39",
        TipoSimboloEtiqueta.Ean13 => "EAN-13",
        TipoSimboloEtiqueta.DataMatrix => "Data Matrix",
        TipoSimboloEtiqueta.Pdf417 => "PDF417",
        TipoSimboloEtiqueta.Aztec => "Aztec",
        _ => "Símbolo"
    };

    public static bool EsBidimensional(TipoSimboloEtiqueta tipo) => tipo is
        TipoSimboloEtiqueta.Qr or
        TipoSimboloEtiqueta.DataMatrix or
        TipoSimboloEtiqueta.Aztec;

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    public static void Precargar(
        IEnumerable<string> contenidos,
        TipoSimboloEtiqueta tipo,
        float tamanoMm,
        IProgress<(int Hechos, int Total)>? progreso = null)
    {
        var lista = contenidos
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lista.Count == 0)
        {
            return;
        }

        var hechos = 0;
        Parallel.ForEach(lista, contenido =>
        {
            Generar(contenido, tipo, tamanoMm);
            var actual = Interlocked.Increment(ref hechos);
            progreso?.Report((actual, lista.Count));
        });
    }

    public static byte[] Generar(string contenido, TipoSimboloEtiqueta tipo, float tamanoMm)
    {
        var clave = $"{tipo}|{tamanoMm:0.#}|{contenido.Trim()}";
        return Cache.GetOrAdd(clave, _ => Crear(contenido, tipo, tamanoMm));
    }

    private static byte[] Crear(string contenido, TipoSimboloEtiqueta tipo, float tamanoMm)
    {
        var px = Math.Max(48, (int)Math.Round(tamanoMm * 8));
        var formato = Formato(tipo);
        var texto = PrepararContenido(contenido, tipo);

        var opciones = tipo == TipoSimboloEtiqueta.Qr
            ? new QrCodeEncodingOptions
            {
                Width = px,
                Height = px,
                Margin = 1,
                CharacterSet = "UTF-8",
                ErrorCorrection = ErrorCorrectionLevel.M
            }
            : new EncodingOptions
            {
                Width = EsBidimensional(tipo) ? px : Math.Max(px * 3, 180),
                Height = tipo == TipoSimboloEtiqueta.Pdf417 ? Math.Max(px / 2, 48) : px,
                Margin = 1,
                PureBarcode = true
            };

        opciones.Hints[EncodeHintType.CHARACTER_SET] = "UTF-8";

        var escritor = new BarcodeWriterPixelData
        {
            Format = formato,
            Options = opciones
        };

        try
        {
            return APng(escritor.Write(texto));
        }
        catch (Exception) when (tipo == TipoSimboloEtiqueta.Ean13)
        {
            return Crear(contenido, TipoSimboloEtiqueta.Code128, tamanoMm);
        }
    }

    private static BarcodeFormat Formato(TipoSimboloEtiqueta tipo) => tipo switch
    {
        TipoSimboloEtiqueta.Qr => BarcodeFormat.QR_CODE,
        TipoSimboloEtiqueta.Code128 => BarcodeFormat.CODE_128,
        TipoSimboloEtiqueta.Code39 => BarcodeFormat.CODE_39,
        TipoSimboloEtiqueta.Ean13 => BarcodeFormat.EAN_13,
        TipoSimboloEtiqueta.DataMatrix => BarcodeFormat.DATA_MATRIX,
        TipoSimboloEtiqueta.Pdf417 => BarcodeFormat.PDF_417,
        TipoSimboloEtiqueta.Aztec => BarcodeFormat.AZTEC,
        _ => BarcodeFormat.QR_CODE
    };

    private static string PrepararContenido(string contenido, TipoSimboloEtiqueta tipo)
    {
        var texto = string.IsNullOrWhiteSpace(contenido) ? "IDERMA" : contenido.Trim();
        if (tipo != TipoSimboloEtiqueta.Ean13)
        {
            return texto;
        }

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length is 12 or 13)
        {
            return digitos.Length == 13 ? digitos[..12] : digitos;
        }

        throw new ArgumentException("EAN-13 requiere 12 o 13 dígitos.");
    }

    private static byte[] APng(PixelData datos)
    {
        using var bitmap = new Bitmap(datos.Width, datos.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, datos.Width, datos.Height);
        var bits = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(datos.Pixels, 0, bits.Scan0, datos.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bits);
        }

        using var memoria = new MemoryStream();
        bitmap.Save(memoria, ImageFormat.Png);
        return memoria.ToArray();
    }
}

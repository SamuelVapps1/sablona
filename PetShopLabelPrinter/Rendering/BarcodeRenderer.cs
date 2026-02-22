using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using ZXing;
using ZXing.Common;

namespace PetShopLabelPrinter.Rendering
{
    /// <summary>
    /// Renders barcode for WPF preview and PDF print. Never throws; fails gracefully.
    /// </summary>
    public static class BarcodeRenderer
    {
        private static BarcodeFormat GetFormat(string format)
        {
            return string.Equals(format, "CODE128", StringComparison.OrdinalIgnoreCase)
                ? BarcodeFormat.CODE_128
                : BarcodeFormat.EAN_13;
        }

        /// <summary>
        /// Validates barcode value for the given format. Returns (isValid, errorMessage).
        /// EAN13: digits only, length 12 or 13. CODE128: non-empty after trim.
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateBarcodeValue(string? value, string? format)
        {
            var trimmed = value?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return (false, null);

            var fmt = format ?? "EAN13";
            if (string.Equals(fmt, "CODE128", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null);
            }

            var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());
            var hasNonDigit = trimmed.Any(c => !char.IsDigit(c) && !char.IsWhiteSpace(c));
            if (hasNonDigit || digitsOnly.Length != 12 && digitsOnly.Length != 13)
                return (false, "Neplatný EAN-13");
            return (true, null);
        }

        /// <summary>
        /// Normalizes value for encoding: EAN13 = digits only; CODE128 = trim.
        /// </summary>
        public static string NormalizeBarcodeValue(string? value, string? format)
        {
            var trimmed = value?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed)) return trimmed;
            if (string.Equals(format, "CODE128", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return new string(trimmed.Where(char.IsDigit).ToArray());
        }

        public static bool TryEncode(string value, string format, int widthPx, int heightPx, out byte[]? pngBytes, int marginPx = 0)
        {
            pngBytes = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = NormalizeBarcodeValue(value, format);
            if (string.IsNullOrEmpty(normalized)) return false;
            var (isValid, _) = ValidateBarcodeValue(normalized, format);
            if (!isValid) return false;
            try
            {
                var writer = new BarcodeWriter
                {
                    Format = GetFormat(format),
                    Options = new EncodingOptions
                    {
                        Width = widthPx,
                        Height = heightPx,
                        Margin = Math.Max(0, marginPx),
                        PureBarcode = false
                    }
                };
                var bmp = writer.Write(normalized);
                if (bmp == null) return false;
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                pngBytes = ms.ToArray();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void DrawToWpf(DrawingContext dc, string value, string format, Rect rect, bool showText)
        {
            try
            {
                var w = (int)Math.Max(1, rect.Width);
                var h = (int)Math.Max(1, rect.Height);
                if (!TryEncode(value, format, w, h, out var pngBytes, 0) || pngBytes == null) return;
                using var ms = new MemoryStream(pngBytes);
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                if (frame == null) return;
                dc.DrawImage(frame, rect);
            }
            catch { }
        }

        public static void DrawToPdf(XGraphics gfx, string value, string format, double xMm, double yMm, double wMm, double hMm, bool showText)
        {
            try
            {
                var wPx = Math.Max(80, (int)Math.Round(wMm * 12.0));
                var hPx = Math.Max(26, (int)Math.Round(hMm * 12.0));
                if (!TryEncode(value, format, wPx, hPx, out var pngBytes, 0) || pngBytes == null) return;
                var ms = new MemoryStream(pngBytes);
                var ximg = XImage.FromStream(ms);
                gfx.DrawImage(ximg, XUnit.FromMillimeter(xMm), XUnit.FromMillimeter(yMm), XUnit.FromMillimeter(wMm), XUnit.FromMillimeter(hMm));
            }
            catch { }
        }
    }
}

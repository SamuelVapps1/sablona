using System;
using System.Diagnostics;
using System.IO;

namespace PetShopLabelPrinter.Services
{
    public static class SafeDocumentLauncher
    {
        /// <summary>Opens a PDF with the shell only if path is rooted, exists, and has .pdf extension.</summary>
        public static bool TryOpenPdf(string path, out string? errorMessage)
        {
            errorMessage = null;
            if (!TryValidatePdfPath(path, out errorMessage))
                return false;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>Print-to verb for a PDF; validates path first.</summary>
        public static bool TryPrintPdfToPrinter(string pdfPath, string printerName, out string? errorMessage)
        {
            errorMessage = null;
            if (!TryValidatePdfPath(pdfPath, out errorMessage))
                return false;

            if (string.IsNullOrWhiteSpace(printerName))
            {
                errorMessage = "Chýba názov tlačiarne.";
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pdfPath,
                    Verb = "printto",
                    Arguments = $"\"{printerName}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryValidatePdfPath(string path, out string? errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "Prázdna cesta.";
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                errorMessage = "Cesta musí byť absolútna.";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }

            if (!string.Equals(Path.GetExtension(full), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Povolené sú len súbory PDF.";
                return false;
            }

            if (!File.Exists(full))
            {
                errorMessage = "Súbor neexistuje.";
                return false;
            }

            return true;
        }
    }
}

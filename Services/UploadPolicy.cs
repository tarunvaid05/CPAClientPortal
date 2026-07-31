using Microsoft.AspNetCore.Http;

namespace JyotiIyerCPA.Services
{
    /// <summary>
    /// Upload limits shared by every endpoint that accepts a file.
    /// 25 MB covers scanned multi-page tax documents (a phone-scanned return is
    /// typically 5-20 MB) while keeping peak memory bounded, because the storage
    /// providers hold the file in memory while encrypting it. Raising this past
    /// ~28 MB also requires raising the Kestrel and IIS request body limits.
    /// </summary>
    public static class UploadPolicy
    {
        public const long MaxBytes = 25L * 1024 * 1024;

        public static int MaxMegabytes => (int)(MaxBytes / (1024 * 1024));

        /// <summary>Extensions a client or admin may send through the portal.</summary>
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".tif", ".tiff",
            ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"
        };

        /// <summary>
        /// Returns null when the file is acceptable, otherwise a message safe to show the user.
        /// </summary>
        public static string? Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "No file provided.";
            }

            if (file.Length > MaxBytes)
            {
                return $"That file is {file.Length / (1024.0 * 1024.0):F1} MB. The maximum size is {MaxMegabytes} MB.";
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                return "Unsupported file type. Please upload a PDF, image (JPG, PNG, HEIC, TIFF), Word or Excel document, CSV, or text file.";
            }

            return null;
        }
    }
}

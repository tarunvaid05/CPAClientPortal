namespace JyotiIyerCPA.Options
{
    public class FileStorageOptions
    {
        // Root directory where encrypted files are stored (outside web root recommended)
        public string RootPath { get; set; } = "App_Data/Uploads";

        // Base64-encoded 32-byte key for AES-256 (required for encryption at rest)
        public string EncryptionKey { get; set; } = string.Empty;
    }
}


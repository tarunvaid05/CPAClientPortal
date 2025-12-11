using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using JyotiIyerCPA.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JyotiIyerCPA.Services
{
    /// <summary>
    /// Azure Blob Storage implementation with client-side AES-256-GCM encryption.
    /// Files are encrypted before upload and decrypted after download.
    /// Blob naming pattern: {userId}/{guid}.bin for user isolation.
    /// </summary>
    public class AzureBlobFileStorage : IFileStorage
    {
        private readonly BlobContainerClient _containerClient;
        private readonly byte[] _key; // 32 bytes AES-256
        private readonly ILogger<AzureBlobFileStorage> _logger;

        public AzureBlobFileStorage(IOptions<FileStorageOptions> options, ILogger<AzureBlobFileStorage> logger)
        {
            _logger = logger;
            var opts = options.Value;

            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException("Azure Blob Storage connection string is not configured. Set 'Storage:ConnectionString'.");
            }

            if (string.IsNullOrWhiteSpace(opts.ContainerName))
            {
                throw new InvalidOperationException("Azure Blob Storage container name is not configured. Set 'Storage:ContainerName'.");
            }

            if (string.IsNullOrWhiteSpace(opts.EncryptionKey))
            {
                throw new InvalidOperationException("File storage encryption key is not configured. Set 'Storage:EncryptionKey' to a base64 32-byte key.");
            }

            _key = Convert.FromBase64String(opts.EncryptionKey);
            if (_key.Length != 32)
            {
                throw new InvalidOperationException("Encryption key must decode to 32 bytes for AES-256.");
            }

            var blobServiceClient = new BlobServiceClient(opts.ConnectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(opts.ContainerName);

            // Ensure container exists (created if not)
            _containerClient.CreateIfNotExists(PublicAccessType.None);

            _logger.LogInformation("[AzureBlobStorage] Initialized with container: {Container}", opts.ContainerName);
        }

        public async Task<(string storedFileName, long size, string sha256)> SaveEncryptedAsync(
            string ownerUserId, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Empty file.");

            var blobName = $"{Sanitize(ownerUserId)}/{Guid.NewGuid():N}.bin";
            var blobClient = _containerClient.GetBlobClient(blobName);

            // Read file into memory for hashing and encryption
            byte[] plaintext;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                plaintext = ms.ToArray();
            }

            // Compute SHA256 of plaintext for integrity verification
            string sha256;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(plaintext);
                sha256 = Convert.ToHexString(hash);
            }

            // Encrypt with AES-GCM: [nonce(12)][ciphertext][tag(16)]
            var nonce = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];

            using (var gcm = new AesGcm(_key, 16))
            {
                gcm.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            // Combine: nonce + ciphertext + tag
            var encryptedData = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, encryptedData, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, encryptedData, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, encryptedData, nonce.Length + ciphertext.Length, tag.Length);

            // Upload to Azure Blob Storage
            using (var uploadStream = new MemoryStream(encryptedData))
            {
                await blobClient.UploadAsync(uploadStream, overwrite: true, ct);
            }

            _logger.LogInformation("[AzureBlobStorage] Uploaded encrypted blob: {BlobName} ({Size} bytes)", blobName, file.Length);
            return (blobName, file.Length, sha256);
        }

        public async Task<Stream> OpenDecryptedReadStreamAsync(string storedFileName, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(storedFileName);

            if (!await blobClient.ExistsAsync(ct))
            {
                throw new FileNotFoundException("Blob not found.", storedFileName);
            }

            // Download encrypted blob
            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(ct);
            var encryptedData = downloadResult.Content.ToArray();

            // Layout: [nonce(12)][ciphertext][tag(16)]
            const int nonceLen = 12;
            const int tagLen = 16;

            if (encryptedData.Length < nonceLen + tagLen)
            {
                throw new InvalidDataException("Corrupt encrypted blob - too short.");
            }

            var nonce = new byte[nonceLen];
            var tag = new byte[tagLen];
            var cipherLen = encryptedData.Length - nonceLen - tagLen;
            var ciphertext = new byte[cipherLen];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, nonceLen);
            Buffer.BlockCopy(encryptedData, nonceLen, ciphertext, 0, cipherLen);
            Buffer.BlockCopy(encryptedData, nonceLen + cipherLen, tag, 0, tagLen);

            // Decrypt
            var plaintext = new byte[cipherLen];
            using (var gcm = new AesGcm(_key, 16))
            {
                gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            _logger.LogInformation("[AzureBlobStorage] Decrypted blob: {BlobName}", storedFileName);
            return new MemoryStream(plaintext, writable: false);
        }

        public async Task DeleteAsync(string storedFileName, CancellationToken ct = default)
        {
            var blobClient = _containerClient.GetBlobClient(storedFileName);
            var deleted = await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

            if (deleted)
            {
                _logger.LogInformation("[AzureBlobStorage] Deleted blob: {BlobName}", storedFileName);
            }
        }

        private static string Sanitize(string input)
        {
            // Azure blob names allow most characters, but we sanitize for consistency
            var invalidChars = new[] { '\\', '#', '?', '%', '[', ']' };
            foreach (var c in invalidChars)
            {
                input = input.Replace(c, '_');
            }
            return input;
        }
    }
}

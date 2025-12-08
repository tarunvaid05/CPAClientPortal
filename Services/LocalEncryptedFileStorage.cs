using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JyotiIyerCPA.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JyotiIyerCPA.Services
{
    public class LocalEncryptedFileStorage : IFileStorage
    {
        private readonly string _root;
        private readonly byte[] _key; // 32 bytes AES-256
        private readonly ILogger<LocalEncryptedFileStorage> _logger;
        private readonly IWebHostEnvironment _env;

        public LocalEncryptedFileStorage(IOptions<FileStorageOptions> options,
            ILogger<LocalEncryptedFileStorage> logger,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
            var opts = options.Value;
            _root = Path.IsPathRooted(opts.RootPath) ? opts.RootPath : Path.Combine(env.ContentRootPath, opts.RootPath);
            Directory.CreateDirectory(_root);

            if (string.IsNullOrWhiteSpace(opts.EncryptionKey))
            {
                throw new InvalidOperationException("File storage encryption key is not configured. Set 'Storage:EncryptionKey' to a base64 32-byte key.");
            }
            _key = Convert.FromBase64String(opts.EncryptionKey);
            if (_key.Length != 32)
                throw new InvalidOperationException("Encryption key must decode to 32 bytes for AES-256.");
        }

        public async Task<(string storedFileName, long size, string sha256)> SaveEncryptedAsync(string ownerUserId, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Empty file.");

            var storedName = $"{Guid.NewGuid():N}.bin";
            var fullPath = Path.Combine(_root, Sanitize(ownerUserId));
            Directory.CreateDirectory(fullPath);
            var filePath = Path.Combine(fullPath, storedName);

            // Compute SHA256 of plaintext
            string sha256;
            using (var sha = SHA256.Create())
            using (var src = file.OpenReadStream())
            {
                var hash = await sha.ComputeHashAsync(src, ct);
                sha256 = Convert.ToHexString(hash);
            }

            // Encrypt and save: [nonce(12)][ciphertext][tag(16)] with AES-GCM
            using (var src = file.OpenReadStream())
            using (var dest = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var nonce = RandomNumberGenerator.GetBytes(12);
                await dest.WriteAsync(nonce, 0, nonce.Length, ct);

                var plaintext = new byte[src.Length];
                var read = await src.ReadAsync(plaintext.AsMemory(0, plaintext.Length), ct);
                if (read != plaintext.Length) throw new IOException("Failed to read upload stream.");

                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[16];
                using (var gcm = new AesGcm(_key))
                {
                    gcm.Encrypt(nonce, plaintext, ciphertext, tag);
                }

                await dest.WriteAsync(ciphertext, 0, ciphertext.Length, ct);
                await dest.WriteAsync(tag, 0, tag.Length, ct);
            }

            _logger.LogInformation("[Storage] Saved encrypted file for {Owner} as {StoredName}", ownerUserId, storedName);
            return (Path.Combine(Sanitize(ownerUserId), storedName), file.Length, sha256);
        }

        public async Task<Stream> OpenDecryptedReadStreamAsync(string storedFileName, CancellationToken ct = default)
        {
            var filePath = Path.Combine(_root, storedFileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Stored file not found.", filePath);

            var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                // Layout: [nonce(12)][ciphertext][tag(16)]
                var nonce = new byte[12];
                var tag = new byte[16];
                var totalLen = fs.Length;
                if (totalLen < nonce.Length + tag.Length)
                    throw new InvalidDataException("Corrupt encrypted file.");

                await fs.ReadAsync(nonce, 0, nonce.Length, ct);
                var cipherLen = (int)(totalLen - nonce.Length - tag.Length);
                var ciphertext = ArrayPool<byte>.Shared.Rent(cipherLen);
                try
                {
                    var read = await fs.ReadAsync(ciphertext.AsMemory(0, cipherLen), ct);
                    if (read != cipherLen) throw new IOException("Failed to read ciphertext.");
                    await fs.ReadAsync(tag, 0, tag.Length, ct);

                    var plaintext = new byte[cipherLen];
                    using (var gcm = new AesGcm(_key))
                    {
                        gcm.Decrypt(nonce, ciphertext.AsSpan(0, cipherLen), tag, plaintext);
                    }

                    // Return memory stream (caller disposes)
                    return new MemoryStream(plaintext, writable: false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(ciphertext);
                    await fs.DisposeAsync();
                }
            }
            catch
            {
                await fs.DisposeAsync();
                throw;
            }
        }

        public Task DeleteAsync(string storedFileName, CancellationToken ct = default)
        {
            var filePath = Path.Combine(_root, storedFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        private static string Sanitize(string input)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                input = input.Replace(c, '_');
            }
            foreach (var c in Path.GetInvalidPathChars())
            {
                input = input.Replace(c, '_');
            }
            return input;
        }
    }
}


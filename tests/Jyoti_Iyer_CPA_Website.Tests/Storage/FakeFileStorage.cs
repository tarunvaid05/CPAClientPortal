using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Http;

namespace Tests.Storage
{
    public class FakeFileStorage : IFileStorage
    {
        public Task DeleteAsync(string storedFileName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Stream> OpenDecryptedReadStreamAsync(string storedFileName, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        public Task<(string storedFileName, long size, string sha256)> SaveEncryptedAsync(string ownerUserId, IFormFile file, CancellationToken ct = default)
            => Task.FromResult<(string, long, string)>(($"{ownerUserId}/test.bin", file.Length, "ABC123"));
    }
}


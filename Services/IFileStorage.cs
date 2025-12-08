using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace JyotiIyerCPA.Services
{
    public interface IFileStorage
    {
        Task<(string storedFileName, long size, string sha256)> SaveEncryptedAsync(string ownerUserId, IFormFile file, CancellationToken ct = default);
        Task<Stream> OpenDecryptedReadStreamAsync(string storedFileName, CancellationToken ct = default);
        Task DeleteAsync(string storedFileName, CancellationToken ct = default);
    }
}


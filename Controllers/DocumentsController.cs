using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JyotiIyerCPA.Data;
using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JyotiIyerCPA.Controllers
{
    [Authorize]
    [Route("[controller]/[action]")]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFileStorage _storage;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(ApplicationDbContext db, IFileStorage storage, UserManager<ApplicationUser> userManager, ILogger<DocumentsController> logger)
        {
            _db = db;
            _storage = storage;
            _userManager = userManager;
            _logger = logger;
        }

        // Clients upload only (per requirements). Admins are forbidden to upload.
        [HttpPost]
        [RequestSizeLimit(long.MaxValue)] // per requirement: uncapped size (note: still subject to server limits)
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Upload(IFormFile file, string category = "", CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
            {
                return Forbid(); // admins cannot upload
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "No file provided." });
            }

            var (storedFileName, size, sha) = await _storage.SaveEncryptedAsync(user.Id, file, ct);

            var doc = new Document
            {
                OwnerUserId = user.Id,
                UploadedByUserId = user.Id,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                ContentType = file.ContentType ?? string.Empty,
                Size = size,
                Category = category ?? string.Empty,
                UploadedAt = DateTimeOffset.UtcNow,
                Sha256 = sha
            };
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("[Docs] {UserId} uploaded {DocId} ({Name})", user.Id, doc.Id, doc.OriginalFileName);

            return Ok(new { success = true, id = doc.Id, name = doc.OriginalFileName, size = doc.Size, category = doc.Category, uploadedAt = doc.UploadedAt });
        }

        [HttpGet]
        public async Task<IActionResult> List(string userId = null, CancellationToken ct = default)
        {
            var caller = await _userManager.GetUserAsync(User);
            if (caller == null) return Unauthorized();
            var isAdmin = await _userManager.IsInRoleAsync(caller, "Admin");

            IQueryable<Document> query = _db.Documents.AsNoTracking().Where(d => !d.IsDeleted);
            if (isAdmin)
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(d => d.OwnerUserId == userId);
                }
            }
            else
            {
                // Clients see their own documents only
                query = query.Where(d => d.OwnerUserId == caller.Id);
            }

            var list = await query
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    id = d.Id,
                    name = d.OriginalFileName,
                    size = d.Size,
                    category = d.Category,
                    uploadedAt = d.UploadedAt,
                    ownerUserId = d.OwnerUserId
                }).ToListAsync(ct);

            return Ok(list);
        }

        // Admin-only download
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("Download/{id}")]
        public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
        {
            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
            if (doc == null) return NotFound();
            var stream = await _storage.OpenDecryptedReadStreamAsync(doc.StoredFileName, ct);
            return File(stream, doc.ContentType ?? "application/octet-stream", doc.OriginalFileName);
        }

        // Admin-only delete (soft delete + remove encrypted file)
        [HttpDelete]
        [Authorize(Roles = "Admin")]
        [Route("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
            if (doc == null) return NotFound(new { success = false, message = "Document not found." });

            doc.IsDeleted = true;
            doc.DeletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            try { await _storage.DeleteAsync(doc.StoredFileName, ct); } catch { /* best-effort */ }
            _logger.LogInformation("[Docs] Admin {Admin} deleted {DocId} ({Name})", User?.Identity?.Name, doc.Id, doc.OriginalFileName);
            return Ok(new { success = true });
        }
    }
}


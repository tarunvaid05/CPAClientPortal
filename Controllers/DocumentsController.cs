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
using Microsoft.Extensions.Configuration;
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
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public DocumentsController(ApplicationDbContext db, IFileStorage storage, UserManager<ApplicationUser> userManager, ILogger<DocumentsController> logger, IEmailSender emailSender, IConfiguration configuration)
        {
            _db = db;
            _storage = storage;
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        // Clients upload only (per requirements). Admins are forbidden to upload.
        [HttpPost]
        [RequestSizeLimit(UploadPolicy.MaxBytes)]
        public async Task<IActionResult> Upload(IFormFile file, string category = "", CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
            {
                return Forbid(); // admins cannot upload
            }

            var uploadError = UploadPolicy.Validate(file);
            if (uploadError != null)
            {
                return BadRequest(new { success = false, message = uploadError });
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

            // Send notification to admin
            var adminEmail = _configuration["Email:AdminNotificationEmail"];
            if (!string.IsNullOrEmpty(adminEmail))
            {
                var clientName = $"{user.FirstName} {user.LastName}";
                await _emailSender.SendDocumentUploadNotificationAsync(adminEmail, clientName, category ?? string.Empty, file.FileName, DateTime.Now);
            }

            return Ok(new { success = true, id = doc.Id, name = doc.OriginalFileName, size = doc.Size, category = doc.Category, uploadedAt = doc.UploadedAt });
        }

        /// <summary>
        /// Admin uploads a document for a specific client.
        /// Per Section 3.6: Document stored with OwnerUserId = Client, UploadedByUserId = Admin.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadPolicy.MaxBytes)]
        public async Task<IActionResult> AdminUpload(IFormFile file, string clientUserId, string category, string? adminNotes = null, CancellationToken ct = default)
        {
            // Validate admin is logged in
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null) return Unauthorized();

            var uploadError = UploadPolicy.Validate(file);
            if (uploadError != null)
            {
                return BadRequest(new { success = false, message = uploadError });
            }

            // Validate category is one of the allowed admin categories (Section 3.6)
            var allowedCategories = new[] { "Tax Return - For Review", "Tax Return - Finalized", "Requires Signature" };
            if (string.IsNullOrWhiteSpace(category) || !allowedCategories.Contains(category))
            {
                return BadRequest(new { success = false, message = "Invalid category. Must be one of: Tax Return - For Review, Tax Return - Finalized, Requires Signature" });
            }

            // Validate client user exists
            if (string.IsNullOrWhiteSpace(clientUserId))
            {
                return BadRequest(new { success = false, message = "Client user ID is required." });
            }

            var client = await _userManager.FindByIdAsync(clientUserId);
            if (client == null)
            {
                return BadRequest(new { success = false, message = "Client not found." });
            }

            // Validate target is a Client role user
            var isClient = await _userManager.IsInRoleAsync(client, "Client");
            if (!isClient)
            {
                return BadRequest(new { success = false, message = "Target user is not a client." });
            }

            // Use existing encryption (AES-256-GCM) to store file
            var (storedFileName, size, sha) = await _storage.SaveEncryptedAsync(clientUserId, file, ct);

            // Create document with OwnerUserId = client, UploadedByUserId = admin
            var doc = new Document
            {
                OwnerUserId = clientUserId,
                UploadedByUserId = admin.Id,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                ContentType = file.ContentType ?? string.Empty,
                Size = size,
                Category = category,
                UploadedAt = DateTimeOffset.UtcNow,
                Sha256 = sha
            };
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);

            // Create DocumentWorkflow record to track the document sent to client
            var workflow = new DocumentWorkflow
            {
                ClientUserId = clientUserId,
                AdminUserId = admin.Id,
                DocumentId = doc.Id,
                AdminNotes = adminNotes,
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.DocumentWorkflows.Add(workflow);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("[Docs] Admin {AdminId} uploaded {DocId} ({Name}) for client {ClientId} with workflow {WorkflowId}", admin.Id, doc.Id, doc.OriginalFileName, clientUserId, workflow.Id);

            // Send notification to client
            if (!string.IsNullOrEmpty(client.Email))
            {
                var clientName = $"{client.FirstName} {client.LastName}";
                await _emailSender.SendDocumentSentNotificationAsync(client.Email, clientName, category, file.FileName, adminNotes);
            }

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

        // Download: Owner or Admin only (per requirements 3.3)
        [HttpGet("~/Documents/Download/{id}")]
        public async Task<IActionResult> Download(Guid id, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
            if (doc == null) return NotFound();

            // Security: Only owner or admin can download
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = doc.OwnerUserId == user.Id;

            if (!isAdmin && !isOwner)
            {
                _logger.LogWarning("[Docs] Unauthorized download attempt: User {UserId} tried to access document {DocId} owned by {OwnerId}", 
                    user.Id, doc.Id, doc.OwnerUserId);
                return Forbid();
            }

            var stream = await _storage.OpenDecryptedReadStreamAsync(doc.StoredFileName, ct);
            _logger.LogInformation("[Docs] {UserId} downloaded {DocId} ({Name})", user.Id, doc.Id, doc.OriginalFileName);
            return File(stream, doc.ContentType ?? "application/octet-stream", doc.OriginalFileName);
        }

        // View/Preview: Owner or Admin only - displays inline in browser
        [HttpGet("~/Documents/Preview/{id}")]
        public async Task<IActionResult> Preview(Guid id, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
            if (doc == null) return NotFound();

            // Security: Only owner or admin can view
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = doc.OwnerUserId == user.Id;

            if (!isAdmin && !isOwner)
            {
                _logger.LogWarning("[Docs] Unauthorized view attempt: User {UserId} tried to access document {DocId} owned by {OwnerId}",
                    user.Id, doc.Id, doc.OwnerUserId);
                return Forbid();
            }

            var stream = await _storage.OpenDecryptedReadStreamAsync(doc.StoredFileName, ct);
            _logger.LogInformation("[Docs] {UserId} viewed {DocId} ({Name})", user.Id, doc.Id, doc.OriginalFileName);

            // Return without filename to display inline (browser will render PDFs, images, etc.)
            return File(stream, doc.ContentType ?? "application/octet-stream");
        }

        // Delete (soft delete + remove encrypted file) - Owner or Admin only
        [HttpDelete("~/Documents/Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);
            if (doc == null) return NotFound(new { success = false, message = "Document not found." });

            // Security: Only owner or admin can delete
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = doc.OwnerUserId == user.Id;

            if (!isAdmin && !isOwner)
            {
                _logger.LogWarning("[Docs] Unauthorized delete attempt: User {UserId} tried to delete document {DocId} owned by {OwnerId}",
                    user.Id, doc.Id, doc.OwnerUserId);
                return Forbid();
            }

            doc.IsDeleted = true;
            doc.DeletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            try { await _storage.DeleteAsync(doc.StoredFileName, ct); } catch { /* best-effort */ }
            _logger.LogInformation("[Docs] {UserId} deleted {DocId} ({Name})", user.Id, doc.Id, doc.OriginalFileName);
            return Ok(new { success = true });
        }
    }
}


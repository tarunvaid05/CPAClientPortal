using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using JyotiIyerCPA.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace JyotiIyerCPA.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private static int CurrentYear => DateTime.Now.Year;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AdminController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IFileStorage _storage;

        public AdminController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext db, ILogger<AdminController> logger, IEmailSender emailSender, IFileStorage storage)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _logger = logger;
            _emailSender = emailSender;
            _storage = storage;
        }

        [HttpGet]
        public async Task<IActionResult> Clients(string query = null)
        {
            // Only return users in the Client role (exclude Admins)
            var clientUsers = await _userManager.GetUsersInRoleAsync("Client");
            var users = clientUsers
                .Where(u => u.EmailConfirmed)
                .OrderBy(u => u.Email)
                .Select(u => new { id = u.Id, name = (u.FirstName + " " + u.LastName).Trim(), email = u.Email })
                .ToList();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.ToLowerInvariant();
                users = users.Where(u => (u.name ?? "").ToLower().Contains(q) || (u.email ?? "").ToLower().Contains(q)).ToList();
            }
            return Ok(users);
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var displayName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : "Admin";

            // Load documents safely (handle missing table by returning empty set)
            var documents = await SafeLoadDocumentsAsync();

            // Build clients list with counts and last upload
            var docGroups = documents
                .Where(d => !d.IsDeleted)
                .GroupBy(d => d.OwnerUserId)
                .Select(g => new { OwnerUserId = g.Key, Count = g.Count(), LastUpload = g.Max(x => x.UploadedAt) })
                .ToDictionary(x => x.OwnerUserId, x => new { x.Count, x.LastUpload });

            // Only show users in the Client role (exclude Admins from client list)
            var users = (await _userManager.GetUsersInRoleAsync("Client"))
                .OrderBy(u => u.Email)
                .ToList();

            var clientCards = users.Select(u => new ClientViewModel
            {
                Id = u.Id,
                Name = string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? "Client") : ($"{u.FirstName} {u.LastName}").Trim(),
                Email = u.Email ?? string.Empty,
                Initials = string.Concat((u.FirstName ?? string.Empty).DefaultIfEmpty(' ').First(), (u.LastName ?? string.Empty).DefaultIfEmpty(' ').First()).Trim().ToUpper(),
                DocumentCount = docGroups.TryGetValue(u.Id, out var g) ? g.Count : 0,
                LastUpload = docGroups.TryGetValue(u.Id, out var g2) ? g2.LastUpload.LocalDateTime : DateTime.MinValue
            }).ToList();

            // Recent uploads pane - includes both client uploads and admin-sent documents
            var recentUploads = documents
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Take(50)
                .Join(users, d => d.OwnerUserId, u => u.Id, (d, u) => new AdminUploadViewModel
                {
                    Id = d.Id,
                    OwnerUserId = d.OwnerUserId,
                    Category = string.IsNullOrEmpty(d.Category) ? "Other" : d.Category,
                    FileName = d.OriginalFileName,
                    FileType = string.IsNullOrEmpty(d.Category) ? "Document" : d.Category,
                    ClientName = string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? "Client") : ($"{u.FirstName} {u.LastName}").Trim(),
                    UploadDate = d.UploadedAt.LocalDateTime,
                    Status = "Uploaded",
                    UploadSource = d.OwnerUserId == d.UploadedByUserId ? "Client Upload" : "Sent to Client"
                }).ToList();

            // Stats
            var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);
            var weekCount = documents.Count(d => !d.IsDeleted && d.UploadedAt >= weekAgo);
            var stats = new AdminUploadStatsViewModel
            {
                CurrentTaxYear = CurrentYear,
                TotalUploadsThisWeek = weekCount,
                FilterPeriod = "week",
                StartDate = DateTime.Now.AddDays(-7)
            };

            var model = new AdminDashboardViewModel
            {
                AdminName = string.IsNullOrWhiteSpace(displayName) ? (user?.Email ?? "Admin") : displayName,
                UploadStats = stats,
                Clients = clientCards,
                AllUploads = recentUploads
            };
            return View(model);
        }

        private List<RecentUploadViewModel> GetRecentUploads()
        {
            return new List<RecentUploadViewModel>
            {
                new RecentUploadViewModel { FileName = "W2_2025.pdf", FileType = "W2", UploadDate = DateTime.Now.AddDays(-1), Status = "Processed" },
                new RecentUploadViewModel { FileName = "1099_INT_Chase.pdf", FileType = "1099 Int", UploadDate = DateTime.Now.AddDays(-2), Status = "Processing" },
                new RecentUploadViewModel { FileName = "Schedule_K1_Partnership.pdf", FileType = "Schedule K-1", UploadDate = DateTime.Now.AddDays(-3), Status = "Processed" },
                new RecentUploadViewModel { FileName = "1098_Mortgage_Interest.pdf", FileType = "1098", UploadDate = DateTime.Now.AddDays(-4), Status = "Processed" },
                new RecentUploadViewModel { FileName = "Business_Expenses_Q4.pdf", FileType = "Business Income/Expenses", UploadDate = DateTime.Now.AddDays(-5), Status = "Processing" },
                new RecentUploadViewModel { FileName = "Rental_Income_Statement.pdf", FileType = "Rental Property", UploadDate = DateTime.Now.AddDays(-6), Status = "Processed" }
            };
        }

        private UploadStatsViewModel GetUploadStats()
        {
            return new UploadStatsViewModel
            {
                TotalUploads = 28,
                ProcessedFiles = 22,
                PendingFiles = 6,
                CurrentTaxYear = CurrentYear
            };
        }

        [HttpGet]
        public async Task<IActionResult> DocumentCategories(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { success = false, message = "Missing userId" });
            var exists = await _userManager.FindByIdAsync(userId);
            if (exists == null) return NotFound(new { success = false, message = "Client not found" });

            var documents = await SafeLoadDocumentsAsync();
            var categories = documents
                .Where(d => d.OwnerUserId == userId && !d.IsDeleted)
                .GroupBy(d => d.Category ?? "Other")
                .Select(g => new
                {
                    category = g.Key,
                    count = g.Count(),
                    lastUpdated = g.Max(x => x.UploadedAt)
                }).ToList();

            var shaped = categories.Select(c => new { category = c.category, count = c.count, lastUpdated = Humanize(c.lastUpdated) });
            return Ok(new { success = true, categories = shaped });
        }

        [HttpGet]
        public async Task<IActionResult> CategoryDocuments(string userId, string category, [FromQuery] List<int> years)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { success = false, message = "Missing userId" });

            var documents = await SafeLoadDocumentsAsync();
            var q = documents.Where(d => d.OwnerUserId == userId && !d.IsDeleted).AsQueryable();
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(d => (d.Category ?? "").ToLower() == category.ToLower());
            if (years != null && years.Count > 0) q = q.Where(d => years.Contains(d.UploadedAt.Year));

            var docs = q.OrderByDescending(d => d.UploadedAt)
                .Select(d => new { id = d.Id, fileName = d.OriginalFileName, uploadDate = d.UploadedAt, fileSize = d.Size, status = "Uploaded" })
                .ToList();
            return Ok(new { success = true, documents = docs });
        }

        /// <summary>
        /// Returns documents sent by admin to a specific client.
        /// Per Section 3.6: Documents where OwnerUserId = userId AND UploadedByUserId != userId (admin-sent).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SentDocuments(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest(new { success = false, message = "Missing userId" });
            }

            var documents = await SafeLoadDocumentsAsync();
            var sentDocs = documents
                .Where(d => d.OwnerUserId == userId && d.UploadedByUserId != userId && !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    id = d.Id,
                    fileName = d.OriginalFileName,
                    uploadDate = d.UploadedAt,
                    fileSize = d.Size,
                    category = d.Category,
                    status = "Sent"
                })
                .ToList();

            return Ok(new { success = true, documents = sentDocs });
        }


        [HttpGet]
        public async Task<IActionResult> GetWorkflows(string? status = null)
        {
            var query = _db.DocumentWorkflows
                .Include(w => w.Document)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(w => w.Status.ToLower() == status.ToLower());

            var workflows = await query
                .OrderByDescending(w => w.RespondedAt ?? w.CreatedAt)
                .Take(20)
                .Select(w => new
                {
                    id = w.Id,
                    clientUserId = w.ClientUserId,
                    documentId = w.DocumentId,
                    clientResponseDocumentId = w.ClientResponseDocumentId,
                    documentName = w.Document != null ? w.Document.OriginalFileName : "Unknown",
                    category = w.Document != null ? w.Document.Category : "",
                    adminNotes = w.AdminNotes,
                    clientResponseText = w.ClientResponseText,
                    hasResponseDocument = w.ClientResponseDocumentId != null,
                    status = w.Status,
                    createdAt = w.CreatedAt,
                    respondedAt = w.RespondedAt
                })
                .ToListAsync();

            // Get client names
            var clientIds = workflows.Select(w => w.clientUserId).Distinct().ToList();
            var clients = await _userManager.Users
                .Where(u => clientIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}");

            var result = workflows.Select(w => new
            {
                w.id,
                clientName = clients.GetValueOrDefault(w.clientUserId, "Unknown"),
                w.documentId,
                w.clientResponseDocumentId,
                w.documentName,
                w.category,
                w.adminNotes,
                w.clientResponseText,
                w.hasResponseDocument,
                w.status,
                w.createdAt,
                w.respondedAt
            });

            return Json(new { success = true, workflows = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveWorkflow(Guid id)
        {
            var workflow = await _db.DocumentWorkflows.FindAsync(id);
            if (workflow == null) return NotFound();

            workflow.Status = "Resolved";
            workflow.ResolvedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class SendReminderRequest
        {
            [JsonPropertyName("clientIds")]
            public List<string> ClientIds { get; set; } = new();

            [JsonPropertyName("subject")]
            public string Subject { get; set; } = string.Empty;

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminder([FromBody] SendReminderRequest request)
        {
            if (request?.ClientIds == null || request.ClientIds.Count == 0)
            {
                return BadRequest(new { success = false, message = "No clients selected" });
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { success = false, message = "Email body is required" });
            }

            var successCount = 0;
            var failedEmails = new List<string>();

            foreach (var clientId in request.ClientIds)
            {
                var user = await _userManager.FindByIdAsync(clientId);
                if (user == null || string.IsNullOrWhiteSpace(user.Email))
                {
                    _logger.LogWarning("[Reminder] Client not found or no email: {ClientId}", clientId);
                    continue;
                }

                try
                {
                    // Replace [Client Name] placeholder with actual name
                    var clientName = $"{user.FirstName} {user.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(clientName)) clientName = "Client";

                    var personalizedBody = request.Body.Replace("[Client Name]", clientName);

                    // Convert plain text to HTML (preserve line breaks)
                    var htmlBody = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px;'>
                            {System.Text.RegularExpressions.Regex.Replace(
                                System.Net.WebUtility.HtmlEncode(personalizedBody),
                                @"\r?\n",
                                "<br/>")}
                        </div>";

                    await _emailSender.SendEmailAsync(user.Email, request.Subject, htmlBody);
                    successCount++;
                    _logger.LogInformation("[Reminder] Email sent to {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Reminder] Failed to send email to {Email}", user.Email);
                    failedEmails.Add(user.Email ?? clientId);
                }
            }

            if (successCount == 0)
            {
                return Ok(new { success = false, message = "Failed to send any reminders. Check SMTP configuration." });
            }

            var message = successCount == request.ClientIds.Count
                ? $"Reminders sent successfully to {successCount} client(s)!"
                : $"Sent {successCount} of {request.ClientIds.Count} reminders. Failed: {string.Join(", ", failedEmails)}";

            return Ok(new { success = true, message, sentCount = successCount, failedCount = failedEmails.Count });
        }

        [HttpPost]
        public IActionResult UpdateEmailTemplate(string template)
        {
            // Save email template logic here
            return Json(new { success = true, message = "Email template updated successfully" });
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new AdminProfileViewModel
            {
                Email = user.Email,
                Phone = user.PhoneNumber,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(AdminProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("EditProfile", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Handle email change with proper Identity normalization and uniqueness check
            if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _userManager.FindByEmailAsync(model.Email);
                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(string.Empty, "Email is already in use by another account.");
                    return View("EditProfile", model);
                }

                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View("EditProfile", model);
                }

                var setUserNameResult = await _userManager.SetUserNameAsync(user, model.Email);
                if (!setUserNameResult.Succeeded)
                {
                    foreach (var error in setUserNameResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View("EditProfile", model);
                }
            }

            // Update phone using Identity helper (optional but keeps consistency)
            if (!string.Equals(user.PhoneNumber, model.Phone, StringComparison.Ordinal))
            {
                var phoneResult = await _userManager.SetPhoneNumberAsync(user, model.Phone);
                if (!phoneResult.Succeeded)
                {
                    foreach (var error in phoneResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View("EditProfile", model);
                }
            }

            // Update custom fields (first/last name)
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View("EditProfile", model);
            }

            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult UpdateNotificationSettings(NotificationSettingsViewModel model)
        {
            return Json(new { success = true, message = "Notification settings updated!" });
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private static string Humanize(DateTimeOffset ts)
            => ts.ToLocalTime().ToString("MMM dd, yyyy");

        private async Task<List<Document>> SafeLoadDocumentsAsync()
        {
            try
            {
                return await _db.Documents.AsNoTracking().ToListAsync();
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Documents table not available. Returning empty set for admin views.");
                return new List<Document>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading documents.");
                return new List<Document>();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClient(string id)
        {
            if (string.IsNullOrEmpty(id))
                return Json(new { success = false, message = "Client ID is required." });

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Client not found." });

            // Verify user is a Client, not Admin
            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return Json(new { success = false, message = "Cannot delete admin accounts." });

            try
            {
                // Get all documents owned by this client
                var documents = await _db.Documents
                    .Where(d => d.OwnerUserId == id && !d.IsDeleted)
                    .ToListAsync();

                // Delete physical files and soft-delete documents
                foreach (var doc in documents)
                {
                    try
                    {
                        await _storage.DeleteAsync(doc.StoredFileName);
                    }
                    catch { /* best-effort file deletion */ }
                    
                    doc.IsDeleted = true;
                    doc.DeletedAt = DateTimeOffset.UtcNow;
                }

                // Delete all workflows involving this client
                var workflows = await _db.DocumentWorkflows
                    .Where(w => w.ClientUserId == id)
                    .ToListAsync();
                _db.DocumentWorkflows.RemoveRange(workflows);

                // Deactivate the user account
                user.IsActive = false;
                await _userManager.UpdateAsync(user);

                await _db.SaveChangesAsync();

                return Json(new { success = true, message = "Client account deactivated and all documents deleted." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting client {ClientId}", id);
                return Json(new { success = false, message = "An error occurred while deleting the client." });
            }
        }
    }
}



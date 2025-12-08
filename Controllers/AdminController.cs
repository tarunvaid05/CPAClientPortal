using JyotiIyerCPA.Models;
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

        public AdminController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext db, ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Clients(string query = null)
        {
            var users = await _userManager.Users
                .Where(u => u.EmailConfirmed)
                .OrderBy(u => u.Email)
                .Select(u => new { id = u.Id, name = (u.FirstName + " " + u.LastName).Trim(), email = u.Email })
                .ToListAsync();
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

            var users = await _userManager.Users.AsNoTracking()
                .OrderBy(u => u.Email)
                .ToListAsync();

            var clientCards = users.Select(u => new ClientViewModel
            {
                Id = u.Id,
                Name = string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? "Client") : ($"{u.FirstName} {u.LastName}").Trim(),
                Email = u.Email ?? string.Empty,
                Initials = string.Concat((u.FirstName ?? string.Empty).DefaultIfEmpty(' ').First(), (u.LastName ?? string.Empty).DefaultIfEmpty(' ').First()).Trim().ToUpper(),
                DocumentCount = docGroups.TryGetValue(u.Id, out var g) ? g.Count : 0,
                LastUpload = docGroups.TryGetValue(u.Id, out var g2) ? g2.LastUpload.LocalDateTime : DateTime.MinValue
            }).ToList();

            // Recent uploads pane
            var recentUploads = documents
                .Where(d => !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Take(12)
                .Join(users, d => d.OwnerUserId, u => u.Id, (d, u) => new AdminUploadViewModel
                {
                    Id = 0,
                    FileName = d.OriginalFileName,
                    FileType = string.IsNullOrEmpty(d.Category) ? "Document" : d.Category,
                    ClientName = string.IsNullOrWhiteSpace(($"{u.FirstName} {u.LastName}").Trim()) ? (u.Email ?? "Client") : ($"{u.FirstName} {u.LastName}").Trim(),
                    UploadDate = d.UploadedAt.LocalDateTime,
                    Status = "Uploaded"
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendReminder([FromBody] List<string> clientIds, string emailContent)
        {
            // Stub only for now
            return Ok(new { success = true, message = $"Reminders queued for {clientIds?.Count ?? 0} clients" });
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

        public IActionResult Logout()
        {
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
    }
}



using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JyotiIyerCPA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JyotiIyerCPA.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientPortalController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IFileStorage _storage;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ClientPortalController> _logger;

        public ClientPortalController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            IFileStorage storage,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<ClientPortalController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _storage = storage;
            _emailSender = emailSender;
            _configuration = configuration;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            var name = ($"{user.FirstName} {user.LastName}").Trim();

            // Get all user's documents for counting
            var allMyDocs = await _db.Documents.AsNoTracking()
                .Where(d => d.OwnerUserId == user.Id && !d.IsDeleted)
                .ToListAsync();

            // Get recent uploads (top 10)
            var myDocs = allMyDocs
                .OrderByDescending(d => d.UploadedAt)
                .Take(10)
                .ToList();

            var recent = new List<RecentUploadViewModel>();
            foreach (var d in myDocs)
            {
                recent.Add(new RecentUploadViewModel
                {
                    Id = d.Id,
                    FileName = d.OriginalFileName,
                    FileType = string.IsNullOrEmpty(d.Category) ? "Document" : d.Category,
                    UploadDate = d.UploadedAt.LocalDateTime,
                    Status = "Uploaded"
                });
            }

            // Count documents by category
            var categoryCounts = allMyDocs
                .GroupBy(d => string.IsNullOrEmpty(d.Category) ? "Miscellaneous" : d.Category)
                .ToDictionary(g => g.Key, g => g.Count());

            var stats = new UploadStatsViewModel
            {
                TotalUploads = allMyDocs.Count,
                ProcessedFiles = 0,
                PendingFiles = 0,
                CurrentTaxYear = DateTime.Now.Year
            };

            var model = new ClientDashboardViewModel
            {
                ClientName = string.IsNullOrWhiteSpace(name) ? (user.Email ?? "Client") : name,
                RecentUploads = recent,
                UploadStats = stats,
                CategoryCounts = categoryCounts
            };
            return View(model);
        }

        /// <summary>
        /// Returns documents sent to the client by admin (OwnerUserId = current user, UploadedByUserId != current user)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ReceivedDocuments()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Get documents where the client owns them but didn't upload them (admin sent)
            var receivedDocs = await _db.Documents.AsNoTracking()
                .Where(d => d.OwnerUserId == user.Id && d.UploadedByUserId != user.Id && !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    id = d.Id,
                    fileName = d.OriginalFileName,
                    category = string.IsNullOrEmpty(d.Category) ? "Document" : d.Category,
                    sentDate = d.UploadedAt
                })
                .ToListAsync();

            return Json(new { success = true, documents = receivedDocs });
        }

        /// <summary>
        /// Returns workflows for the current client with document info
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWorkflows(string? status = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _db.DocumentWorkflows
                .Include(w => w.Document)
                .Where(w => w.ClientUserId == user.Id && w.Document != null && !w.Document.IsDeleted);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(w => w.Status.ToLower() == status.ToLower());
            }

            var workflows = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    id = w.Id,
                    documentId = w.DocumentId,
                    documentName = w.Document!.OriginalFileName,
                    category = w.Document.Category,
                    adminNotes = w.AdminNotes,
                    status = w.Status,
                    createdAt = w.CreatedAt,
                    respondedAt = w.RespondedAt,
                    resolvedAt = w.ResolvedAt,
                    clientResponseText = w.ClientResponseText,
                    clientResponseDocumentId = w.ClientResponseDocumentId,
                    canRespond = w.Status == "Pending" && w.Document.Category != "Tax Return - Finalized"
                })
                .ToListAsync();

            return Json(new { success = true, workflows });
        }

        /// <summary>
        /// Allows client to submit a response to a workflow
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(UploadPolicy.MaxBytes)]
        public async Task<IActionResult> SubmitWorkflowResponse(Guid workflowId, string? responseText, IFormFile? responseFile, CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var workflow = await _db.DocumentWorkflows
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.ClientUserId == user.Id, ct);

            if (workflow == null)
                return NotFound(new { success = false, message = "Workflow not found." });

            if (workflow.Status != "Pending")
                return BadRequest(new { success = false, message = "This workflow has already been responded to." });

            // Validate at least one of text or file
            if (string.IsNullOrWhiteSpace(responseText) && (responseFile == null || responseFile.Length == 0))
                return BadRequest(new { success = false, message = "Please provide a response or attach a file." });

            // If file provided, save it as a document
            if (responseFile != null && responseFile.Length > 0)
            {
                var uploadError = UploadPolicy.Validate(responseFile);
                if (uploadError != null)
                {
                    return BadRequest(new { success = false, message = uploadError });
                }

                var (storedFileName, size, sha) = await _storage.SaveEncryptedAsync(user.Id, responseFile, ct);

                var responseDoc = new Document
                {
                    OwnerUserId = user.Id,
                    UploadedByUserId = user.Id,
                    OriginalFileName = responseFile.FileName,
                    StoredFileName = storedFileName,
                    ContentType = responseFile.ContentType ?? string.Empty,
                    Size = size,
                    Category = "Workflow Response",
                    UploadedAt = DateTimeOffset.UtcNow,
                    Sha256 = sha
                };
                _db.Documents.Add(responseDoc);
                await _db.SaveChangesAsync(ct);

                workflow.ClientResponseDocumentId = responseDoc.Id;
            }

            workflow.ClientResponseText = responseText;
            workflow.Status = "Responded";
            workflow.RespondedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Send notification to admin
            try
            {
                var adminEmail = _configuration["Email:AdminNotificationEmail"];
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var userName = $"{user.FirstName} {user.LastName}";
                    var document = await _db.Documents.FindAsync(workflow.DocumentId);
                    var documentName = document?.OriginalFileName ?? "Unknown";
                    await _emailSender.SendWorkflowResponseNotificationAsync(adminEmail, userName, documentName, responseText, responseFile != null);
                }
            }
            catch
            {
                // Email notification failed - don't block the response submission
            }

            return Json(new { success = true, message = "Response submitted successfully." });
        }

        [HttpPost]
        public IActionResult UploadFile(FileUploadViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Process file upload logic here
                TempData["Success"] = $"File '{model.FileName}' uploaded successfully!";
                return Json(new { success = true, message = "File uploaded successfully!" });
            }
            return Json(new { success = false, message = "Upload failed. Please try again." });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);

                // Update name fields
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;

                // Update email if provided and different
                if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
                {
                    user.Email = model.Email;
                    user.UserName = model.Email; // In ASP.NET Identity, UserName is often the same as Email
                }

                // Update phone if provided
                if (!string.IsNullOrEmpty(model.PhoneNumber))
                {
                    user.PhoneNumber = model.PhoneNumber;
                }

                // Save changes
                var result = await _userManager.UpdateAsync(user);
                
                if (result.Succeeded)
                {
                    // Handle password change if requested
                    if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
                    {
                        result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                        if (result.Succeeded)
                        {
                            // Refresh sign-in cookie with the new password
                            await _signInManager.RefreshSignInAsync(user);
                        }
                        else
                        {
                            foreach (var error in result.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                            return View("Dashboard");
                        }
                    }

                    TempData["Success"] = "Profile updated successfully!";
                    return RedirectToAction("Dashboard");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View("Dashboard");
        }

        [HttpPost]
        public IActionResult UpdateNotificationSettings(NotificationSettingsViewModel model)
        {
            // Update notification settings logic here
            return Json(new { success = true, message = "Notification settings updated!" });
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromForm] string subject, [FromForm] string message)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
                return Json(new { success = false, message = "Subject and message are required." });

            try
            {
                var adminEmail = _configuration["Email:AdminNotificationEmail"] ?? "admin@example.com";
                var clientName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrEmpty(clientName)) clientName = user.Email ?? "Unknown Client";
                
                await _emailSender.SendClientMessageAsync(adminEmail, clientName, user.Email!, subject, message);
                
                return Json(new { success = true, message = "Your message has been sent to your CPA." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send client message");
                return Json(new { success = false, message = "Failed to send message. Please try again." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RequestAppointment([FromForm] string preferredDate, [FromForm] string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (string.IsNullOrWhiteSpace(preferredDate))
                return Json(new { success = false, message = "Preferred date/time is required." });

            try
            {
                var adminEmail = _configuration["Email:AdminNotificationEmail"] ?? "admin@example.com";
                var clientName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrEmpty(clientName)) clientName = user.Email ?? "Unknown Client";
                
                await _emailSender.SendAppointmentRequestAsync(adminEmail, clientName, user.Email!, preferredDate, notes);
                
                return Json(new { success = true, message = "Your appointment request has been sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send appointment request");
                return Json(new { success = false, message = "Failed to send request. Please try again." });
            }
        }

        // Removed hardcoded helpers; Dashboard computes live data.
    }
}

using JyotiIyerCPA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JyotiIyerCPA.Data;
using Microsoft.EntityFrameworkCore;

namespace JyotiIyerCPA.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientPortalController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;

        public ClientPortalController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
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

            var myDocs = await _db.Documents.AsNoTracking()
                .Where(d => d.OwnerUserId == user.Id && !d.IsDeleted)
                .OrderByDescending(d => d.UploadedAt)
                .Take(10)
                .ToListAsync();

            var recent = new List<RecentUploadViewModel>();
            foreach (var d in myDocs)
            {
                recent.Add(new RecentUploadViewModel
                {
                    FileName = d.OriginalFileName,
                    FileType = string.IsNullOrEmpty(d.Category) ? "Document" : d.Category,
                    UploadDate = d.UploadedAt.LocalDateTime,
                    Status = "Uploaded"
                });
            }

            var stats = new UploadStatsViewModel
            {
                TotalUploads = await _db.Documents.CountAsync(d => d.OwnerUserId == user.Id && !d.IsDeleted),
                ProcessedFiles = 0,
                PendingFiles = 0,
                CurrentTaxYear = DateTime.Now.Year
            };

            var model = new ClientDashboardViewModel
            {
                ClientName = string.IsNullOrWhiteSpace(name) ? (user.Email ?? "Client") : name,
                RecentUploads = recent,
                UploadStats = stats
            };
            return View(model);
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

        // Removed hardcoded helpers; Dashboard computes live data.
    }
}

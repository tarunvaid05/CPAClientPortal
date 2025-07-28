using JyotiIyerCPA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClientPortal.Controllers
{
    [Authorize(Roles = "Client")]
    public class ClientPortalController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ClientPortalController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
            
            var model = new ClientDashboardViewModel
            {
                ClientName = $"{user.FirstName} {user.LastName}", // Get name from authenticated user
                RecentUploads = GetRecentUploads(),
                UploadStats = GetUploadStats()
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

        private List<RecentUploadViewModel> GetRecentUploads()
        {
            return new List<RecentUploadViewModel>
            {
                new RecentUploadViewModel { FileName = "W2_2023.pdf", FileType = "W2", UploadDate = DateTime.Now.AddDays(-1), Status = "Processed" },
                new RecentUploadViewModel { FileName = "1099_R_Retirement.pdf", FileType = "1099-R", UploadDate = DateTime.Now.AddDays(-2), Status = "Processing" },
                new RecentUploadViewModel { FileName = "SSA_1099_SocialSecurity.pdf", FileType = "SSA-1099", UploadDate = DateTime.Now.AddDays(-3), Status = "Processed" },
                new RecentUploadViewModel { FileName = "529_Contributions_2023.pdf", FileType = "529 Contributions", UploadDate = DateTime.Now.AddDays(-4), Status = "Processed" },
                new RecentUploadViewModel { FileName = "IRA_Contributions.pdf", FileType = "IRA Contributions", UploadDate = DateTime.Now.AddDays(-5), Status = "Processing" },
                new RecentUploadViewModel { FileName = "Estimated_Tax_Q4.pdf", FileType = "Estimated Taxes Paid", UploadDate = DateTime.Now.AddDays(-6), Status = "Processed" },
                new RecentUploadViewModel { FileName = "Foreign_Income_Statement.pdf", FileType = "Foreign Income", UploadDate = DateTime.Now.AddDays(-7), Status = "Processing" },
                new RecentUploadViewModel { FileName = "Rental_Property_Expenses.pdf", FileType = "Rental Property", UploadDate = DateTime.Now.AddDays(-8), Status = "Processed" },
                new RecentUploadViewModel { FileName = "Business_Income_2023.pdf", FileType = "Business Income/Expenses", UploadDate = DateTime.Now.AddDays(-9), Status = "Processed" },
                new RecentUploadViewModel { FileName = "Schedule_K1.pdf", FileType = "Schedule K-1", UploadDate = DateTime.Now.AddDays(-10), Status = "Processed" }
            };
        }

        private UploadStatsViewModel GetUploadStats()
        {
            return new UploadStatsViewModel
            {
                TotalUploads = 28,
                ProcessedFiles = 22,
                PendingFiles = 6,
                CurrentTaxYear = 2023
            };
        }
    }
}

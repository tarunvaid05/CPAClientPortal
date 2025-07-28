using ClientPortal.Models;
using JyotiIyerCPA.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClientPortal.Controllers
{
    public class AdminController : Controller
    {
        private const int CURRENT_YEAR = 2025;

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            var model = new AdminDashboardViewModel
            {
                AdminName = "Sarah Johnson", // This would come from authentication
                AllUploads = GetAllUploadsData(),
                UploadStats = GetAdminUploadStatsData(),
                Clients = GetClientsData()
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult GetClientDocuments(int clientId)
        {
            var client = GetClientsData().FirstOrDefault(c => c.Id == clientId);
            if (client == null)
            {
                return Json(new { success = false, message = "Client not found" });
            }

            var documents = GetClientDocumentCategoriesData(clientId);
            return Json(new { success = true, client = client, documents = documents });
        }

        [HttpPost]
        public IActionResult GetCategoryDocuments(int clientId, string category, List<int> years)
        {
            var documents = GetCategoryDocumentsData(clientId, category, years);
            return Json(new { success = true, documents = documents });
        }

        [HttpPost]
        public IActionResult SendReminder(List<int> clientIds, string emailContent)
        {
            // Process sending reminders
            var clients = GetClientsData().Where(c => clientIds.Contains(c.Id)).ToList();

            // Simulate sending emails
            foreach (var client in clients)
            {
                // Send email logic here
                System.Diagnostics.Debug.WriteLine($"Sending reminder to {client.Name}: {emailContent}");
            }

            return Json(new { success = true, message = $"Reminders sent to {clients.Count} clients" });
        }

        [HttpPost]
        public IActionResult UpdateEmailTemplate(string template)
        {
            // Save email template logic here
            return Json(new { success = true, message = "Email template updated successfully" });
        }

        [HttpPost]
        public IActionResult UpdateProfile(AdminProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("Dashboard");
            }
            return View("Dashboard");
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

        // Private helper methods with renamed method names to avoid conflicts
        private List<AdminUploadViewModel> GetAllUploadsData()
        {
            return new List<AdminUploadViewModel>
            {
                new AdminUploadViewModel { Id = 1, FileName = "W2_2025.pdf", FileType = "W2", ClientName = "John Doe", UploadDate = DateTime.Now.AddDays(-1), Status = "Processed" },
                new AdminUploadViewModel { Id = 2, FileName = "1099_INT_Chase.pdf", FileType = "1099 Int", ClientName = "Jane Smith", UploadDate = DateTime.Now.AddDays(-2), Status = "Processing" },
                new AdminUploadViewModel { Id = 3, FileName = "Schedule_K1_Partnership.pdf", FileType = "Schedule K-1", ClientName = "Mike Johnson", UploadDate = DateTime.Now.AddDays(-3), Status = "Processed" },
                new AdminUploadViewModel { Id = 4, FileName = "1098_Mortgage_Interest.pdf", FileType = "1098", ClientName = "Sarah Wilson", UploadDate = DateTime.Now.AddDays(-4), Status = "Processed" },
                new AdminUploadViewModel { Id = 5, FileName = "Business_Expenses_Q4.pdf", FileType = "Business Income/Expenses", ClientName = "David Brown", UploadDate = DateTime.Now.AddDays(-5), Status = "Processing" },
                new AdminUploadViewModel { Id = 6, FileName = "Rental_Income_Statement.pdf", FileType = "Rental Property", ClientName = "Lisa Davis", UploadDate = DateTime.Now.AddDays(-6), Status = "Processed" },
                new AdminUploadViewModel { Id = 7, FileName = "Foreign_Income_2025.pdf", FileType = "Foreign Income", ClientName = "Robert Miller", UploadDate = DateTime.Now.AddDays(-7), Status = "Processing" },
                new AdminUploadViewModel { Id = 8, FileName = "IRA_Contribution_Receipt.pdf", FileType = "IRA Contributions", ClientName = "Emily Garcia", UploadDate = DateTime.Now.AddDays(-8), Status = "Processed" }
            };
        }

        private AdminUploadStatsViewModel GetAdminUploadStatsData()
        {
            return new AdminUploadStatsViewModel
            {
                TotalUploadsThisWeek = 15,
                CurrentTaxYear = CURRENT_YEAR,
                FilterPeriod = "week",
                StartDate = DateTime.Now.AddDays(-7)
            };
        }

        private List<ClientViewModel> GetClientsData()
        {
            return new List<ClientViewModel>
            {
                new ClientViewModel { Id = 1, Name = "John Doe", Email = "john.doe@email.com", Initials = "JD", DocumentCount = 8, LastUpload = DateTime.Now.AddDays(-1) },
                new ClientViewModel { Id = 2, Name = "Jane Smith", Email = "jane.smith@email.com", Initials = "JS", DocumentCount = 12, LastUpload = DateTime.Now.AddDays(-2) },
                new ClientViewModel { Id = 3, Name = "Mike Johnson", Email = "mike.johnson@email.com", Initials = "MJ", DocumentCount = 6, LastUpload = DateTime.Now.AddDays(-3) },
                new ClientViewModel { Id = 4, Name = "Sarah Wilson", Email = "sarah.wilson@email.com", Initials = "SW", DocumentCount = 10, LastUpload = DateTime.Now.AddDays(-4) },
                new ClientViewModel { Id = 5, Name = "David Brown", Email = "david.brown@email.com", Initials = "DB", DocumentCount = 15, LastUpload = DateTime.Now.AddDays(-5) },
                new ClientViewModel { Id = 6, Name = "Lisa Davis", Email = "lisa.davis@email.com", Initials = "LD", DocumentCount = 9, LastUpload = DateTime.Now.AddDays(-6) },
                new ClientViewModel { Id = 7, Name = "Robert Miller", Email = "robert.miller@email.com", Initials = "RM", DocumentCount = 7, LastUpload = DateTime.Now.AddDays(-7) },
                new ClientViewModel { Id = 8, Name = "Emily Garcia", Email = "emily.garcia@email.com", Initials = "EG", DocumentCount = 11, LastUpload = DateTime.Now.AddDays(-8) }
            };
        }

        private List<ClientDocumentCategoryViewModel> GetClientDocumentCategoriesData(int clientId)
        {
            return new List<ClientDocumentCategoryViewModel>
            {
                new ClientDocumentCategoryViewModel { Category = "W2", DocumentCount = 2, LastUpdated = DateTime.Now.AddDays(-1) },
                new ClientDocumentCategoryViewModel { Category = "1099 Int", DocumentCount = 3, LastUpdated = DateTime.Now.AddDays(-2) },
                new ClientDocumentCategoryViewModel { Category = "1098", DocumentCount = 1, LastUpdated = DateTime.Now.AddDays(-3) },
                new ClientDocumentCategoryViewModel { Category = "Schedule K-1", DocumentCount = 1, LastUpdated = DateTime.Now.AddDays(-4) },
                new ClientDocumentCategoryViewModel { Category = "Business Income/Expenses", DocumentCount = 4, LastUpdated = DateTime.Now.AddDays(-5) }
            };
        }

        private List<DocumentDetailViewModel> GetCategoryDocumentsData(int clientId, string category, List<int> years)
        {
            return new List<DocumentDetailViewModel>
            {
                new DocumentDetailViewModel { Id = 1, FileName = $"{category}_Document_1.pdf", UploadDate = DateTime.Now.AddDays(-1), FileSize = "2.3 MB", Status = "Processed" },
                new DocumentDetailViewModel { Id = 2, FileName = $"{category}_Document_2.pdf", UploadDate = DateTime.Now.AddDays(-3), FileSize = "1.8 MB", Status = "Processed" },
                new DocumentDetailViewModel { Id = 3, FileName = $"{category}_Document_3.pdf", UploadDate = DateTime.Now.AddDays(-5), FileSize = "3.1 MB", Status = "Processing" }
            };
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace JyotiIyerCPA.Models
{
    public class ClientDashboardViewModel
    {
        public string ClientName { get; set; } = string.Empty;
        public List<RecentUploadViewModel> RecentUploads { get; set; } = new();
        public UploadStatsViewModel UploadStats { get; set; } = new();
    }

    public class FileUploadViewModel
    {
        [Required]
        public string FileType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        [Required]
        public IFormFile File { get; set; }

        public string CustomFileType { get; set; } = string.Empty;
    }

    public class UserProfileViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;

        public string CurrentPassword { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class NotificationSettingsViewModel
    {
        public bool EmailNotifications { get; set; }
        public bool SmsNotifications { get; set; }
        public bool FileProcessedNotifications { get; set; }
        public bool TaxDeadlineReminders { get; set; }
        public bool AppointmentReminders { get; set; }
    }

    public class RecentUploadViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class UploadStatsViewModel
    {
        public int TotalUploads { get; set; }
        public int ProcessedFiles { get; set; }
        public int PendingFiles { get; set; }
        public int CurrentTaxYear { get; set; }
    }
}
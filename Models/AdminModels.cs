using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Models
{
    public class AdminDashboardViewModel
    {
        public string AdminName { get; set; }
        public List<AdminUploadViewModel> AllUploads { get; set; }
        public AdminUploadStatsViewModel UploadStats { get; set; }
        public List<ClientViewModel> Clients { get; set; }
    }

    public class AdminUploadViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string ClientName { get; set; }
        public DateTime UploadDate { get; set; }
        public string Status { get; set; }
    }

    public class AdminUploadStatsViewModel
    {
        public int TotalUploadsThisWeek { get; set; }
        public int CurrentTaxYear { get; set; }
        public string FilterPeriod { get; set; }
        public DateTime StartDate { get; set; }
    }

    public class ClientViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Initials { get; set; }
        public int DocumentCount { get; set; }
        public DateTime LastUpload { get; set; }
    }

    public class ClientDocumentCategoryViewModel
    {
        public string Category { get; set; }
        public int DocumentCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class DocumentDetailViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime UploadDate { get; set; }
        public string FileSize { get; set; }
        public string Status { get; set; }
    }

    public class AdminProfileViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string Phone { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

    public class EmailTemplateViewModel
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}

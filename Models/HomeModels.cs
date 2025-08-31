using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JyotiIyerCPA.Models
{
    public class HomeViewModel
    {
        public List<TestimonialViewModel> Testimonials { get; set; } = new();
    }

    public class ServicesViewModel
    {
        public List<ServiceViewModel> Services { get; set; } = new();
    }

    public class ServiceViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class TestimonialViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        [Range(1, 5)]
        public int Rating { get; set; }
    }

    public class ContactViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;
    }
}


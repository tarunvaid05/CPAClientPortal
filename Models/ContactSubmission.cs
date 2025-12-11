using System.ComponentModel.DataAnnotations;

namespace JyotiIyerCPA.Models
{
    public class ContactSubmission
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Phone { get; set; }

        [Required]
        [MaxLength(100)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Message { get; set; } = string.Empty;

        public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(45)]
        public string? IpAddress { get; set; }
    }
}

using System;
using System.ComponentModel.DataAnnotations;

namespace JyotiIyerCPA.Models
{
    public class Document
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string OwnerUserId { get; set; } = string.Empty; // Client who owns this document

        [Required]
        public string UploadedByUserId { get; set; } = string.Empty; // Uploader (client only per requirements)

        [Required]
        [MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty; // Physical name on disk (encrypted)

        [MaxLength(255)]
        public string ContentType { get; set; } = string.Empty;

        public long Size { get; set; }

        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }

        // Optional integrity hash of plaintext bytes at upload time
        [MaxLength(128)]
        public string Sha256 { get; set; } = string.Empty;
    }
}


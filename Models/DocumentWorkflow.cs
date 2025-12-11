using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JyotiIyerCPA.Models
{
    public class DocumentWorkflow
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Users involved
        [Required]
        public string ClientUserId { get; set; } = string.Empty;

        [Required]
        public string AdminUserId { get; set; } = string.Empty;

        // The document being sent (FK to Document)
        [Required]
        public Guid DocumentId { get; set; }

        // Admin's notes when sending (optional)
        public string? AdminNotes { get; set; }

        // Client's response (optional text)
        public string? ClientResponseText { get; set; }

        // Client's response document (optional, FK to Document)
        public Guid? ClientResponseDocumentId { get; set; }

        // Status: "Pending", "Responded", "Resolved"
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        // Timestamps
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? RespondedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }

        // Navigation properties (optional, for EF convenience)
        [ForeignKey("DocumentId")]
        public virtual Document? Document { get; set; }

        [ForeignKey("ClientResponseDocumentId")]
        public virtual Document? ClientResponseDocument { get; set; }
    }
}

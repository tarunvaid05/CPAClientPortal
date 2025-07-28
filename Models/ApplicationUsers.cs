using Microsoft.AspNetCore.Identity;

namespace JyotiIyerCPA.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ClientType { get; set; } // "Personal", "Business", etc.
        public bool IsActive { get; set; } = true;
        public string ProfilePictureUrl { get; set; }
    }
}
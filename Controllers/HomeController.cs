using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JyotiIyerCPA.Data;
using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;

namespace JyotiIyerCPA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public HomeController(
            ILogger<HomeController> logger,
            IEmailSender emailSender,
            ApplicationDbContext db,
            IConfiguration configuration)
        {
            _logger = logger;
            _emailSender = emailSender;
            _db = db;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            var model = new HomeViewModel
            {
                Testimonials = GetTestimonials()
            };
            return View(model);
        }

        public IActionResult AboutUs()
        {
            return View();
        }

        public IActionResult Services()
        {
            var model = new ServicesViewModel
            {
                Services = GetServices()
            };
            return View(model);
        }

        public IActionResult ClientPortal()
        {
            return View();
        }

        public IActionResult ContactUs()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ContactUs(ContactViewModel model)
        {
            // Server-side validation for services (list Required attribute doesn't work with default initialization)
            if (model.Services == null || model.Services.Count == 0)
            {
                ModelState.AddModelError("Services", "Please select at least one service.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Rate limiting: Check if this email has submitted in the last 24 hours
            var oneDayAgo = DateTimeOffset.UtcNow.AddDays(-1);
            var recentSubmission = await _db.ContactSubmissions
                .Where(c => c.Email.ToLower() == model.Email.ToLower() && c.SubmittedAt > oneDayAgo)
                .FirstOrDefaultAsync();

            if (recentSubmission != null)
            {
                TempData["Error"] = "You have already submitted a message today. Please try again tomorrow.";
                return View(model);
            }

            try
            {
                // Save to database - Subject is now a computed property that joins services
                var submission = new ContactSubmission
                {
                    Name = model.Name,
                    Email = model.Email,
                    Phone = model.Phone,
                    Subject = model.Subject,
                    Message = model.Message,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };
                _db.ContactSubmissions.Add(submission);
                await _db.SaveChangesAsync();

                // Send email notification to admin
                var adminEmail = _configuration["Email:AdminNotificationEmail"] ?? "admin@example.com";
                await _emailSender.SendContactFormNotificationAsync(
                    adminEmail,
                    model.Name,
                    model.Email,
                    model.Phone,
                    model.Subject,
                    model.Message
                );

                _logger.LogInformation("Contact form submitted successfully from {Email}", model.Email);
                TempData["Success"] = "Thank you for your message. We'll get back to you soon!";
                return RedirectToAction("ContactUs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process contact form from {Email}", model.Email);
                TempData["Error"] = "An error occurred while sending your message. Please try again later.";
                return View(model);
            }
        }

        private List<TestimonialViewModel> GetTestimonials()
        {
            return new List<TestimonialViewModel>
            {
                new TestimonialViewModel
                {
                    Name = "Sarah Johnson",
                    Company = "Johnson Enterprises",
                    Message = "Jyoti's expertise in tax planning saved our company thousands. Her attention to detail is exceptional.",
                    Rating = 5
                },
                new TestimonialViewModel
                {
                    Name = "Michael Chen",
                    Company = "Tech Startup Inc.",
                    Message = "Professional, reliable, and always available when we need guidance. Highly recommended!",
                    Rating = 5
                },
                new TestimonialViewModel
                {
                    Name = "Lisa Rodriguez",
                    Company = "Rodriguez Consulting",
                    Message = "Outstanding service and clear communication. Jyoti makes complex tax matters easy to understand.",
                    Rating = 5
                }
            };
        }

        private List<ServiceViewModel> GetServices()
        {
            return new List<ServiceViewModel>
            {
                new ServiceViewModel
                {
                    Title = "Tax Compliance & Planning",
                    Icon = "fas fa-calculator",
                    Description = "Comprehensive tax preparation for individuals and businesses."
                },
                new ServiceViewModel
                {
                    Title = "Bookkeeping Services",
                    Icon = "fas fa-book",
                    Description = "Professional Bookkeeping services to keep your financial records accurate and up to date."
                },
                new ServiceViewModel
                {
                    Title = "Business Formation",
                    Icon = "fas fa-building",
                    Description = "Guidance on business structure selection, incorporation services and ongoing compliance requirements for new businesses."
                },
                new ServiceViewModel
                {
                    Title = "Payroll Services",
                    Icon = "fas fa-users",
                    Description = "Complete payroll processing services including tax withholdings, direct deposits, and quarterly payroll tax filings."
                }
            };
        }
    }
}
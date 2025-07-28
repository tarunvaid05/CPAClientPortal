using Microsoft.AspNetCore.Mvc;
using JyotiIyerCPA.Models;
using System.Diagnostics;

namespace JyotiIyerCPA.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
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
        public IActionResult ContactUs(ContactViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Process contact form
                TempData["Success"] = "Thank you for your message. We'll get back to you soon!";
                return RedirectToAction("ContactUs");
            }
            return View(model);
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
                    Title = "Tax Preparation & Planning",
                    Icon = "fas fa-calculator",
                    Description = "Comprehensive tax preparation for individuals and businesses, including strategic tax planning to minimize your tax liability and maximize deductions."
                },
                new ServiceViewModel
                {
                    Title = "Bookkeeping Services",
                    Icon = "fas fa-book",
                    Description = "Professional bookkeeping services to keep your financial records accurate and up-to-date, including QuickBooks setup and training."
                },
                new ServiceViewModel
                {
                    Title = "Financial Consulting",
                    Icon = "fas fa-chart-line",
                    Description = "Expert financial advice and consulting services to help you make informed business decisions and achieve your financial goals."
                },
                new ServiceViewModel
                {
                    Title = "Audit & Assurance",
                    Icon = "fas fa-shield-alt",
                    Description = "Independent audit and assurance services to provide confidence in your financial statements and compliance with regulations."
                },
                new ServiceViewModel
                {
                    Title = "Business Formation",
                    Icon = "fas fa-building",
                    Description = "Guidance on business structure selection, incorporation services, and ongoing compliance requirements for new businesses."
                },
                new ServiceViewModel
                {
                    Title = "Payroll Services",
                    Icon = "fas fa-users",
                    Description = "Complete payroll processing services including tax withholdings, direct deposits, and quarterly reporting."
                }
            };
        }
    }
}
using System;
using System.IO;
using System.Threading.Tasks;
using JyotiIyerCPA.Controllers;
using JyotiIyerCPA.Data;
using JyotiIyerCPA.Models;
using JyotiIyerCPA.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Tests.Storage;
using Xunit;

namespace Tests
{
    public class DocumentsControllerTests
    {
        private (ApplicationDbContext, UserManager<ApplicationUser>, SignInManager<ApplicationUser>) SetupIdentity()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options);

            var store = new Mock<IUserStore<ApplicationUser>>();
            var userMgr = new UserManager<ApplicationUser>(store.Object, null, new PasswordHasher<ApplicationUser>(), null, null, null, null, null, null);
            var signInMgr = new SignInManager<ApplicationUser>(userMgr,
                new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
                null, null, null, null);

            return (db, userMgr, signInMgr);
        }

        [Fact]
        public async Task Upload_Forbids_Admin()
        {
            var (db, userMgr, _) = SetupIdentity();
            var storage = new FakeFileStorage();
            var emailSender = new Mock<IEmailSender>();
            var configuration = new Mock<IConfiguration>();
            var controller = new DocumentsController(db, storage, userMgr, new NullLogger<DocumentsController>(), emailSender.Object, configuration.Object);
            var admin = new ApplicationUser { Id = "admin", Email = "a@a" };

            // Fake identity context
            var http = new DefaultHttpContext();
            http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, admin.Id),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            }, "TestAuth"));
            controller.ControllerContext = new ControllerContext { HttpContext = http };

            var file = new FormFile(new MemoryStream(new byte[] {1,2,3}), 0, 3, "file", "test.pdf") { Headers = new HeaderDictionary() };
            var result = await controller.Upload(file);
            Assert.True(result is ForbidResult || result is UnauthorizedResult, "Expected Forbid or Unauthorized for admin upload");
        }
    }
}

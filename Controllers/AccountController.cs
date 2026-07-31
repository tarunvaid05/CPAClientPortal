using JyotiIyerCPA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using JyotiIyerCPA.Services;

namespace JyotiIyerCPA.Controllers
{
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Dashboard", "Admin");
                else
                    return RedirectToAction("Dashboard", "ClientPortal");
            }

            // Always show the Client Portal page for login UI
            return RedirectToAction("ClientPortal", "Home", new { returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                // First, find the user to check IsActive status before sign-in
                var user = await _userManager.FindByEmailAsync(model.Email);

                // Check if user is deactivated
                if (user != null && !user.IsActive)
                {
                    TempData["LoginError"] = "This account has been deactivated. Please contact your administrator.";
                    return RedirectToAction("ClientPortal", "Home");
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.IsLockedOut)
                {
                    TempData["LoginError"] = "Too many failed sign-in attempts. Please try again in 10 minutes.";
                    return RedirectToAction("ClientPortal", "Home");
                }

                if (result.Succeeded)
                {
                    // Update security stamp to invalidate other sessions
                    await _userManager.UpdateSecurityStampAsync(user);
                    await _signInManager.RefreshSignInAsync(user);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                        return RedirectToAction("Dashboard", "Admin");
                    else
                        return RedirectToAction("Dashboard", "ClientPortal");
                }
                // No server-rendered login view; redirect back to ClientPortal with error
                TempData["LoginError"] = "Invalid email or password.";
                return RedirectToAction("ClientPortal", "Home");
            }
            // Invalid model: redirect back to unified login page
            TempData["LoginError"] = "Please provide a valid email and password.";
            return RedirectToAction("ClientPortal", "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> AjaxLogin([FromBody] AjaxLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input." });
            }

            _logger.LogInformation("AjaxLogin attempt for: {Email}", request.Email);
            var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, request.RememberMe, lockoutOnFailure: true);
            _logger.LogInformation("SignIn result: Succeeded={Succeeded}, IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, RequiresTwoFactor={RequiresTwoFactor}",
                result.Succeeded, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);

            if (result.IsLockedOut)
            {
                return Ok(new { success = false, message = "Too many failed sign-in attempts. Please try again in 10 minutes." });
            }

            if (!result.Succeeded)
            {
                return Ok(new { success = false, message = "Invalid email or password." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email);

            // Deactivated accounts must not hold a session. Checked only after the password
            // verifies, so this never reveals whether an address has an account.
            if (user == null || !user.IsActive)
            {
                await _signInManager.SignOutAsync();
                _logger.LogWarning("[Login] Blocked sign-in for deactivated or missing account: {Email}", request.Email);
                return Ok(new { success = false, message = "This account has been deactivated. Please contact your administrator." });
            }

            // Update security stamp to invalidate other sessions
            await _userManager.UpdateSecurityStampAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var redirectUrl = Url.Action("Dashboard", isAdmin ? "Admin" : "ClientPortal");
            return Ok(new { success = true, redirect = redirectUrl });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult InviteUser()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteUser(InviteUserViewModel model)
        {
            _logger.LogInformation("[Invite] Starting invite for {Email}", model?.Email);
            if (!ModelState.IsValid)
                return View(model);

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("[Invite] User already exists: {Email}", model.Email);
                ModelState.AddModelError("", "User with this email already exists.");
                return View(model);
            }

            // Create new user
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = false,
                ClientType = "Client",
                IsActive = true,
                ProfilePictureUrl = string.Empty
            };

            var result = await _userManager.CreateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("[Invite] Create user error: {Code} {Desc}", error.Code, error.Description);
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }

            // Add to Client role
            await _userManager.AddToRoleAsync(user, "Client");

            // Generate password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("SetPassword", "Account",
                new { userId = user.Id, code = token },
                protocol: Request.Scheme);
            _logger.LogInformation("[Invite] Generated token (len={Len}) and callback for user {UserId}.", token?.Length ?? 0, user.Id);

            // Send invite email
            try
            {
                await _emailSender.SendInviteEmail(model.Email, callbackUrl);
                _logger.LogInformation("[Invite] Invite email sent to {Email}", model.Email);
                TempData["Success"] = $"Invitation email sent to {model.Email}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Invite] Failed to send invite email to {Email}: {Message}", model.Email, ex.Message);
                TempData["Error"] = $"User created, but failed to send invite email: {ex.Message}";
            }

            TempData["Success"] = $"Invitation email sent to {model.Email}";
            return RedirectToAction(nameof(InviteUser));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteUserAjax(InviteUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid input.", errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray() });
            }

            _logger.LogInformation("[Invite/Ajax] Starting invite for {Email}", model.Email);
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                // If user completed setup (EmailConfirmed = true), block re-invite
                if (existingUser.EmailConfirmed)
                {
                    _logger.LogWarning("[Invite/Ajax] User already has active account: {Email}", model.Email);
                    return Ok(new { success = false, message = "A user with this email already exists and has an active account." });
                }
                
                // User is pending (never completed setup) - allow re-invite
                _logger.LogInformation("[Invite/Ajax] Re-inviting pending user: {Email}", model.Email);
                var resendToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resendCallbackUrl = Url.Action("SetPassword", "Account", 
                    new { userId = existingUser.Id, code = resendToken }, protocol: Request.Scheme);
                _logger.LogInformation("[Invite/Ajax] Generated re-invite token (len={Len}) for user {UserId}.", resendToken?.Length ?? 0, existingUser.Id);
                
                bool resendEmailSent = true;
                string resendMsg = "A new invitation has been sent. The previous invitation link is no longer valid.";
                try
                {
                    await _emailSender.SendInviteEmail(existingUser.Email, resendCallbackUrl);
                    _logger.LogInformation("[Invite/Ajax] Re-invite email sent to {Email}", existingUser.Email);
                }
                catch (Exception ex)
                {
                    resendEmailSent = false;
                    resendMsg = $"Failed to resend invitation email: {ex.Message}";
                    _logger.LogError(ex, "[Invite/Ajax] Failed to resend invite email to {Email}: {Message}", existingUser.Email, ex.Message);
                }
                
                return Ok(new { success = true, isResend = true, message = resendMsg, emailSent = resendEmailSent });
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailConfirmed = false,
                ClientType = "Client",
                IsActive = true,
                ProfilePictureUrl = string.Empty
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToArray();
                foreach (var e in result.Errors)
                {
                    _logger.LogWarning("[Invite/Ajax] Create user error: {Code} {Desc}", e.Code, e.Description);
                }
                return Ok(new { success = false, message = "Failed to create user.", errors });
            }

            await _userManager.AddToRoleAsync(user, "Client");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("SetPassword", "Account", new { userId = user.Id, code = token }, protocol: Request.Scheme);
            _logger.LogInformation("[Invite/Ajax] Generated token (len={Len}) and callback for user {UserId}.", token?.Length ?? 0, user.Id);

            bool emailSent = true;
            string msg = $"Invitation email sent to {model.Email}";
            try
            {
                await _emailSender.SendInviteEmail(model.Email, callbackUrl);
                _logger.LogInformation("[Invite/Ajax] Invite email sent to {Email}", model.Email);
            }
            catch (Exception ex)
            {
                emailSent = false;
                msg = $"User created, but failed to send invite email: {ex.Message}";
                _logger.LogError(ex, "[Invite/Ajax] Failed to send invite email to {Email}: {Message}", model.Email, ex.Message);
            }

            return Ok(new { success = true, isResend = false, message = msg, emailSent });
        }

        [HttpGet]
        public IActionResult SetPassword(string userId, string code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
                return RedirectToAction(nameof(Login));

            var model = new SetPasswordViewModel { UserId = userId, Token = code };
            // Pre-fill known profile data so users can confirm/update it
            // Note: this is a GET; any failure to load user should still allow the form to render
            try
            {
                var user = _userManager.FindByIdAsync(userId).GetAwaiter().GetResult();
                if (user != null)
                {
                    model.FirstName = user.FirstName;
                    model.LastName = user.LastName;
                    model.PhoneNumber = user.PhoneNumber;
                }
            }
            catch { /* non-fatal prefill */ }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            // Additional password complexity enforcement for invitations
            // Require: min 8 chars, one uppercase, one special character
            var pwd = model.Password ?? string.Empty;
            bool strong = pwd.Length >= 8 && pwd.Any(char.IsUpper) && pwd.Any(ch => !char.IsLetterOrDigit(ch));
            if (!strong)
            {
                ModelState.AddModelError("Password", "Password must be at least 8 characters, include one uppercase letter and one special character.");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("[SetPassword] Reset failed for user {UserId}: {Code} {Desc}", user.Id, error.Code, error.Description);
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // Capture additional profile details (always persist what user provides)
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                user.PhoneNumber = model.PhoneNumber;
            }

            // Mark email as confirmed on successful first-time password set
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
            }

            await _userManager.UpdateAsync(user);

            // Auto-sign in after setting password
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("[SetPassword] User {UserId} activated and signed in", user.Id);

            return RedirectToAction("Dashboard", "ClientPortal");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("ClientPortal", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Your password has been changed successfully.";

            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "Admin");
            else
                return RedirectToAction("Dashboard", "ClientPortal");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist or is not confirmed
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // Generate password reset token
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account",
                new { userId = user.Id, code = code },
                protocol: Request.Scheme);

            // Send password reset email using the specialized method
            await _emailSender.SendPasswordResetEmailAsync(model.Email, callbackUrl);

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string userId, string code)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            {
                return RedirectToAction("Index", "Home");
            }

            var model = new ResetPasswordViewModel { UserId = userId, Code = code };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}


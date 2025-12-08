# Project Context

This repository hosts the Jyoti Iyer CPA website and client/admin portal built on ASP.NET Core (.NET 8) using MVC with Razor Views and ASP.NET Core Identity for authentication/authorization.

Note: Although some metadata mentions “Razor Pages,” the current codebase uses MVC controllers and Razor Views (not Razor Pages).

## Project Context
The CPA Client Portal is a secure, invite-only web application built using ASP.NET Core MVC for a small CPA firm (Jyoti Iyer CPA). It is designed to streamline document management and client interactions in a professional accounting setting. The portal enables:

Admin-based client invitations using one-time registration links.

Client authentication and role-based access via ASP.NET Core Identity.

Secure upload/download of sensitive financial documents.

SMTP-based email notifications (e.g., account invitations).

A structured admin dashboard for overseeing users and uploaded files.

An expandable backend powered by Entity Framework Core and SQL Server.

Security, usability, and role separation (Admin vs Client) are central to the application’s design.

## Tech Stack

- .NET 8, C# 12
- ASP.NET Core MVC + Razor Views
- ASP.NET Core Identity (roles: Admin, Client)
- Entity Framework Core (SQL Server)
- Bootstrap, Font Awesome
- Custom email sender via DI (IEmailSender)

## Solution Layout

- Startup: Program.cs configures services, Identity, EF Core, and routing.
- MVC:
  - Controllers/
    - AccountController.cs: Auth, user invite/onboarding, password flows.
    - HomeController.cs, ClientPortalController.cs, AdminController.cs may exist (not shown here).
  - Views/
    - Home/ClientPortal.cshtml: Public “Client Portal” landing + Ajax login form.
    - Account/*: Login, Forgot/Reset password, SetPassword, ChangePassword, AccessDenied.
    - Shared/_Layout.cshtml
- Models/
  - HomeModels.cs: View models for home/services/testimonials/contact.
  - Identity models: ApplicationUser (not shown here but referenced).
  - ViewModels: LoginViewModel, AjaxLoginRequest, InviteUserViewModel, Reset/Set/ChangePassword view models (referenced by controller).
- Data/
  - ApplicationDbContext.cs (EF Core DbContext; not shown here but part of solution).
- Services/
  - IEmailSender + EmailService.cs (SendGrid/SMTP via DI; invite/password reset emails).
- wwwroot/
  - js/admin-portal.js: Admin UI interactions (search, filters, modals, reminders, invite).
  - css/, images/, lib/

## Authentication & Authorization

- Identity configured with SQL-backed user store.
- Roles: Admin, Client.
- Login paths:
  - Standard POST /Account/Login (form post + antiforgery).
  - Ajax POST /Account/AjaxLogin (JSON, no antiforgery on this action).
- On successful login:
  - Admin -> /Admin/Dashboard
  - Client -> /ClientPortal/Dashboard
- Other flows:
  - Logout: GET /Account/Logout
  - Forgot password: GET/POST /Account/ForgotPassword -> email reset link -> GET/POST /Account/ResetPassword
  - Invite user (Admin only): GET/POST /Account/InviteUser
    - Creates user, assigns “Client” role, marks EmailConfirmed = true.
    - Generates password reset token and emails Set Password link:
      - /Account/SetPassword?userId={id}&code={token}
    - After setting password, user is signed in and redirected to Client Dashboard.

## Key Endpoints (Attribute Routing)

- [Route("[controller]/[action]")] on AccountController => URLs like /Account/Login, /Account/AjaxLogin, etc.

AccountController highlights:
- GET Login
- POST Login(LoginViewModel) [ValidateAntiForgeryToken]
- POST AjaxLogin([FromBody] AjaxLoginRequest) [AllowAnonymous]
- GET/POST InviteUser (Admin-only) [ValidateAntiForgeryToken]
- GET/POST SetPassword
- GET Logout [Authorize]
- GET/POST ChangePassword [Authorize, ValidateAntiForgeryToken]
- GET/POST ForgotPassword [ValidateAntiForgeryToken]
- GET/POST ResetPassword [ValidateAntiForgeryToken]
- GET AccessDenied, ForgotPasswordConfirmation, ResetPasswordConfirmation

Credential validation (database-backed via Identity):
- Both POST Login and POST AjaxLogin call:
  - SignInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false)
  - User and role lookup via UserManager

## Frontend Flows

Client portal login (Views/Home/ClientPortal.cshtml):
- UI renders login form.
- JavaScript intercepts submit and calls:
  - fetch('@Url.Action("AjaxLogin", "Account")', { method: 'POST', body: JSON.stringify({ email, password, rememberMe }) })
- Expects JSON { success: bool, redirect: url }

Admin portal UI (wwwroot/js/admin-portal.js):
- Client search, upload filters, date presets.
- Modal-based document browsing (simulated data).
- Reminder flow (simulated sending).
- Profile/password/notification modal handlers (simulated).
- Invite client form handler handleInviteSubmit posts via fetch expecting JSON.

Important note: The current AccountController InviteUser action returns Views and redirects (not JSON). If the invite UI is submitted via fetch expecting JSON, either:
- Add an API-style action (e.g., POST /Account/InviteUserAjax) that returns JSON, or
- Submit the form normally without fetch and handle server-rendered validation.

## Email

- IEmailSender injected into AccountController.
- Used for:
  - Invite emails (SendInviteEmail)
  - Password reset emails (SendPasswordResetEmailAsync)
- Configure credentials via User Secrets for dev; environment variables for prod.

## Configuration & Secrets

- appsettings.json, appsettings.Development.json
- Do not commit secrets. Use:
  - dotnet user-secrets init
  - dotnet user-secrets set "Email:SmtpServer" "smtp.example.com"
  - dotnet user-secrets set "Email:SmtpPort" "587"
  - dotnet user-secrets set "Email:SmtpUsername" "you@example.com"
  - dotnet user-secrets set "Email:SmtpPassword" "<app-password>"
  - dotnet user-secrets set "Email:FromEmail" "noreply@example.com"
- Connection string via ConnectionStrings:DefaultConnection (env var: ConnectionStrings__DefaultConnection)

## Build & Run

- dotnet restore
- dotnet build -c Debug
- dotnet run
- dotnet watch run (hot reload)

## EF Core

- Add migration: dotnet ef migrations add <Name>
- Update DB: dotnet ef database update
- Identity tables reside in the same DefaultConnection database.

## Security Notes

- Standard login action is protected by [ValidateAntiForgeryToken].
- AjaxLogin does not validate antiforgery tokens. If exposed publicly, consider adding antiforgery on JSON posts (send token in header) or use SameSite cookies and additional safeguards (rate-limit, lockoutOnFailure true).
- Ensure HTTPS enforced; set cookie security (HttpOnly, Secure, SameSite) in Startup.

## Known Gaps / TODOs

- Align Invite Client flow:
  - Either provide a JSON-returning action used by admin-portal.js or submit the form normally.
- admin-portal.js uses simulated data for documents and reminders. Back-end endpoints are needed for:
  - Listing categories/documents for a client
  - Sending reminders
  - Saving profile/notification settings
- Confirm and document all ViewModels (LoginViewModel, AjaxLoginRequest, InviteUserViewModel, Change/Reset/Set password models).
- Add unit/integration tests (controllers, services).
- Consider enabling account lockout (lockoutOnFailure: true) to mitigate brute-force attempts.

## Files Touched in This Context

- Controllers/AccountController.cs: Complete auth/user onboarding flows.
- Views/Home/ClientPortal.cshtml: Client login page with Ajax handler.
- Models/HomeModels.cs: Home/services/testimonials/contact view models.
- wwwroot/js/admin-portal.js: Admin portal interactions (simulated).
- Services/IEmailSender + EmailService.cs: Email delivery (callbacks used in AccountController).
- Data/ApplicationDbContext.cs: EF Core DbContext (not shown here).

# Repository Guidelines

## Project Structure & Module Organization
- **Startup:** `Program.cs` configures the ASP.NET Core app.
- **MVC:** `Controllers/`, `Views/`, `Models/` follow standard MVC.
- **Data:** `Data/ApplicationDbContext.cs` for EF Core context.
- **Services:** `Services/EmailService.cs` and future DI services.
- **Static Assets:** `wwwroot/` for `css/`, `js/`, `images/`, and vendor `lib/`.
- **Config:** `appsettings.json` and `appsettings.Development.json` for settings.

## Build, Run, and Development Commands
- `dotnet restore`: restore NuGet packages.
- `dotnet build -c Debug` or `-c Release`: compile the app.
- `dotnet run`: run locally. Example (PowerShell): ``$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run``.
- `dotnet watch run`: hot-reload during development.

## Coding Style & Naming Conventions
- **C#:** 4-space indent, file-scoped namespaces, `var` when type is obvious.
- **Naming:** PascalCase for types/methods; camelCase for locals/params; private fields `_camelCase`.
- **MVC:** Controllers end with `Controller` (e.g., `HomeController`); views in `Views/<Controller>/<Action>.cshtml`.
- **Services:** Prefer DI via constructor injection.
- **Formatting:** Run `dotnet format` before committing (if installed).
- **Static assets:** Place custom code under `wwwroot/js` and `wwwroot/css`; avoid editing `wwwroot/lib`.

## Testing Guidelines
- No test project is present yet. When adding tests, use xUnit in `tests/Jyoti_Iyer_CPA_Website.Tests`.
- **Naming:** `ClassNameTests.cs`; methods like `MethodName_ShouldExpectedBehavior`.
- **Scope:** Cover controllers (action results/filters), services (business rules), and data mapping.
- **Run:** `dotnet test` from the repo root.

## Commit & Pull Request Guidelines
- **Commits:** Imperative, present tense ("Add EmailService retry"); group logically. Conventional Commits (e.g., `feat:`, `fix:`) welcome.
- **PRs:** Include a clear description, linked issue, reproduction/validation steps, and screenshots for UI changes.
- **Quality gates:** CI must build; run `dotnet build` and `dotnet format` locally; ensure no secrets added.

## Security & Configuration Tips
- Do not commit secrets. Use User Secrets for local dev: `dotnet user-secrets init` then `dotnet user-secrets set "Smtp:ApiKey" "<value>"`.
- Prefer environment variables for production (`ASPNETCORE_ENVIRONMENT`, connection strings). Keep `appsettings.Development.json` developer-friendly but sanitized.


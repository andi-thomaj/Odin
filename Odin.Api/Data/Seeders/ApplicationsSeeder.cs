using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Odin.Api.Authentication;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the <c>applications</c> registry that powers multi-app data isolation. Idempotent — inserts only the
/// rows that don't already exist — so it is safe on every startup and after an integration-test Respawn (which
/// wipes the table; without re-seeding, app resolution would 400 mid-suite). Branding (frontend URL, from-email)
/// is read from configuration so deployed environments get real values; missing keys fall back to dev origins.
/// Add a new application by appending a row here (or, later, via an admin editor) + shipping its frontend header.
/// </summary>
public class ApplicationsSeeder(ApplicationDbContext context, IConfiguration configuration)
{
    public async Task SeedAsync()
    {
        var existing = await context.Applications.Select(a => a.Key).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var fromEmail = configuration["Resend:FromEmail"] ?? "info@ancestrify.io";
        var seeds = new[]
        {
            new Application
            {
                Key = AppKeys.Ancestrify,
                DisplayName = "Ancestrify",
                FrontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000",
                FromEmail = fromEmail,
                FromName = configuration["Resend:FromName"] ?? "Ancestrify",
                IsActive = true,
            },
            new Application
            {
                Key = "aurora",
                DisplayName = "Aurora",
                FrontendBaseUrl = configuration["Applications:Aurora:FrontendBaseUrl"] ?? "http://localhost:3001",
                FromEmail = configuration["Applications:Aurora:FromEmail"] ?? fromEmail,
                FromName = configuration["Applications:Aurora:FromName"] ?? "Aurora",
                IsActive = true,
            },
        };

        var toAdd = seeds.Where(s => !existingSet.Contains(s.Key)).ToList();
        if (toAdd.Count == 0)
            return;

        context.Applications.AddRange(toAdd);
        await context.SaveChangesAsync();
    }
}

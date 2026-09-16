using Microsoft.AspNetCore.Identity;

namespace ShiftingGuru.Data;

/// <summary>
/// Creates the Admin role and, if credentials are supplied, the first admin
/// user. Credentials come from configuration (environment variables or user
/// secrets) and are never written to source, appsettings or the logs.
///
///   SHIFTINGGURU_ADMIN_EMAIL
///   SHIFTINGGURU_ADMIN_PASSWORD
///
/// If either is missing the role is still created and seeding is skipped -
/// the app starts normally, you just can't log in until you supply them.
/// </summary>
public static class AdminSeeder
{
    public const string AdminRole = "Admin";
    public const string VendorRole = "Vendor";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = provider.GetRequiredService<UserManager<IdentityUser>>();
        var config = provider.GetRequiredService<IConfiguration>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder");

        foreach (var role in new[] { AdminRole, VendorRole })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created the {Role} role.", role);
            }
        }

        var email = config["SHIFTINGGURU_ADMIN_EMAIL"];
        var password = config["SHIFTINGGURU_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Admin seed credentials not supplied; skipping admin user creation.");
            return;
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Already there. Make sure the role is attached, then leave it alone -
            // this must never reset a password on every restart.
            if (!await users.IsInRoleAsync(existing, AdminRole))
            {
                await users.AddToRoleAsync(existing, AdminRole);
            }
            return;
        }

        var admin = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await users.CreateAsync(admin, password);

        if (!result.Succeeded)
        {
            // Error descriptions cover things like password complexity. They
            // never contain the password itself.
            logger.LogError("Could not create the admin user: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(admin, AdminRole);
        logger.LogInformation("Admin user created and added to the {Role} role.", AdminRole);
    }
}
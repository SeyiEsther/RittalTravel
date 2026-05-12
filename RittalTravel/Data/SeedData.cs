using Microsoft.AspNetCore.Identity;
using RittalTravel.Models;

namespace RittalTravel.Data;

public static class SeedData
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<RittalTravelContext>>();

        foreach (var role in new[] { "Admin", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }

        await SeedUser(userManager, logger,
            email: "admin@rittal.co.uk",
            password: "RittalTravel2025!",
            role: "Admin");

        await SeedUser(userManager, logger,
            email: "viewer@rittal.co.uk",
            password: "ViewOnly2025!",
            role: "Viewer");
    }

    private static async Task SeedUser(UserManager<IdentityUser> um, ILogger logger,
        string email, string password, string role)
    {
        if (await um.FindByEmailAsync(email) != null) return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await um.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await um.AddToRoleAsync(user, role);
            logger.LogInformation("Seeded user {Email} in role {Role}", email, role);
        }
        else
        {
            foreach (var e in result.Errors)
                logger.LogError("Failed to create {Email}: {Code} - {Desc}", email, e.Code, e.Description);
        }
    }
}

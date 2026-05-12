using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RittalTravel.Models;
using RittalTravel.Services;

namespace RittalTravel.Data;

public static class SeedData
{
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<RittalTravelContext>();
        var logger = services.GetRequiredService<ILogger<RittalTravelContext>>();

        foreach (var role in new[] { "Admin", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }

        await SeedUser(userManager, logger, "admin@rittal.co.uk", "RittalTravel2025!", "Admin");
        await SeedUser(userManager, logger, "viewer@rittal.co.uk", "ViewOnly2025!", "Viewer");

        if (!await db.Trips.AnyAsync())
        {
            logger.LogInformation("Seeding initial trip data...");
            await SeedTrips(db, logger);
        }
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

    private static async Task SeedTrips(RittalTravelContext db, ILogger logger)
    {
        var seedData = new[]
        {
            new { Name = "Ali Hassan",      Origin = "London",     Dest = "Frankfurt",  Mode = "Flight-ShortHaul", Class = "Economy",  Passengers = 1, Date = new DateTime(2025, 1, 14) },
            new { Name = "Marcus Williams", Origin = "Birmingham", Dest = "Brussels",   Mode = "Flight-ShortHaul", Class = "Economy",  Passengers = 2, Date = new DateTime(2025, 1, 22) },
            new { Name = "Sarah Johnson",   Origin = "Manchester", Dest = "London",     Mode = "Train-National",   Class = "Standard", Passengers = 1, Date = new DateTime(2025, 2,  3) },
            new { Name = "David Chen",      Origin = "London",     Dest = "New York",   Mode = "Flight-LongHaul",  Class = "Economy",  Passengers = 1, Date = new DateTime(2025, 2, 11) },
            new { Name = "Emma Clarke",     Origin = "Bristol",    Dest = "Birmingham", Mode = "Car-Diesel",       Class = "N/A",      Passengers = 1, Date = new DateTime(2025, 2, 19) },
            new { Name = "Ali Hassan",      Origin = "Sheffield",  Dest = "Hellaby",    Mode = "Car-Diesel",       Class = "N/A",      Passengers = 1, Date = new DateTime(2025, 3, 18) },
            new { Name = "Chloe Roberts",   Origin = "London",     Dest = "Edinburgh",  Mode = "Flight-Domestic",  Class = "Economy",  Passengers = 1, Date = new DateTime(2025, 4,  2) },
            new { Name = "David Chen",      Origin = "Manchester", Dest = "Leeds",      Mode = "Train-National",   Class = "Standard", Passengers = 1, Date = new DateTime(2025, 4,  8) },
        };
        var distances = new Dictionary<string, double>
        {
            { "London-Frankfurt", 898.0 }, { "Birmingham-Brussels", 475.0 },
            { "Manchester-London", 263.0 }, { "London-New York", 5539.0 },
            { "Bristol-Birmingham", 93.0 }, { "Sheffield-Hellaby", 8.0 },
            { "London-Edinburgh", 534.0 }, { "Manchester-Leeds", 65.0 },
        };
        var trips = new List<Trip>();
        foreach (var s in seedData)
        {
            string key = $"{s.Origin}-{s.Dest}";
            double dist = distances.TryGetValue(key, out double d) ? d : 100.0;
            double factor = DefraCalculator.GetEmissionFactor(s.Mode, s.Class);
            double kg = DefraCalculator.CalculateKgCO2e(dist, factor, s.Passengers);
            trips.Add(new Trip
            {
                OrganisationId = 1, TravellerName = s.Name, Origin = s.Origin,
                Destination = s.Dest, TransportMode = s.Mode, TravelClass = s.Class,
                Passengers = s.Passengers, TripDate = s.Date, DistanceKm = dist,
                EmissionFactor = factor, KgCO2e = kg,
                Formula = DefraCalculator.GetFormula(s.Mode, s.Class, dist, factor, s.Passengers, kg),
                DistanceMethodology = DefraCalculator.GetDistanceMethodology(s.Mode),
                DefraFactorYear = "DEFRA 2025", LoggedBy = "admin@rittal.co.uk", CreatedAt = DateTime.UtcNow,
            });
        }
        db.Trips.AddRange(trips);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} trips", trips.Count);
    }
}

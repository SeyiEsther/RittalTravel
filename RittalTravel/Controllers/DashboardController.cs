using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RittalTravel.Data;
using RittalTravel.Models;

namespace RittalTravel.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(RittalTravelContext db, ILogger<DashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            int currentYear = DateTime.Now.Year;
            var allTrips = await _db.Trips.Where(t => t.OrganisationId == 1)
                .OrderByDescending(t => t.TripDate).ToListAsync();
            var yearTrips = allTrips.Where(t => t.TripDate.Year == currentYear).ToList();

            var vm = new DashboardViewModel
            {
                TotalTco2eThisYear = yearTrips.Sum(t => t.KgCO2e) / 1000.0,
                TotalTrips         = yearTrips.Count,
                UniqueTravellers   = yearTrips.Select(t => t.TravellerName).Distinct().Count(),
                MostTravelledRoute = yearTrips.GroupBy(t => $"{t.Origin} → {t.Destination}")
                    .OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "N/A",
                FlightTco2e = yearTrips.Where(t => t.TransportMode.StartsWith("Flight")).Sum(t => t.KgCO2e) / 1000.0,
                RailTco2e   = yearTrips.Where(t => t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0,
                RoadTco2e   = yearTrips.Where(t => !t.TransportMode.StartsWith("Flight") && !t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0,
                Top5Travellers = yearTrips.GroupBy(t => t.TravellerName)
                    .Select(g => new TopTraveller { Name = g.Key, Trips = g.Count(), TotalKgCO2e = g.Sum(t => t.KgCO2e), TotalTco2e = g.Sum(t => t.KgCO2e) / 1000.0 })
                    .OrderByDescending(x => x.TotalKgCO2e).Take(5).ToList(),
                RecentTrips = allTrips.Take(10).ToList(),
                CurrentYear = currentYear,
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard load failed");
            TempData["Error"] = "Could not load dashboard data. Please try again.";
            return View(new DashboardViewModel());
        }
    }
}

public class DashboardViewModel
{
    public double TotalTco2eThisYear { get; set; }
    public int TotalTrips { get; set; }
    public int UniqueTravellers { get; set; }
    public string MostTravelledRoute { get; set; } = "N/A";
    public double FlightTco2e { get; set; }
    public double RailTco2e { get; set; }
    public double RoadTco2e { get; set; }
    public List<TopTraveller> Top5Travellers { get; set; } = new();
    public List<Trip> RecentTrips { get; set; } = new();
    public int CurrentYear { get; set; } = DateTime.Now.Year;
}

public class TopTraveller
{
    public string Name { get; set; } = "";
    public int Trips { get; set; }
    public double TotalKgCO2e { get; set; }
    public double TotalTco2e { get; set; }
}

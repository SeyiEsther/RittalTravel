using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RittalTravel.Data;
using RittalTravel.Models;
using RittalTravel.Services;

namespace RittalTravel.Controllers;

[Authorize]
public class TripController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly GoogleMapsService _maps;
    private readonly ILogger<TripController> _logger;
    private readonly IConfiguration _config;

    public TripController(RittalTravelContext db, GoogleMapsService maps, ILogger<TripController> logger, IConfiguration config)
    {
        _db = db;
        _maps = maps;
        _logger = logger;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var trips = await _db.Trips
                .Where(t => t.OrganisationId == 1)
                .OrderByDescending(t => t.TripDate)
                .ToListAsync();

            int currentYear = DateTime.Now.Year;
            var yearTrips = trips.Where(t => t.TripDate.Year == currentYear).ToList();

            ViewBag.TotalTco2e     = yearTrips.Sum(t => t.KgCO2e) / 1000.0;
            ViewBag.TotalTrips     = yearTrips.Count;
            ViewBag.UniqueTravellers = yearTrips.Select(t => t.TravellerName).Distinct().Count();
            ViewBag.CurrentYear    = currentYear;

            return View(trips);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trip log load failed");
            TempData["Error"] = "Could not load trip log. Please try again.";
            return View(new List<Trip>());
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult LogTrip()
    {
        ViewBag.GoogleMapsApiKey = _config["GoogleMaps:ApiKey"] ?? "";
        var trip = new Trip
        {
            TripDate   = DateTime.Today,
            Passengers = 1,
            LoggedBy   = User.Identity?.Name ?? "",
        };
        return View(trip);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTrip(Trip trip,
        string? waypointsJson,
        bool returnTrip = false,
        bool differentReturn = false,
        string? returnOrigin = null,
        string? returnDestination = null)
    {
        // Remove server-calculated fields from validation
        ModelState.Remove(nameof(Trip.DistanceKm));
        ModelState.Remove(nameof(Trip.EmissionFactor));
        ModelState.Remove(nameof(Trip.KgCO2e));
        ModelState.Remove(nameof(Trip.Formula));
        ModelState.Remove(nameof(Trip.DistanceMethodology));
        ModelState.Remove(nameof(Trip.DefraFactorYear));
        ModelState.Remove(nameof(Trip.OrganisationId));
        ModelState.Remove(nameof(Trip.Organisation));
        ModelState.Remove(nameof(Trip.CreatedAt));
        ModelState.Remove(nameof(Trip.LoggedBy));
        ModelState.Remove(nameof(Trip.Id));

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("LogTrip ModelState invalid: {Errors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return View(trip);
        }

        try
        {
            // Parse waypoints
            List<WaypointEntry>? waypoints = null;
            if (!string.IsNullOrWhiteSpace(waypointsJson))
            {
                try { waypoints = JsonConvert.DeserializeObject<List<WaypointEntry>>(waypointsJson); }
                catch (Exception ex) { _logger.LogWarning(ex, "Waypoints JSON parse failed"); }
            }

            double totalDistance = 0;
            double totalKg       = 0;
            var formulaParts     = new List<string>();
            string dominantMode  = trip.TransportMode;
            double maxLegDist    = 0;

            if (waypoints != null && waypoints.Count > 0)
            {
                // Multi-leg trip
                var locations = new List<string> { trip.Origin };
                var modes     = new List<string>();
                var classes   = new List<string>();

                foreach (var wp in waypoints)
                {
                    if (string.IsNullOrWhiteSpace(wp.Location))
                    {
                        ModelState.AddModelError("", "All waypoint locations must be filled in.");
                        return View(trip);
                    }
                    locations.Add(wp.Location);
                    modes.Add(wp.Mode ?? trip.TransportMode);
                    classes.Add(wp.TravelClass ?? trip.TravelClass);
                }
                locations.Add(trip.Destination);
                // Last leg uses trip-level mode
                modes.Add(trip.TransportMode);
                classes.Add(trip.TravelClass);

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    string legOrigin = locations[i];
                    string legDest   = locations[i + 1];
                    string legMode   = modes[i];
                    string legClass  = classes[i];

                    double legDist = await _maps.GetDistanceKm(legOrigin, legDest, legMode);
                    double legFactor = DefraCalculator.GetEmissionFactor(legMode, legClass);

                    if (legFactor == 0)
                    {
                        ModelState.AddModelError("", $"Unknown transport mode '{legMode}'.");
                        return View(trip);
                    }
                    if (legDist <= 0)
                    {
                        ModelState.AddModelError("", $"Could not calculate distance for leg {legOrigin} → {legDest}.");
                        return View(trip);
                    }

                    double legKg = DefraCalculator.CalculateKgCO2e(legDist, legFactor, trip.Passengers);
                    totalDistance += legDist;
                    totalKg       += legKg;
                    formulaParts.Add($"Leg {i + 1} ({legMode}): {DefraCalculator.GetFormula(legMode, legClass, legDist, legFactor, trip.Passengers, legKg)}");

                    if (legDist > maxLegDist)
                    {
                        maxLegDist    = legDist;
                        dominantMode  = legMode;
                    }
                }

                trip.Waypoints = waypointsJson;
            }
            else
            {
                // Direct trip
                double dist = await _maps.GetDistanceKm(trip.Origin, trip.Destination, trip.TransportMode);
                double factor = DefraCalculator.GetEmissionFactor(trip.TransportMode, trip.TravelClass);

                if (factor == 0)
                {
                    ModelState.AddModelError("", $"Unknown transport mode '{trip.TransportMode}'.");
                    return View(trip);
                }
                if (dist <= 0)
                {
                    ModelState.AddModelError("", "Could not calculate distance. Check origin and destination.");
                    return View(trip);
                }

                double kg = DefraCalculator.CalculateKgCO2e(dist, factor, trip.Passengers);
                totalDistance = dist;
                totalKg       = kg;
                formulaParts.Add(DefraCalculator.GetFormula(trip.TransportMode, trip.TravelClass, dist, factor, trip.Passengers, kg));
            }

            // Handle return trip
            if (returnTrip)
            {
                string retOrig = differentReturn && !string.IsNullOrWhiteSpace(returnOrigin) ? returnOrigin : trip.Destination;
                string retDest = differentReturn && !string.IsNullOrWhiteSpace(returnDestination) ? returnDestination : trip.Origin;

                double retDist = await _maps.GetDistanceKm(retOrig, retDest, trip.TransportMode);
                double retFactor = DefraCalculator.GetEmissionFactor(trip.TransportMode, trip.TravelClass);
                double retKg = DefraCalculator.CalculateKgCO2e(retDist, retFactor, trip.Passengers);

                totalDistance += retDist;
                totalKg       += retKg;
                formulaParts.Add($"Return: {DefraCalculator.GetFormula(trip.TransportMode, trip.TravelClass, retDist, retFactor, trip.Passengers, retKg)}");
            }

            trip.DistanceKm          = Math.Round(totalDistance, 2);
            trip.EmissionFactor      = DefraCalculator.GetEmissionFactor(dominantMode, trip.TravelClass);
            trip.KgCO2e              = Math.Round(totalKg, 3);
            trip.Formula             = string.Join(" | ", formulaParts);
            trip.DistanceMethodology = DefraCalculator.GetDistanceMethodology(dominantMode);
            trip.TransportMode       = dominantMode;
            trip.DefraFactorYear     = "DEFRA 2025";
            trip.OrganisationId      = 1;
            trip.LoggedBy            = User.Identity?.Name ?? "";
            trip.CreatedAt           = DateTime.UtcNow;

            _db.Trips.Add(trip);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Trip logged: {Name} {Origin} → {Dest} {Kg:F3} kgCO2e by {User}",
                trip.TravellerName, trip.Origin, trip.Destination, trip.KgCO2e, trip.LoggedBy);

            TempData["Success"] = $"Trip logged: {trip.TravellerName} — {trip.Origin} → {trip.Destination} — {trip.KgCO2e:F3} kgCO2e";
            return RedirectToAction(nameof(Index));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Maps HTTP error while logging trip");
            ModelState.AddModelError("", "Could not reach Google Maps API. Check internet connection or API key.");
            return View(trip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error logging trip");
            ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            return View(trip);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var trip = await _db.Trips.FindAsync(id);
            if (trip == null)
            {
                TempData["Error"] = "Trip not found.";
                return RedirectToAction(nameof(Index));
            }

            _db.Trips.Remove(trip);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Trip {Id} deleted by {User}", id, User.Identity?.Name);
            TempData["Success"] = $"Trip for {trip.TravellerName} ({trip.Origin} → {trip.Destination}) deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete trip {Id} failed", id);
            TempData["Error"] = "Could not delete trip. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}

public class WaypointEntry
{
    public string? Location    { get; set; }
    public string? Mode        { get; set; }
    public string? TravelClass { get; set; }
}

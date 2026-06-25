using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RittalTravel.Data;
using RittalTravel.Models;
using RittalTravel.Services;

namespace RittalTravel.Controllers;

public class TripController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly GoogleMapsService _maps;
    private readonly ReceiptParserService _parser;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<TripController> _logger;

    public TripController(RittalTravelContext db, GoogleMapsService maps,
        ReceiptParserService parser, IWebHostEnvironment env, ILogger<TripController> logger)
    {
        _db = db; _maps = maps; _parser = parser; _env = env; _logger = logger;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ParseReceipt(IFormFile? receipt)
    {
        if (receipt == null || receipt.Length == 0)
            return Json(new { success = false, error = "No file received." });

        var ext = Path.GetExtension(receipt.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
            return Json(new { success = false, error = "Only PDF, JPG and PNG files are supported." });

        try
        {
            var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsDir);
            var fileName = Guid.NewGuid() + ext;
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var fs = new FileStream(filePath, FileMode.Create))
                await receipt.CopyToAsync(fs);

            ParsedReceiptData? parsed = null;
            if (ext == ".pdf")
            {
                await using var fs = System.IO.File.OpenRead(filePath);
                parsed = _parser.ParsePdf(fs);
            }

            return Json(new { success = true, fileName, parsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt parse failed");
            return Json(new { success = false, error = "Could not process file." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(int id)
    {
        var trip = await _db.Trips.FindAsync(id);
        if (trip?.ReceiptFileName == null) return NotFound();

        var safeFileName = Path.GetFileName(trip.ReceiptFileName);
        if (string.IsNullOrEmpty(safeFileName)) return NotFound();
        var filePath = Path.Combine(_env.ContentRootPath, "Uploads", safeFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf"          => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"          => "image/png",
            _               => "application/octet-stream"
        };
        var downloadName = $"Receipt-{trip.TravellerName.Replace(" ", "_")}-{trip.TripDate:yyyy-MM-dd}{ext}";
        return PhysicalFile(filePath, contentType, downloadName);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var trips = await _db.Trips.Where(t => t.OrganisationId == 1)
                .OrderByDescending(t => t.TripDate).ToListAsync();
            int currentYear = DateTime.Now.Year;
            var yearTrips = trips.Where(t => t.TripDate.Year == currentYear).ToList();
            ViewBag.TotalTco2e       = yearTrips.Sum(t => t.KgCO2e) / 1000.0;
            ViewBag.TotalTrips       = yearTrips.Count;
            ViewBag.UniqueTravellers = yearTrips.Select(t => t.TravellerName).Distinct().Count();
            ViewBag.CurrentYear      = currentYear;
            return View(trips);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trip log load failed");
            TempData["Error"] = "Could not load trip log. Please try again.";
            ViewBag.TotalTco2e       = 0.0;
            ViewBag.TotalTrips       = 0;
            ViewBag.UniqueTravellers = 0;
            ViewBag.CurrentYear      = DateTime.Now.Year;
            return View(new List<Trip>());
        }
    }

    [HttpGet]
    public IActionResult LogTrip()
    {
        return View(new Trip { TripDate = DateTime.Today, Passengers = 1, LoggedBy = User.Identity?.Name ?? "" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTrip(Trip trip, List<WaypointEntry>? waypoints,
        bool returnTrip = false, bool differentReturn = false,
        string? returnOrigin = null, string? returnDestination = null,
        bool accountForDetours = false, string? receiptFileName = null)
    {
        foreach (var f in new[] { nameof(Trip.DistanceKm), nameof(Trip.EmissionFactor), nameof(Trip.KgCO2e),
            nameof(Trip.Formula), nameof(Trip.DistanceMethodology), nameof(Trip.DefraFactorYear),
            nameof(Trip.OrganisationId), nameof(Trip.Organisation), nameof(Trip.CreatedAt), nameof(Trip.LoggedBy),
            nameof(Trip.Id), nameof(Trip.ReceiptFileName) })
            ModelState.Remove(f);

        if (!ModelState.IsValid) return View(trip);

        var legs = waypoints?.Where(w => !string.IsNullOrWhiteSpace(w.Location)).ToList();

        try
        {
            double totalDist = 0, totalKg = 0, maxLegDist = 0;
            string dominantMode = trip.TransportMode;
            var formulaParts = new List<string>();

            if (legs != null && legs.Count > 0)
            {
                var locs = new List<string> { trip.Origin };
                locs.AddRange(legs.Select(w => w.Location!));
                locs.Add(trip.Destination);
                var modes   = legs.Select(w => w.Mode        ?? trip.TransportMode).Append(trip.TransportMode).ToList();
                var classes = legs.Select(w => w.TravelClass ?? trip.TravelClass).Append(trip.TravelClass).ToList();
                for (int i = 0; i < locs.Count - 1; i++)
                {
                    double legDist   = await _maps.GetDistanceKm(locs[i], locs[i + 1], modes[i], accountForDetours);
                    double legFactor = DefraCalculator.GetEmissionFactor(modes[i], classes[i]);
                    if (legFactor == 0 || legDist <= 0) { ModelState.AddModelError("", $"Could not calculate leg {locs[i]} to {locs[i + 1]}."); return View(trip); }
                    double legKg = DefraCalculator.CalculateKgCO2e(legDist, legFactor, trip.Passengers);
                    totalDist += legDist; totalKg += legKg;
                    formulaParts.Add($"Leg {i + 1} ({modes[i]}): {DefraCalculator.GetFormula(modes[i], classes[i], legDist, legFactor, trip.Passengers, legKg)}");
                    if (legDist > maxLegDist) { maxLegDist = legDist; dominantMode = modes[i]; }
                }
                trip.Waypoints = JsonConvert.SerializeObject(legs);
            }
            else
            {
                double dist   = await _maps.GetDistanceKm(trip.Origin, trip.Destination, trip.TransportMode, accountForDetours);
                double factor = DefraCalculator.GetEmissionFactor(trip.TransportMode, trip.TravelClass);
                if (factor == 0 || dist <= 0) { ModelState.AddModelError("", "Could not calculate distance. Check origin, destination and mode."); return View(trip); }
                double kg = DefraCalculator.CalculateKgCO2e(dist, factor, trip.Passengers);
                totalDist = dist; totalKg = kg;
                formulaParts.Add(DefraCalculator.GetFormula(trip.TransportMode, trip.TravelClass, dist, factor, trip.Passengers, kg));
            }

            if (returnTrip)
            {
                string ro  = differentReturn && !string.IsNullOrWhiteSpace(returnOrigin)      ? returnOrigin!      : trip.Destination;
                string rd  = differentReturn && !string.IsNullOrWhiteSpace(returnDestination) ? returnDestination! : trip.Origin;
                double rd2 = await _maps.GetDistanceKm(ro, rd, trip.TransportMode, accountForDetours);
                double rf  = DefraCalculator.GetEmissionFactor(trip.TransportMode, trip.TravelClass);
                double rkg = DefraCalculator.CalculateKgCO2e(rd2, rf, trip.Passengers);
                totalDist += rd2; totalKg += rkg;
                formulaParts.Add($"Return: {DefraCalculator.GetFormula(trip.TransportMode, trip.TravelClass, rd2, rf, trip.Passengers, rkg)}");
            }

            trip.DistanceKm          = Math.Round(totalDist, 2);
            trip.EmissionFactor      = DefraCalculator.GetEmissionFactor(dominantMode, trip.TravelClass);
            trip.KgCO2e              = Math.Round(totalKg, 3);
            trip.Formula             = string.Join(" | ", formulaParts);
            trip.DistanceMethodology = DefraCalculator.GetDistanceMethodology(dominantMode);
            trip.TransportMode       = dominantMode;
            trip.DefraFactorYear     = "DEFRA 2025";
            trip.OrganisationId      = 1;
            trip.LoggedBy            = User.Identity?.Name ?? "";
            trip.CreatedAt           = DateTime.UtcNow;
            trip.ReceiptFileName     = receiptFileName;

            _db.Trips.Add(trip);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Trip logged: {trip.TravellerName} - {trip.Origin} to {trip.Destination} - {trip.KgCO2e:F3} kgCO2e";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging trip");
            ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
            return View(trip);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var trip = await _db.Trips.FindAsync(id);
            if (trip == null) { TempData["Error"] = "Trip not found."; return RedirectToAction(nameof(Index)); }
            _db.Trips.Remove(trip);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Trip for {trip.TravellerName} deleted.";
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

using System.Text.Json;

namespace RittalTravel.Services;

public class OsrmRoutingService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly ILogger<OsrmRoutingService> _logger;

    public OsrmRoutingService(HttpClient http, IConfiguration config, ILogger<OsrmRoutingService> logger)
    {
        _http    = http;
        _baseUrl = config["Osrm:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger  = logger;
    }

    /// <summary>
    /// Queries the OSRM driving profile for the physical road distance between two coordinate pairs.
    /// When <paramref name="excludeMotorway"/> is true an exclusion tag is sent so the graph solver
    /// avoids motorway links and returns the realistic detour distance.
    /// Returns null when OSRM is unreachable or returns a non-OK status.
    /// </summary>
    public async Task<double?> GetDistanceKm(
        double lat1, double lon1,
        double lat2, double lon2,
        bool excludeMotorway = false)
    {
        // OSRM coordinate order is longitude,latitude
        string coords = $"{lon1},{lat1};{lon2},{lat2}";
        string url    = $"{_baseUrl}/route/v1/driving/{coords}?overview=false&steps=false";
        if (excludeMotorway) url += "&exclude=motorway";

        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.GetProperty("code").GetString() != "Ok")
            {
                _logger.LogWarning("OSRM returned non-OK code for {lat1},{lon1} → {lat2},{lon2}", lat1, lon1, lat2, lon2);
                return null;
            }
            double metres = root.GetProperty("routes")[0].GetProperty("distance").GetDouble();
            return Math.Round(metres / 1000.0, 6);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSRM unavailable for {lat1},{lon1} → {lat2},{lon2}; will fall back", lat1, lon1, lat2, lon2);
            return null;
        }
    }
}

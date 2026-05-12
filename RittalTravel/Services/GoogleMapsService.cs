using System.Text.Json;

namespace RittalTravel.Services;

public class GoogleMapsService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<GoogleMapsService> _logger;

    public GoogleMapsService(HttpClient http, IConfiguration config, ILogger<GoogleMapsService> logger)
    {
        _http = http;
        _apiKey = config["GoogleMaps:ApiKey"];
        _logger = logger;
        if (string.IsNullOrWhiteSpace(_apiKey))
            _logger.LogWarning("Google Maps API key is not configured. Distance calculations will fall back to Haversine.");
    }

    public async Task<double> GetDistanceKm(string origin, string destination, string mode)
    {
        if (mode.StartsWith("Flight", StringComparison.OrdinalIgnoreCase) ||
            mode.StartsWith("Ferry", StringComparison.OrdinalIgnoreCase))
            return await GetFlightDistanceKm(origin, destination);
        return await GetRoadDistanceKm(origin, destination, mode);
    }

    public async Task<double> GetRoadDistanceKm(string origin, string destination, string mode)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No API key — using Haversine fallback for {O} to {D}", origin, destination);
            return await GetFlightDistanceKm(origin, destination);
        }
        string travelMode = mode.StartsWith("Train", StringComparison.OrdinalIgnoreCase) ? "transit" : "driving";
        string url = $"https://maps.googleapis.com/maps/api/distancematrix/json"
                   + $"?origins={Uri.EscapeDataString(origin)}&destinations={Uri.EscapeDataString(destination)}"
                   + $"&mode={travelMode}&key={_apiKey}";
        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.GetProperty("status").GetString() == "REQUEST_DENIED")
            {
                _logger.LogError("Google Maps API request denied.");
                return await GetFlightDistanceKm(origin, destination);
            }
            var rows = root.GetProperty("rows");
            if (rows.GetArrayLength() == 0) return await GetFlightDistanceKm(origin, destination);
            var element = rows[0].GetProperty("elements")[0];
            if (element.GetProperty("status").GetString() != "OK") return await GetFlightDistanceKm(origin, destination);
            double metres = element.GetProperty("distance").GetProperty("value").GetDouble();
            return metres / 1000.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Distance Matrix error for {O} to {D}", origin, destination);
            return await GetFlightDistanceKm(origin, destination);
        }
    }

    public async Task<double> GetFlightDistanceKm(string origin, string destination)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return FallbackHaversine(origin, destination);
        try
        {
            double[]? orig = await Geocode(origin);
            double[]? dest = await Geocode(destination);
            if (orig == null || dest == null) return FallbackHaversine(origin, destination);
            return DefraCalculator.HaversineDistance(orig[0], orig[1], dest[0], dest[1]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocode error for {O} to {D}", origin, destination);
            return FallbackHaversine(origin, destination);
        }
    }

    private async Task<double[]?> Geocode(string address)
    {
        string url = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_apiKey}";
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        if (root.GetProperty("status").GetString() != "OK") return null;
        var results = root.GetProperty("results");
        if (results.GetArrayLength() == 0) return null;
        var loc = results[0].GetProperty("geometry").GetProperty("location");
        return new[] { loc.GetProperty("lat").GetDouble(), loc.GetProperty("lng").GetDouble() };
    }

    private static double FallbackHaversine(string origin, string destination)
    {
        var coords = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "London",     new[]{51.5074,-0.1278}  }, { "Manchester", new[]{53.4808,-2.2426} },
            { "Birmingham", new[]{52.4862,-1.8904}  }, { "Edinburgh",  new[]{55.9533,-3.1883} },
            { "Bristol",   new[]{51.4545,-2.5879}   }, { "Leeds",      new[]{53.8008,-1.5491} },
            { "Sheffield", new[]{53.3811,-1.4701}   }, { "Hellaby",    new[]{53.4167,-1.2833} },
            { "Frankfurt", new[]{50.1109, 8.6821}   }, { "Brussels",   new[]{50.8503, 4.3517} },
            { "New York",  new[]{40.7128,-74.0060}  }, { "Paris",      new[]{48.8566, 2.3522} },
            { "Amsterdam", new[]{52.3676, 4.9041}   }, { "Berlin",     new[]{52.5200,13.4050} },
            { "Dublin",    new[]{53.3498,-6.2603}   },
        };
        double[] orig = FindCity(origin, coords) ?? new[]{51.5,-0.1};
        double[] dest = FindCity(destination, coords) ?? new[]{51.5,-0.1};
        return DefraCalculator.HaversineDistance(orig[0], orig[1], dest[0], dest[1]);
    }

    private static double[]? FindCity(string name, Dictionary<string, double[]> coords)
    {
        foreach (var k in coords.Keys)
            if (name.Contains(k, StringComparison.OrdinalIgnoreCase)) return coords[k];
        return null;
    }
}

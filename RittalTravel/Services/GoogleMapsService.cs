using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        {
            return await GetFlightDistanceKm(origin, destination);
        }

        return await GetRoadDistanceKm(origin, destination, mode);
    }

    public async Task<double> GetRoadDistanceKm(string origin, string destination, string mode)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No API key — using Haversine fallback for road distance {O} to {D}", origin, destination);
            return await GetFlightDistanceKm(origin, destination);
        }

        string travelMode = mode.StartsWith("Train", StringComparison.OrdinalIgnoreCase) ? "transit" : "driving";

        string url = $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                     $"?origins={Uri.EscapeDataString(origin)}" +
                     $"&destinations={Uri.EscapeDataString(destination)}" +
                     $"&mode={travelMode}" +
                     $"&key={_apiKey}";

        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string status = root.GetProperty("status").GetString() ?? "";
            if (status == "REQUEST_DENIED")
            {
                _logger.LogError("Google Maps API request denied. Check API key and permissions.");
                return await GetFlightDistanceKm(origin, destination);
            }

            var rows = root.GetProperty("rows");
            if (rows.GetArrayLength() == 0)
            {
                _logger.LogWarning("ZERO_RESULTS from Distance Matrix for {O} to {D}", origin, destination);
                return await GetFlightDistanceKm(origin, destination);
            }

            var elements = rows[0].GetProperty("elements");
            if (elements.GetArrayLength() == 0)
                return await GetFlightDistanceKm(origin, destination);

            var element = elements[0];
            string elemStatus = element.GetProperty("status").GetString() ?? "";
            if (elemStatus == "ZERO_RESULTS" || elemStatus != "OK")
            {
                _logger.LogWarning("Distance Matrix element status {S} for {O} to {D}", elemStatus, origin, destination);
                return await GetFlightDistanceKm(origin, destination);
            }

            double metres = element.GetProperty("distance").GetProperty("value").GetDouble();
            _logger.LogInformation("Road distance {O} to {D}: {Km:F1} km", origin, destination, metres / 1000.0);
            return metres / 1000.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Distance Matrix API error for {O} to {D}", origin, destination);
            return await GetFlightDistanceKm(origin, destination);
        }
    }

    public async Task<double> GetFlightDistanceKm(string origin, string destination)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No API key — using approximate Haversine. Geocode unavailable.");
            return FallbackHaversine(origin, destination);
        }

        try
        {
            double[]? orig = await Geocode(origin);
            double[]? dest = await Geocode(destination);

            if (orig == null || dest == null)
            {
                _logger.LogWarning("Geocode failed for {O} or {D} — using fallback", origin, destination);
                return FallbackHaversine(origin, destination);
            }

            double dist = DefraCalculator.HaversineDistance(orig[0], orig[1], dest[0], dest[1]);
            _logger.LogInformation("Haversine distance {O} to {D}: {Km:F1} km", origin, destination, dist);
            return dist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocode API error for flight distance {O} to {D}", origin, destination);
            return FallbackHaversine(origin, destination);
        }
    }

    private async Task<double[]?> Geocode(string address)
    {
        string url = $"https://maps.googleapis.com/maps/api/geocode/json" +
                     $"?address={Uri.EscapeDataString(address)}" +
                     $"&key={_apiKey}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string status = root.GetProperty("status").GetString() ?? "";
        if (status != "OK")
        {
            _logger.LogWarning("Geocode status {S} for address {A}", status, address);
            return null;
        }

        var results = root.GetProperty("results");
        if (results.GetArrayLength() == 0) return null;

        var loc = results[0].GetProperty("geometry").GetProperty("location");
        return new[] { loc.GetProperty("lat").GetDouble(), loc.GetProperty("lng").GetDouble() };
    }

    // Approximate city coordinates for fallback when no API key
    private static double FallbackHaversine(string origin, string destination)
    {
        var coords = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "London",       new[] { 51.5074, -0.1278 } },
            { "Manchester",   new[] { 53.4808, -2.2426 } },
            { "Birmingham",   new[] { 52.4862, -1.8904 } },
            { "Edinburgh",    new[] { 55.9533, -3.1883 } },
            { "Bristol",      new[] { 51.4545, -2.5879 } },
            { "Leeds",        new[] { 53.8008, -1.5491 } },
            { "Sheffield",    new[] { 53.3811, -1.4701 } },
            { "Hellaby",      new[] { 53.4167, -1.2833 } },
            { "Frankfurt",    new[] { 50.1109, 8.6821  } },
            { "Brussels",     new[] { 50.8503, 4.3517  } },
            { "New York",     new[] { 40.7128, -74.0060} },
            { "Paris",        new[] { 48.8566, 2.3522  } },
            { "Amsterdam",    new[] { 52.3676, 4.9041  } },
            { "Berlin",       new[] { 52.5200, 13.4050 } },
            { "Dublin",       new[] { 53.3498, -6.2603 } },
        };

        double[] orig = FindCity(origin, coords) ?? new[] { 51.5, -0.1 };
        double[] dest = FindCity(destination, coords) ?? new[] { 51.5, -0.1 };
        return DefraCalculator.HaversineDistance(orig[0], orig[1], dest[0], dest[1]);
    }

    private static double[]? FindCity(string name, Dictionary<string, double[]> coords)
    {
        foreach (var key in coords.Keys)
        {
            if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                return coords[key];
        }
        return null;
    }
}

namespace RittalTravel.Services;

public static class DefraCalculator
{
    // DEFRA 2025 GHG Conversion Factors - Table 5: Business Travel (kgCO2e per passenger-km)

    public const double Diesel_kgCO2e_per_litre = 2.51610;
    public const double Petrol_kgCO2e_per_litre = 2.16080;

    public static readonly Dictionary<string, double> FreightFactors = new()
    {
        { "HGV-Articulated", 0.07647 },
        { "HGV-Rigid",       0.14876 },
        { "Van",             0.27228 }
    };

    private static readonly Dictionary<string, double> Factors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Flight-Domestic",     0.24468 },
        { "Flight-ShortHaul",    0.15553 },
        { "Flight-LongHaul",     0.19085 },
        { "Train-National",      0.03549 },
        // DEFRA 2025/2026 DESNZ verified TTW factor for international high-speed rail (e.g. Eurostar)
        { "Train-International", 0.004432 },
        { "Train-Underground",   0.02800 },
        { "Car-Diesel",          0.16844 },
        { "Car-Petrol",          0.17380 },
        { "Car-Electric",        0.04714 },
        { "Car-Hybrid",          0.11565 },
        { "Bus-Local",           0.10213 },
        { "Coach-National",      0.02722 },
        { "Taxi-Regular",        0.14938 },
        { "Taxi-Electric",       0.04714 },
        { "Ferry-Foot",          0.01867 },
    };

    // Physical rail network track distances (km) for known international corridors.
    // These replace straight-line Haversine values for GHG Protocol Scope 3 Cat. 6 compliance.
    private static readonly Dictionary<string, double> RailNetworkDistances =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "London|Paris",      492.0 },
        { "Paris|London",      492.0 },
        { "London|Brussels",   356.0 },
        { "Brussels|London",   356.0 },
        { "London|Amsterdam",  458.0 },
        { "Amsterdam|London",  458.0 },
        { "Paris|Brussels",    265.0 },
        { "Brussels|Paris",    265.0 },
        { "Paris|Amsterdam",   429.0 },
        { "Amsterdam|Paris",   429.0 },
        { "Brussels|Amsterdam",173.0 },
        { "Amsterdam|Brussels",173.0 },
    };

    private static readonly Dictionary<string, double> ClassMultipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Economy",  1.0 }, { "Business", 2.0 }, { "First", 2.4 },
        { "Standard", 1.0 }, { "Premium",  1.6 },
    };

    public static double GetEmissionFactor(string mode, string travelClass = "Economy")
    {
        if (!Factors.TryGetValue(mode, out double factor)) return 0.0;
        if (mode.StartsWith("Flight", StringComparison.OrdinalIgnoreCase))
        {
            double multiplier = ClassMultipliers.TryGetValue(travelClass, out double m) ? m : 1.0;
            return factor * multiplier;
        }
        return factor;
    }

    public static double CalculateKgCO2e(double distanceKm, double emissionFactor, int passengers = 1)
        => distanceKm * emissionFactor * passengers;

    public static string GetFormula(string mode, string travelClass, double distanceKm, double factor, int passengers, double kgCO2e)
    {
        string classNote = mode.StartsWith("Flight", StringComparison.OrdinalIgnoreCase)
            ? $" x {travelClass} class multiplier" : "";
        return $"{distanceKm:F1} km x {factor:F5} kgCO2e/km{classNote} x {passengers} pax = {kgCO2e:F3} kgCO2e";
    }

    public static string GetDistanceMethodology(string mode)
    {
        if (mode.StartsWith("Flight", StringComparison.OrdinalIgnoreCase)) return "Haversine Great Circle";
        if (mode.StartsWith("Ferry", StringComparison.OrdinalIgnoreCase))  return "Haversine Great Circle";
        if (mode.StartsWith("Train", StringComparison.OrdinalIgnoreCase))  return "Network Track Distance (Rail/GTFS)";
        return "Network Routing Graph (OSRM/Google Maps)";
    }

    /// <summary>
    /// Attempts to resolve the physical rail network track distance for a known international corridor.
    /// City names are matched on the first comma-delimited token (case-insensitive).
    /// </summary>
    public static bool TryGetRailNetworkDistance(string origin, string destination, out double distanceKm)
    {
        string o = NormaliseCity(origin);
        string d = NormaliseCity(destination);
        return RailNetworkDistances.TryGetValue($"{o}|{d}", out distanceKm);
    }

    private static string NormaliseCity(string city)
        => city.Split(',')[0].Trim();

    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad(lat2 - lat1), dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat/2)*Math.Sin(dLat/2)
                 + Math.Cos(ToRad(lat1))*Math.Cos(ToRad(lat2))*Math.Sin(dLon/2)*Math.Sin(dLon/2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
    }

    private static double ToRad(double d) => d * Math.PI / 180.0;
}

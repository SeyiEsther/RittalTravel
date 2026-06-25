using System.Text;
using System.Text.RegularExpressions;

namespace RittalTravel.Services;

public class ParsedReceiptData
{
    public string? TravellerName { get; set; }
    public string? Origin { get; set; }
    public string? Destination { get; set; }
    public string? TripDate { get; set; }
    public string? TransportMode { get; set; }
    public string? TravelClass { get; set; }
    public int? Passengers { get; set; }
    public string? Purpose { get; set; }
}

public class ReceiptParserService
{
    public ParsedReceiptData ParsePdf(Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var text = ExtractPdfText(ms.ToArray());
            return ParseText(text);
        }
        catch
        {
            return new ParsedReceiptData();
        }
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        // Scan raw PDF content streams for text operators (Tj and TJ).
        // Works well for text-based PDFs (e-tickets, booking confirmations).
        var raw = Encoding.Latin1.GetString(bytes);
        var sb = new StringBuilder();

        // Simple Tj: (some text) Tj
        foreach (Match m in Regex.Matches(raw, @"\(([^)]{1,300})\)\s*Tj"))
            sb.AppendLine(SanitisePdfString(m.Groups[1].Value));

        // Array TJ: [(text1) kern (text2) ...] TJ
        foreach (Match m in Regex.Matches(raw, @"\[([^\]]{1,1000})\]\s*TJ"))
        {
            foreach (Match part in Regex.Matches(m.Groups[1].Value, @"\(([^)]{1,300})\)"))
                sb.Append(SanitisePdfString(part.Groups[1].Value)).Append(' ');
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string SanitisePdfString(string s)
    {
        s = Regex.Replace(s, @"\\[0-7]{1,3}", m =>
        {
            try { return ((char)Convert.ToInt32(m.Value[1..], 8)).ToString(); } catch { return " "; }
        });
        s = Regex.Replace(s, @"\\.", " ");
        return new string(s.Where(c => c is >= ' ' and <= '~').ToArray());
    }

    public ParsedReceiptData ParseText(string text)
    {
        var result = new ParsedReceiptData();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lower = text.ToLower();

        result.TransportMode = DetectMode(lower);

        if (lower.Contains("trainline") || lower.Contains("lner") || lower.Contains("avanti") ||
            lower.Contains("great western") || lower.Contains("national rail") ||
            lower.Contains("tfl") || lower.Contains("eurostar") ||
            result.TransportMode is "Train-National" or "Train-International")
            ParseTrain(text, lower, result);
        else if (lower.Contains("uber") || lower.Contains("bolt") || lower.Contains("addison lee") ||
                 result.TransportMode is "Taxi-Regular" or "Taxi-Electric")
            ParseTaxi(text, result);
        else if (lower.Contains("flight") || lower.Contains("boarding") || lower.Contains("ryanair") ||
                 lower.Contains("easyjet") || lower.Contains("british airways") || lower.Contains("lufthansa") ||
                 result.TransportMode?.StartsWith("Flight") == true)
            ParseFlight(text, lower, result);
        else
            ParseGeneric(text, result);

        result.TripDate ??= ExtractDate(text);
        result.TravellerName ??= ExtractName(text);
        result.Passengers ??= ExtractPassengers(text);

        return result;
    }

    private static string? DetectMode(string lower)
    {
        if (lower.Contains("uber") || lower.Contains("bolt") || lower.Contains("taxi") || lower.Contains("addison lee"))
            return "Taxi-Regular";
        if (lower.Contains("eurostar"))
            return "Train-International";
        if (lower.Contains("trainline") || lower.Contains("lner") || lower.Contains("avanti") ||
            lower.Contains("national rail") || lower.Contains("tfl") || lower.Contains(" train ") ||
            lower.Contains("station") || lower.Contains("platform"))
            return "Train-National";
        if (lower.Contains("ryanair") || lower.Contains("easyjet") || lower.Contains("british airways") ||
            lower.Contains("flight") || lower.Contains("airline") || lower.Contains("boarding pass"))
            return "Flight-ShortHaul";
        if (lower.Contains("national express") || lower.Contains("megabus") || lower.Contains("coach"))
            return "Coach-National";
        if (lower.Contains("ferry"))
            return "Ferry-Foot";
        return null;
    }

    private static void ParseTrain(string text, string lower, ParsedReceiptData r)
    {
        // "London Paddington to Bristol Temple Meads"
        var m = Regex.Match(text, @"([A-Z][a-zA-Z &']+?)\s+to\s+([A-Z][a-zA-Z &']+?)(?:\r|\n|\s{2,}|$)", RegexOptions.Multiline);
        if (m.Success) { r.Origin = Clean(m.Groups[1].Value); r.Destination = Clean(m.Groups[2].Value); }

        // Arrow format: "Kings Cross → Edinburgh"
        var arrow = Regex.Match(text, @"([A-Z][a-zA-Z\s]+?)\s*[→>]\s*([A-Z][a-zA-Z\s]+?)(?:\r|\n|\s{2,}|$)");
        if (arrow.Success && r.Origin == null) { r.Origin = Clean(arrow.Groups[1].Value); r.Destination = Clean(arrow.Groups[2].Value); }

        r.TripDate ??= ExtractDate(text);
        r.TravellerName ??= ExtractName(text);

        if (lower.Contains("eurostar") || lower.Contains("paris") || lower.Contains("brussels") || lower.Contains("amsterdam"))
            r.TransportMode = "Train-International";
        else
            r.TransportMode ??= "Train-National";

        if (Regex.IsMatch(text, @"\bfirst\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "First";
        else if (Regex.IsMatch(text, @"\bstandard\s*class\b|\bstandard\b", RegexOptions.IgnoreCase)) r.TravelClass = "Standard";
    }

    private static void ParseTaxi(string text, ParsedReceiptData r)
    {
        // "Trip from X to Y"
        var m = Regex.Match(text, @"[Tt]rip\s+from\s+(.+?)\s+to\s+(.+?)(?:\r|\n|$)", RegexOptions.Multiline);
        if (m.Success) { r.Origin = Clean(m.Groups[1].Value); r.Destination = Clean(m.Groups[2].Value); }

        // "Pickup: X" / "Dropoff: Y"
        var pu = Regex.Match(text, @"[Pp]ick[- ]?up[:\s]+(.+?)(?:\r|\n|$)");
        var do_ = Regex.Match(text, @"[Dd]rop[- ]?off[:\s]+(.+?)(?:\r|\n|$)");
        if (pu.Success && r.Origin == null) r.Origin = Clean(pu.Groups[1].Value);
        if (do_.Success && r.Destination == null) r.Destination = Clean(do_.Groups[1].Value);

        r.TransportMode ??= "Taxi-Regular";
        r.Passengers ??= 1;
    }

    private static void ParseFlight(string text, string lower, ParsedReceiptData r)
    {
        // "LHR → CDG" or "LGW to BCN"
        var iata = Regex.Match(text, @"\b([A-Z]{3})\s*[→>]\s*([A-Z]{3})\b");
        if (iata.Success) { r.Origin = iata.Groups[1].Value; r.Destination = iata.Groups[2].Value; }

        // "London Gatwick (LGW) → Barcelona (BCN)"
        var named = Regex.Match(text, @"([A-Z][a-zA-Z\s]+?)\s*\([A-Z]{3}\)\s*[→>]\s*([A-Z][a-zA-Z\s]+?)\s*\([A-Z]{3}\)");
        if (named.Success && r.Origin == null) { r.Origin = Clean(named.Groups[1].Value); r.Destination = Clean(named.Groups[2].Value); }

        if (Regex.IsMatch(text, @"\bbusiness\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "Business";
        else if (Regex.IsMatch(text, @"\bfirst\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "First";
        else r.TravelClass = "Economy";

        // Rough domestic vs short/long heuristic
        if (lower.Contains("domestic") || lower.Contains("internal"))
            r.TransportMode = "Flight-Domestic";
        else if (lower.Contains("long haul") || lower.Contains("longhaul") ||
                 lower.Contains("new york") || lower.Contains("dubai") || lower.Contains("singapore") ||
                 lower.Contains("usa") || lower.Contains("united states") || lower.Contains("australia"))
            r.TransportMode = "Flight-LongHaul";
        else
            r.TransportMode ??= "Flight-ShortHaul";
    }

    private static void ParseGeneric(string text, ParsedReceiptData r)
    {
        var m = Regex.Match(text,
            @"(?:from|origin|departure)[:\s]+([A-Z][a-zA-Z\s,]+?)(?:\s+(?:to|destination|arrival)[:\s]+([A-Z][a-zA-Z\s,]+?))?(?:\r|\n|\s{2,}|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (m.Success)
        {
            if (r.Origin == null && m.Groups[1].Success) r.Origin = Clean(m.Groups[1].Value);
            if (r.Destination == null && m.Groups[2].Success) r.Destination = Clean(m.Groups[2].Value);
        }
    }

    private static string? ExtractDate(string text)
    {
        // "25 June 2025" / "25 Jun 2025" (with optional day-of-week prefix)
        var m = Regex.Match(text,
            @"\b(\d{1,2})\s+(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+(\d{4})\b",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            int day = int.Parse(m.Groups[1].Value), month = MonthNum(m.Groups[2].Value), year = int.Parse(m.Groups[3].Value);
            if (month > 0 && DateTime.TryParse($"{year}-{month:D2}-{day:D2}", out var d)) return d.ToString("yyyy-MM-dd");
        }

        // "25/06/2025" or "25-06-2025"
        var m2 = Regex.Match(text, @"\b(\d{2})[/\-](\d{2})[/\-](\d{4})\b");
        if (m2.Success && DateTime.TryParse($"{m2.Groups[3].Value}-{m2.Groups[2].Value}-{m2.Groups[1].Value}", out var d2))
            return d2.ToString("yyyy-MM-dd");

        // ISO "2025-06-25"
        var m3 = Regex.Match(text, @"\b(20\d{2})[/\-](\d{2})[/\-](\d{2})\b");
        if (m3.Success && DateTime.TryParse($"{m3.Groups[1].Value}-{m3.Groups[2].Value}-{m3.Groups[3].Value}", out var d3))
            return d3.ToString("yyyy-MM-dd");

        return null;
    }

    private static string? ExtractName(string text)
    {
        var patterns = new[]
        {
            @"[Pp]assenger[:\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})",
            @"[Nn]ame[:\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})",
            @"[Bb]ooked\s+for[:\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})",
            @"[Tt]ravell?er[:\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})",
            @"[Tt]icket\s+(?:holder|for)[:\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})",
            @"[Dd]ear\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)",
            @"[Hh]ello,?\s+([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)",
        };
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p);
            if (m.Success)
            {
                var name = Clean(m.Groups[1].Value);
                if (name.Length is > 3 and < 60) return name;
            }
        }
        // All-caps name e.g. "MR JOHN SMITH" from flight bookings
        var caps = Regex.Match(text, @"\b(MR|MRS|MS|DR|MISS)\s+([A-Z]{2,}\s+[A-Z]{2,}(?:\s+[A-Z]{2,})?)\b");
        if (caps.Success) return ToTitleCase(caps.Groups[2].Value);
        return null;
    }

    private static int? ExtractPassengers(string text)
    {
        var m = Regex.Match(text, @"(\d+)\s*(?:adult|passenger|traveller|ticket)s?", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int p) && p is >= 1 and <= 50) return p;
        return null;
    }

    private static int MonthNum(string s) => s.ToLower() switch
    {
        var x when x.StartsWith("jan") => 1, var x when x.StartsWith("feb") => 2,
        var x when x.StartsWith("mar") => 3, var x when x.StartsWith("apr") => 4,
        var x when x.StartsWith("may") => 5, var x when x.StartsWith("jun") => 6,
        var x when x.StartsWith("jul") => 7, var x when x.StartsWith("aug") => 8,
        var x when x.StartsWith("sep") => 9, var x when x.StartsWith("oct") => 10,
        var x when x.StartsWith("nov") => 11, var x when x.StartsWith("dec") => 12,
        _ => 0
    };

    private static string Clean(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

    private static string ToTitleCase(string s) =>
        string.Join(" ", s.Split(' ').Select(w => w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..].ToLower()));
}

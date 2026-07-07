using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Microsoft.Extensions.Logging;

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
    /// <summary>Shown when auto-extraction could not run or found nothing useful.</summary>
    public string? ExtractionNote { get; set; }
}

public class ReceiptParserService
{
    private readonly ILogger<ReceiptParserService> _logger;

    public ReceiptParserService(ILogger<ReceiptParserService> logger) => _logger = logger;

    public ParsedReceiptData ParseFile(Stream stream, string extension, string? originalFileName = null)
    {
        extension = extension.ToLowerInvariant();
        var result = extension switch
        {
            ".pdf"              => ParsePdf(stream),
            ".jpg" or ".jpeg"
                or ".png"       => new ParsedReceiptData
                {
                    ExtractionNote = "Photo and scanned image files cannot be auto-read. Upload a PDF e-ticket or booking confirmation, or enter trip details manually."
                },
            _ => new ParsedReceiptData { ExtractionNote = "Unsupported file type for auto-extraction." }
        };

        ApplyFilenameHints(originalFileName, result);
        if (!HasUsefulData(result) && result.ExtractionNote == null)
            result.ExtractionNote = "Could not recognise trip details in this document. Check it is a travel receipt or e-ticket, or enter details manually.";

        return result;
    }

    public ParsedReceiptData ParsePdf(Stream stream)
    {
        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            var text = ExtractPdfText(bytes);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("PDF text extraction returned empty — file may be a scanned image PDF");
                return new ParsedReceiptData
                {
                    ExtractionNote = "No readable text found in this PDF (it may be a scanned image). Enter trip details manually."
                };
            }

            _logger.LogInformation("Extracted {Chars} characters from PDF for parsing", text.Length);
            return ParseText(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF parse failed");
            return new ParsedReceiptData { ExtractionNote = "Could not read this PDF. Enter trip details manually." };
        }
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var sb = new StringBuilder();
        try
        {
            using var document = PdfDocument.Open(bytes);
            foreach (var page in document.GetPages())
                sb.AppendLine(page.Text);
        }
        catch
        {
            // Fallback for unusual PDFs if PdfPig cannot open them
            sb.Append(ExtractPdfTextLegacy(bytes));
        }

        var text = NormaliseWhitespace(sb.ToString());
        if (text.Length >= 40) return text;

        // Some PDFs need the legacy raw-stream scan as a second pass
        var legacy = NormaliseWhitespace(ExtractPdfTextLegacy(bytes));
        return legacy.Length > text.Length ? legacy : text;
    }

    private static string ExtractPdfTextLegacy(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var sb = new StringBuilder();

        foreach (Match m in Regex.Matches(raw, @"\(([^)]{1,500})\)\s*Tj"))
            sb.AppendLine(SanitisePdfString(m.Groups[1].Value));

        foreach (Match m in Regex.Matches(raw, @"\[([^\]]{1,2000})\]\s*TJ"))
        {
            foreach (Match part in Regex.Matches(m.Groups[1].Value, @"\(([^)]{1,500})\)"))
                sb.Append(SanitisePdfString(part.Groups[1].Value)).Append(' ');
            sb.AppendLine();
        }

        foreach (Match m in Regex.Matches(raw, @"<([0-9A-Fa-f]{2,})>\s*Tj"))
        {
            try { sb.AppendLine(HexToAscii(m.Groups[1].Value)); } catch { /* skip */ }
        }

        return sb.ToString();
    }

    private static string HexToAscii(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string SanitisePdfString(string s)
    {
        s = Regex.Replace(s, @"\\[0-7]{1,3}", m =>
        {
            try { return ((char)Convert.ToInt32(m.Value[1..], 8)).ToString(); } catch { return " "; }
        });
        s = Regex.Replace(s, @"\\.", " ");
        return new string(s.Where(c => c is >= ' ' and <= '~' or >= '\u00A0').ToArray());
    }

    private static string NormaliseWhitespace(string text)
        => Regex.Replace(text.Replace('\r', '\n'), @"[ \t]+", " ").Replace("\n ", "\n").Trim();

    public ParsedReceiptData ParseText(string text)
    {
        var result = new ParsedReceiptData();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lower = text.ToLowerInvariant();

        result.TransportMode = DetectMode(lower);

        if (IsTrainDocument(lower, result.TransportMode))
            ParseTrain(text, lower, result);
        else if (IsTaxiDocument(lower, result.TransportMode))
            ParseTaxi(text, result);
        else if (IsFlightDocument(lower, result.TransportMode))
            ParseFlight(text, lower, result);
        else if (IsCoachDocument(lower, result.TransportMode))
            ParseCoach(text, result);
        else
            ParseGeneric(text, result);

        TryExtractRoute(text, result);
        result.TripDate ??= ExtractDate(text);
        result.TravellerName ??= ExtractName(text);
        result.Passengers ??= ExtractPassengers(text);

        return result;
    }

    private static bool HasUsefulData(ParsedReceiptData r)
        => r.TravellerName != null || r.Origin != null || r.Destination != null
           || r.TripDate != null || r.TransportMode != null;

    private static bool IsTrainDocument(string lower, string? mode)
        => mode is "Train-National" or "Train-International"
           || lower.Contains("trainline") || lower.Contains("lner") || lower.Contains("avanti")
           || lower.Contains("great western") || lower.Contains("national rail")
           || lower.Contains("heathrow express") || lower.Contains("eurostar") || lower.Contains("tfl")
           || lower.Contains("railcard") || lower.Contains("virgin trains") || lower.Contains("crosscountry")
           || lower.Contains("northern rail");

    private static bool IsTaxiDocument(string lower, string? mode)
        => mode is "Taxi-Regular" or "Taxi-Electric"
           || lower.Contains("uber") || lower.Contains("bolt") || lower.Contains("addison lee");

    private static bool IsFlightDocument(string lower, string? mode)
        => mode?.StartsWith("Flight") == true
           || lower.Contains("boarding pass") || lower.Contains("flight") || lower.Contains("airline")
           || lower.Contains("ryanair") || lower.Contains("easyjet") || lower.Contains("british airways")
           || lower.Contains("lufthansa") || lower.Contains("jet2") || lower.Contains("wizz air");

    private static bool IsCoachDocument(string lower, string? mode)
        => mode == "Coach-National"
           || lower.Contains("national express") || lower.Contains("megabus") || lower.Contains("flixbus");

    private static string? DetectMode(string lower)
    {
        if (lower.Contains("uber") || lower.Contains("bolt") || lower.Contains("taxi") || lower.Contains("addison lee"))
            return "Taxi-Regular";
        if (lower.Contains("eurostar"))
            return "Train-International";
        if (lower.Contains("trainline") || lower.Contains("lner") || lower.Contains("avanti")
            || lower.Contains("national rail") || lower.Contains("tfl") || lower.Contains(" train ")
            || lower.Contains("station") || lower.Contains("platform") || lower.Contains("railway"))
            return "Train-National";
        if (lower.Contains("ryanair") || lower.Contains("easyjet") || lower.Contains("british airways")
            || lower.Contains("flight") || lower.Contains("airline") || lower.Contains("boarding pass")
            || lower.Contains("jet2") || lower.Contains("wizz air") || lower.Contains("lufthansa"))
            return "Flight-ShortHaul";
        if (lower.Contains("national express") || lower.Contains("megabus") || lower.Contains("coach") || lower.Contains("flixbus"))
            return "Coach-National";
        if (lower.Contains("ferry") || lower.Contains("p&o") || lower.Contains("brittany ferries"))
            return "Ferry-Foot";
        return null;
    }

    private static void TryExtractRoute(string text, ParsedReceiptData r)
    {
        if (r.Origin != null && r.Destination != null) return;

        var patterns = new[]
        {
            // London Paddington to Bristol Temple Meads
            @"([A-Za-z][A-Za-z0-9 &'\-\.]{2,40}?)\s+to\s+([A-Za-z][A-Za-z0-9 &'\-\.]{2,40}?)(?:\s{2,}|\n|$|,|\.|:)",
            // Kings Cross → Edinburgh / Manchester - Leeds
            @"([A-Za-z][A-Za-z0-9 &'\-\.]{2,40}?)\s*[→–—\-]\s*([A-Za-z][A-Za-z0-9 &'\-\.]{2,40}?)(?:\s{2,}|\n|$)",
            // From: X  To: Y
            @"(?i)(?:from|origin|depart(?:ure)?(?:\s+station)?)[:\s]+(.+?)\s+(?:to|destination|arriv(?:al)?(?:\s+station)?)[:\s]+(.+?)(?:\n|$)",
            // LHR → CDG
            @"\b([A-Z]{3})\s*[→–\-]\s*([A-Z]{3})\b",
            // London Gatwick (LGW) to Barcelona (BCN)
            @"([A-Za-z][A-Za-z\s]{2,30}?)\s*\([A-Z]{3}\)\s*(?:to|[→–\-])\s*([A-Za-z][A-Za-z\s]{2,30}?)\s*\([A-Z]{3}\)",
            // Outbound: Manchester Piccadilly - London Euston
            @"(?i)(?:outbound|inbound|journey|route)[:\s]+(.+?)\s*(?:to|\-|–|—)\s*(.+?)(?:\n|$)",
        };

        foreach (var pattern in patterns)
        {
            var m = Regex.Match(text, pattern, RegexOptions.Multiline);
            if (!m.Success) continue;
            var o = CleanLocation(m.Groups[1].Value);
            var d = CleanLocation(m.Groups[2].Value);
            if (IsPlausibleLocation(o) && IsPlausibleLocation(d))
            {
                if (r.Origin == null) r.Origin = o;
                if (r.Destination == null) r.Destination = d;
                return;
            }
        }
    }

    private static bool IsPlausibleLocation(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 2 || s.Length > 60) return false;
        var lower = s.ToLowerInvariant();
        string[] reject =
        {
            "ticket", "receipt", "invoice", "booking", "reference", "passenger", "class",
            "standard", "date", "total", "amount", "payment", "card", "vat"
        };
        return !reject.Any(r => lower.Contains(r));
    }

    private static void ParseTrain(string text, string lower, ParsedReceiptData r)
    {
        ParseTrainlineItemLine(text, r);
        TryExtractRoute(text, r);

        if (lower.Contains("eurostar"))
            r.TransportMode = "Train-International";
        else
            r.TransportMode ??= "Train-National";

        if (Regex.IsMatch(text, @"\bfirst\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "First";
        else if (Regex.IsMatch(text, @"\b(?:standard|economy)\s*class\b|\bstandard\b", RegexOptions.IgnoreCase)) r.TravelClass = "Standard";
    }

    private static void ParseTaxi(string text, ParsedReceiptData r)
    {
        var m = Regex.Match(text, @"(?i)trip\s+from\s+(.+?)\s+to\s+(.+?)(?:\n|$)", RegexOptions.Multiline);
        if (m.Success)
        {
            r.Origin = CleanLocation(m.Groups[1].Value);
            r.Destination = CleanLocation(m.Groups[2].Value);
        }

        var pu = Regex.Match(text, @"(?i)pick[- ]?up[:\s]+(.+?)(?:\n|drop|$)");
        var do_ = Regex.Match(text, @"(?i)drop[- ]?(?:off| location)?[:\s]+(.+?)(?:\n|$)");
        if (pu.Success && r.Origin == null) r.Origin = CleanLocation(pu.Groups[1].Value);
        if (do_.Success && r.Destination == null) r.Destination = CleanLocation(do_.Groups[1].Value);

        TryExtractRoute(text, r);
        r.TransportMode ??= "Taxi-Regular";
        r.Passengers ??= 1;
    }

    private static void ParseFlight(string text, string lower, ParsedReceiptData r)
    {
        TryExtractRoute(text, r);

        if (Regex.IsMatch(text, @"\bbusiness\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "Business";
        else if (Regex.IsMatch(text, @"\bfirst\s*class\b", RegexOptions.IgnoreCase)) r.TravelClass = "First";
        else r.TravelClass ??= "Economy";

        if (lower.Contains("domestic") || lower.Contains("internal"))
            r.TransportMode = "Flight-Domestic";
        else if (lower.Contains("long haul") || lower.Contains("longhaul")
                 || lower.Contains("new york") || lower.Contains("dubai") || lower.Contains("singapore")
                 || lower.Contains("usa") || lower.Contains("united states") || lower.Contains("australia"))
            r.TransportMode = "Flight-LongHaul";
        else
            r.TransportMode ??= "Flight-ShortHaul";
    }

    private static void ParseCoach(string text, ParsedReceiptData r)
    {
        TryExtractRoute(text, r);
        r.TransportMode = "Coach-National";
    }

    private static void ParseGeneric(string text, ParsedReceiptData r)
    {
        TryExtractRoute(text, r);

        var m = Regex.Match(text,
            @"(?i)(?:from|origin|departure)[:\s]+(.+?)(?:\s+(?:to|destination|arrival)[:\s]+(.+?))?(?:\n|$)",
            RegexOptions.Multiline);
        if (m.Success)
        {
            if (r.Origin == null && m.Groups[1].Success) r.Origin = CleanLocation(m.Groups[1].Value);
            if (r.Destination == null && m.Groups[2].Success) r.Destination = CleanLocation(m.Groups[2].Value);
        }
    }

    private static string? ExtractDate(string text)
    {
        // Mon 25 June 2025 / Monday, 25 Jun 2025
        var m = Regex.Match(text,
            @"(?i)(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*,?\s*(\d{1,2})\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s*,?\s+(\d{4})");
        if (TryBuildDate(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, out var d1)) return d1;

        // 25 June 2025 / 1 Mar, 2026
        m = Regex.Match(text,
            @"\b(\d{1,2})\s+(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s*,?\s+(\d{4})(?=\d?\s*x\s*[aA]|\D|$)\b",
            RegexOptions.IgnoreCase);
        if (TryBuildDate(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, out var d2)) return d2;

        // Date of travel / departure date
        m = Regex.Match(text,
            @"(?i)(?:date of travel|travel date|departure date|journey date|outbound date)[:\s]+(\d{1,2})[\./\-](\d{1,2})[\./\-](\d{2,4})");
        if (m.Success && TryBuildDateFlexible(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, out var d3)) return d3;

        // 25/06/2025 or 25-06-2025 or 25/6/25
        m = Regex.Match(text, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{2,4})\b");
        if (m.Success && TryBuildDateFlexible(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, out var d4)) return d4;

        // ISO 2025-06-25
        m = Regex.Match(text, @"\b(20\d{2})[/\-](\d{2})[/\-](\d{2})\b");
        if (m.Success && DateTime.TryParse($"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}", out var d5))
            return d5.ToString("yyyy-MM-dd");

        return null;
    }

    private static bool TryBuildDate(string dayStr, string monthStr, string yearStr, out string? iso)
    {
        iso = null;
        if (!int.TryParse(dayStr, out int day) || !int.TryParse(yearStr, out int year)) return false;
        int month = MonthNum(monthStr);
        if (month <= 0 || !DateTime.TryParse($"{year}-{month:D2}-{day:D2}", out var d)) return false;
        iso = d.ToString("yyyy-MM-dd");
        return true;
    }

    private static bool TryBuildDateFlexible(string a, string b, string c, out string? iso)
    {
        iso = null;
        if (!int.TryParse(a, out int n1) || !int.TryParse(b, out int n2)) return false;
        int year = int.Parse(c.Length == 2 ? $"20{c}" : c);

        // Try UK order d/m/y then m/d/y
        if (DateTime.TryParse($"{year}-{n2:D2}-{n1:D2}", out var uk) && uk.Month == n2 && uk.Day == n1)
        { iso = uk.ToString("yyyy-MM-dd"); return true; }
        if (DateTime.TryParse($"{year}-{n1:D2}-{n2:D2}", out var alt) && alt.Month == n1 && alt.Day == n2)
        { iso = alt.ToString("yyyy-MM-dd"); return true; }
        return false;
    }

    private static string? ExtractName(string text)
    {
        var patterns = new[]
        {
            @"(?i)passenger(?:\s+name)?[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+){1,3})",
            @"(?i)(?:lead\s+)?(?:travell?er|ticket\s+holder)(?:\s+name)?[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+){1,3})",
            @"(?i)name[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+){1,3})",
            @"(?i)booked\s+for[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+){1,3})",
            @"(?i)ticket\s+(?:holder|for)[:\s]+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+){1,3})",
            @"(?i)dear\s+([A-Za-z][A-Za-z'\-]+(?:\s+[A-Za-z][A-Za-z'\-]+)?)",
        };
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p);
            if (!m.Success) continue;
            var name = ToTitleCase(Clean(m.Groups[1].Value));
            if (name.Length is > 3 and < 60 && !IsPlausibleLocation(name)) return name;
        }

        var caps = Regex.Match(text, @"\b(MR|MRS|MS|DR|MISS)\.?\s+([A-Z][A-Z'\-]+(?:\s+[A-Z][A-Z'\-]+){1,2})\b");
        if (caps.Success) return ToTitleCase(caps.Groups[2].Value);

        return null;
    }

    private static int? ExtractPassengers(string text)
    {
        var m = Regex.Match(text, @"(?<![0-9])(\d{1,2})\s*x\s*adults?", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int px) && px is >= 1 and <= 50) return px;

        m = Regex.Match(text, @"(\d+)\s*(?:adult|passenger|traveller|ticket)s?", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int p) && p is >= 1 and <= 50) return p;
        return null;
    }

    private static void ParseTrainlineItemLine(string text, ParsedReceiptData r)
    {
        const string datePart =
            @"(\d{1,2})\s+(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s*,?\s*(\d{4})(?=\d?\s*x\s*[aA]|\D|$)";

        var patterns = new[]
        {
            $@"(?i)(.+?)\s+rail\s+to\s+(.+?)\s*:\s*{datePart}",
            $@"(?i)(.+?)\s+to\s+(.+?)\s*:\s*{datePart}",
        };

        foreach (var pattern in patterns)
        {
            var m = Regex.Match(text, pattern);
            if (!m.Success) continue;

            var o = CleanLocation(m.Groups[1].Value);
            var d = CleanLocation(m.Groups[2].Value);
            if (IsPlausibleLocation(o)) r.Origin ??= o;
            if (IsPlausibleLocation(d)) r.Destination ??= d;
            if (TryBuildDate(m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, out var iso))
                r.TripDate ??= iso;

            var tail = text[(m.Index + m.Length)..];
            var paxM = Regex.Match(tail, @"(?i)^(\d{1,2})\s*x\s*adults?");
            if (paxM.Success && int.TryParse(paxM.Groups[1].Value, out int px) && px is >= 1 and <= 50)
                r.Passengers ??= px;
            return;
        }
    }

    private static void ApplyFilenameHints(string? fileName, ParsedReceiptData r)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        stem = Regex.Replace(stem, @"\s*\(\d+\)$", "").Trim();
        var lower = stem.ToLowerInvariant();

        // tickets_for_L_Grieve___A_Wilczynski / hotel_for_Adam_Wilczynski
        var forM = Regex.Match(stem, @"(?i)(?:hotel|tickets)_for_(.+?)(?:_re_trip_to_|_re_|_-_)");
        if (forM.Success)
        {
            var raw = forM.Groups[1].Value.Replace("___", " & ").Replace('_', ' ');
            r.TravellerName ??= ToTitleCase(Clean(raw));
        }

        // Heathrow_to_Paddington (skip for hotel receipts — "re_trip_to_Boston" is not a route)
        if (!lower.Contains("hotel"))
        {
            var routeM = Regex.Match(stem, @"(?i)(?:^|_)([A-Za-z][A-Za-z_]{1,40}?)_to_([A-Za-z][A-Za-z_]{1,40}?)(?:_re_|_-_|_\d|$)");
            if (routeM.Success)
            {
                r.Origin ??= ToTitleCase(routeM.Groups[1].Value.Replace('_', ' '));
                r.Destination ??= ToTitleCase(routeM.Groups[2].Value.Replace('_', ' '));
            }
        }

        // re_trip_to_Boston — destination only when no route was parsed above
        var tripM = Regex.Match(stem, @"(?i)re_trip_to_([A-Za-z_]+)");
        if (tripM.Success && r.Destination == null)
            r.Destination ??= ToTitleCase(tripM.Groups[1].Value.Replace('_', ' '));

        // 15-28_February_2026 (use check-in / start date)
        var rangeM = Regex.Match(stem,
            @"(?i)(\d{1,2})-(\d{1,2})_(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)_(\d{4})");
        if (rangeM.Success && TryBuildDate(rangeM.Groups[1].Value, rangeM.Groups[3].Value, rangeM.Groups[4].Value, out var rangeStart))
            r.TripDate ??= rangeStart;

        // trailing _1_March_20 or _1_March_2026
        var dateM = Regex.Match(stem,
            @"(?i)_(\d{1,2})_(jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)_(\d{2,4})$");
        if (dateM.Success)
        {
            var year = dateM.Groups[3].Value;
            if (year.Length == 2) year = "20" + year;
            if (TryBuildDate(dateM.Groups[1].Value, dateM.Groups[2].Value, year, out var single))
                r.TripDate ??= single;
        }

        if (lower.Contains("trainline") || lower.Contains("heathrow express") || lower.Contains("national rail"))
            r.TransportMode ??= "Train-National";
        if (lower.Contains("expedia") && lower.Contains("hotel"))
        {
            r.ExtractionNote = HasUsefulData(r)
                ? "Hotel receipt (scanned PDF) — name and dates filled from filename. Enter transport mode and route manually."
                : "Hotel receipt — scanned PDF with no readable text. Enter trip details manually.";
        }
        else if (HasUsefulData(r))
            r.ExtractionNote = null;
    }

    private static int MonthNum(string s) => s.ToLowerInvariant() switch
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

    private static string CleanLocation(string s)
    {
        s = Clean(s.Trim(' ', ',', '.', ';', ':'));
        // Drop trailing time fragments e.g. "London 09:30"
        s = Regex.Replace(s, @"\s+\d{1,2}:\d{2}(?::\d{2})?\s*$", "");
        return s;
    }

    private static string ToTitleCase(string s) =>
        string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
}

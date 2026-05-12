using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RittalTravel.Data;
using RittalTravel.Models;

namespace RittalTravel.Controllers;

[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly ILogger<ReportController> _logger;

    public ReportController(RittalTravelContext db, ILogger<ReportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Generate()
    {
        try
        {
            var trips = await _db.Trips
                .Where(t => t.OrganisationId == 1)
                .OrderByDescending(t => t.TripDate)
                .ToListAsync();

            _logger.LogInformation("Generating PDF report with {Count} trips", trips.Count);

            var pdfBytes = GeneratePdf(trips);

            string filename = $"RittalTravel-Audit-Report-{DateTime.Now:yyyy-MM-dd}.pdf";
            _logger.LogInformation("PDF report generated: {Filename}", filename);

            return File(pdfBytes, "application/pdf", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation failed");
            TempData["Error"] = "PDF generation failed. Please try again.";
            return RedirectToAction("Index", "Dashboard");
        }
    }

    private static byte[] GeneratePdf(List<Trip> trips)
    {
        var now = DateTime.Now;
        int currentYear = now.Year;
        var yearTrips = trips.Where(t => t.TripDate.Year == currentYear).ToList();

        double totalTco2e  = trips.Sum(t => t.KgCO2e) / 1000.0;
        double flightTco2e = trips.Where(t => t.TransportMode.StartsWith("Flight")).Sum(t => t.KgCO2e) / 1000.0;
        double railTco2e   = trips.Where(t => t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0;
        double roadTco2e   = trips.Where(t => !t.TransportMode.StartsWith("Flight") && !t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0;

        // Navy / red brand colours
        var navy  = Color.FromHex("#0D1B2A");
        var red   = Color.FromHex("#CC0000");
        var white = Colors.White;
        var grey  = Color.FromHex("#8B949E");
        var lightGrey = Color.FromHex("#E8ECF0");

        var doc = Document.Create(container =>
        {
            // ── Cover Page ──────────────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);

                page.Content().Background(navy).Padding(50).Column(col =>
                {
                    col.Item().PaddingTop(60).Text("Rittal Travel")
                        .FontSize(42).Bold().FontColor(white);

                    col.Item().PaddingTop(8).Text("Business Travel Carbon Reporting")
                        .FontSize(18).FontColor(red);

                    col.Item().PaddingTop(4).Text("Rittal UK")
                        .FontSize(14).FontColor(grey);

                    col.Item().PaddingTop(60).LineHorizontal(1).LineColor(red);

                    col.Item().PaddingTop(24).Text("Scope 3 Category 6 Business Travel Audit Report")
                        .FontSize(16).Bold().FontColor(white);

                    col.Item().PaddingTop(8).Text($"Reporting Period: All trips through {now:dd MMMM yyyy}")
                        .FontSize(12).FontColor(grey);

                    col.Item().PaddingTop(4).Text($"Generated: {now:dd MMMM yyyy HH:mm} UTC")
                        .FontSize(12).FontColor(grey);

                    col.Item().PaddingTop(4).Text($"Total Trips in Report: {trips.Count}")
                        .FontSize(12).FontColor(grey);

                    col.Item().PaddingTop(160).Text(
                        "Prepared using Rittal Travel — DEFRA 2025 GHG Conversion Factors — " +
                        "GHG Protocol Scope 3 Category 6 — Rittal UK Internal System")
                        .FontSize(9).FontColor(grey).Italic();
                });
            });

            // ── Summary Page ─────────────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header().Background(navy).Padding(16).Row(row =>
                {
                    row.RelativeItem().Text("Rittal Travel — Audit Report Summary").FontColor(white).Bold().FontSize(14);
                    row.AutoItem().Text($"DEFRA 2025").FontColor(red).Bold();
                });

                page.Content().PaddingTop(24).Column(col =>
                {
                    // Big number
                    col.Item().Background(navy).Padding(20).AlignCenter().Column(inner =>
                    {
                        inner.Item().Text($"{totalTco2e:F3}").FontSize(48).Bold().FontColor(white).AlignCenter();
                        inner.Item().Text("Total tCO2e — All Trips").FontSize(12).FontColor(grey).AlignCenter();
                    });

                    col.Item().PaddingTop(24).Text("Emissions Breakdown").FontSize(14).Bold();
                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        // Header
                        t.Header(h =>
                        {
                            h.Cell().Background(navy).Padding(8).Text("Category").FontColor(white).Bold();
                            h.Cell().Background(navy).Padding(8).Text("tCO2e").FontColor(white).Bold();
                            h.Cell().Background(navy).Padding(8).Text("% of Total").FontColor(white).Bold();
                        });

                        void Row(string label, double val, uint rowIdx)
                        {
                            var bg = rowIdx % 2 == 0 ? white : lightGrey;
                            double pct = totalTco2e > 0 ? val / totalTco2e * 100 : 0;
                            t.Cell().Background(bg).Padding(6).Text(label);
                            t.Cell().Background(bg).Padding(6).Text($"{val:F3}");
                            t.Cell().Background(bg).Padding(6).Text($"{pct:F1}%");
                        }

                        Row("Flights (Scope 3 Cat. 6)", flightTco2e, 0);
                        Row("Rail",                      railTco2e,   1);
                        Row("Road / Other",               roadTco2e,   2);
                        Row("Total",                      totalTco2e,  3);
                    });

                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Total Trips: {trips.Count}").FontSize(11);
                            c.Item().Text($"Unique Travellers: {trips.Select(t => t.TravellerName).Distinct().Count()}").FontSize(11);
                            c.Item().Text($"Report Generated: {now:dd MMM yyyy HH:mm}").FontSize(11);
                        });

                        row.AutoItem().Background(navy).Padding(12).Column(c =>
                        {
                            c.Item().Text("DEFRA 2025 VERIFIED").FontColor(white).Bold().FontSize(10);
                            c.Item().Text("GHG PROTOCOL ALIGNED").FontColor(grey).FontSize(9);
                            c.Item().Text("SCOPE 3 CATEGORY 6").FontColor(grey).FontSize(9);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Rittal Travel — DEFRA 2025 GHG Conversion Factors — GHG Protocol Scope 3 Category 6 — Rittal UK Internal System")
                     .FontSize(8).FontColor(grey);
                });
            });

            // ── Formula Trail Appendix ────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);

                page.Header().Background(navy).Padding(12).Row(row =>
                {
                    row.RelativeItem().Text("Appendix A — Full Formula Trail (All Trips, No Aggregation)").FontColor(white).Bold().FontSize(12);
                    row.AutoItem().Text("DEFRA 2025").FontColor(red).Bold();
                });

                page.Content().PaddingTop(16).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(58);  // Date
                        c.RelativeColumn(2);   // Traveller
                        c.RelativeColumn(2);   // Origin
                        c.RelativeColumn(2);   // Destination
                        c.RelativeColumn(1.5f);// Mode
                        c.ConstantColumn(55);  // Dist km
                        c.ConstantColumn(55);  // Factor
                        c.RelativeColumn(4);   // Formula
                        c.ConstantColumn(55);  // kgCO2e
                        c.RelativeColumn(1.5f);// Methodology
                    });

                    t.Header(h =>
                    {
                        var cols = new[] { "Date", "Traveller", "Origin", "Destination", "Mode", "Dist km", "Factor", "Formula", "kgCO2e", "Methodology" };
                        foreach (var c in cols)
                            h.Cell().Background(navy).Padding(4).Text(c).FontColor(white).Bold().FontSize(8);
                    });

                    uint idx = 0;
                    foreach (var trip in trips)
                    {
                        var bg = idx++ % 2 == 0 ? white : lightGrey;
                        void Cell(string text, bool mono = false)
                        {
                            var cell = t.Cell().Background(bg).Padding(3);
                            if (mono)
                                cell.Text(text).FontSize(6.5f).FontFamily("Courier New");
                            else
                                cell.Text(text).FontSize(7.5f);
                        }

                        Cell(trip.TripDate.ToString("dd/MM/yy"));
                        Cell(trip.TravellerName);
                        Cell(trip.Origin);
                        Cell(trip.Destination);
                        Cell(trip.TransportMode);
                        Cell($"{trip.DistanceKm:F1}");
                        Cell($"{trip.EmissionFactor:F5}");
                        Cell(trip.Formula, mono: true);
                        Cell($"{trip.KgCO2e:F3}");
                        Cell(trip.DistanceMethodology);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Rittal Travel — DEFRA 2025 GHG Conversion Factors — GHG Protocol Scope 3 Category 6 — Rittal UK Internal System")
                     .FontSize(7).FontColor(grey);
                    t.Span("  |  Page ").FontSize(7).FontColor(grey);
                    t.CurrentPageNumber().FontSize(7).FontColor(grey);
                    t.Span(" of ").FontSize(7).FontColor(grey);
                    t.TotalPages().FontSize(7).FontColor(grey);
                });
            });

            // ── Methodology Statement ─────────────────────────────────────
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);

                page.Header().Background(navy).Padding(16)
                    .Text("Methodology Statement").FontColor(white).Bold().FontSize(14);

                page.Content().PaddingTop(24).Column(col =>
                {
                    col.Item().Text("Distance Calculation").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(
                        "Distance calculations for air travel use the Haversine Great Circle formula as specified by " +
                        "DEFRA guidance. Road and rail distances use Google Maps Distance Matrix API for actual route distance.");

                    col.Item().PaddingTop(16).Text("Emission Factors").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(
                        "All emission factors sourced from DEFRA 2025 GHG Conversion Factor tables, Table 5: Business Travel. " +
                        "Factors are expressed as kgCO2e per passenger-km. Flight class multipliers applied as per DEFRA guidance " +
                        "(Economy ×1.0, Business ×2.0, First ×2.4).");

                    col.Item().PaddingTop(16).Text("Standards Alignment").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(
                        "Calculations align with GHG Protocol Corporate Standard, Scope 3 Category 6 (Business Travel). " +
                        "This report supports SECR (Streamlined Energy and Carbon Reporting) compliance.");

                    col.Item().PaddingTop(16).Text("Disclaimer").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text(
                        "This report was generated by Rittal Travel and has not been independently verified. " +
                        "Organisations seeking formal carbon disclosure should have this data independently assured.")
                        .Italic();

                    col.Item().PaddingTop(24).LineHorizontal(1).LineColor(red);
                    col.Item().PaddingTop(8).Text(
                        $"Generated: {now:dd MMMM yyyy HH:mm} UTC  |  " +
                        "Rittal Travel — Rittal UK Internal System  |  " +
                        "DEFRA 2025 GHG Conversion Factors")
                        .FontSize(9).FontColor(grey);
                });
            });
        });

        return doc.GeneratePdf();
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RittalTravel.Data;
using RittalTravel.Models;
using RittalTravel.Services;

namespace RittalTravel.Controllers;

public class ReportController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly ILogger<ReportController> _logger;
    private readonly RittalTravelOptions _options;

    public ReportController(RittalTravelContext db, ILogger<ReportController> logger,
        IOptions<RittalTravelOptions> options)
    {
        _db = db; _logger = logger;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Generate()
    {
        try
        {
            var trips = await _db.Trips.Where(t => t.OrganisationId == _options.OrganisationId)
                .OrderByDescending(t => t.TripDate).ToListAsync();
            _logger.LogInformation("Generating PDF with {Count} trips", trips.Count);
            var pdf = GeneratePdf(trips);
            return File(pdf, "application/pdf", $"RittalTravel-Audit-Report-{DateTime.Now:yyyy-MM-dd}.pdf");
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
        double totalTco2e  = trips.Sum(t => t.KgCO2e) / 1000.0;
        double flightTco2e = trips.Where(t => t.TransportMode.StartsWith("Flight")).Sum(t => t.KgCO2e) / 1000.0;
        double railTco2e   = trips.Where(t => t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0;
        double roadTco2e   = trips.Where(t => !t.TransportMode.StartsWith("Flight") && !t.TransportMode.StartsWith("Train")).Sum(t => t.KgCO2e) / 1000.0;
        var navy = Color.FromHex("#0f1a0f"); var red = Color.FromHex("#5C7A5A");
        var white = Colors.White; var grey = Color.FromHex("#8B949E"); var lgrey = Color.FromHex("#E8ECF0");

        return Document.Create(c =>
        {
            // Cover
            c.Page(p => {
                p.Size(PageSizes.A4); p.Margin(0);
                p.Content().Background(navy).Padding(50).Column(col => {
                    col.Item().PaddingTop(60).Text("Rittal Travel").FontSize(42).Bold().FontColor(white);
                    col.Item().PaddingTop(8).Text("Business Travel Carbon Reporting").FontSize(18).FontColor(red);
                    col.Item().PaddingTop(4).Text("Rittal UK").FontSize(14).FontColor(grey);
                    col.Item().PaddingTop(60).LineHorizontal(1).LineColor(red);
                    col.Item().PaddingTop(24).Text("Scope 3 Category 6 Business Travel Audit Report").FontSize(16).Bold().FontColor(white);
                    col.Item().PaddingTop(8).Text($"Reporting Period: All trips through {now:dd MMMM yyyy}").FontSize(12).FontColor(grey);
                    col.Item().PaddingTop(4).Text($"Generated: {now:dd MMMM yyyy HH:mm} UTC").FontSize(12).FontColor(grey);
                    col.Item().PaddingTop(4).Text($"Total Trips in Report: {trips.Count}").FontSize(12).FontColor(grey);
                    col.Item().PaddingTop(160).Text("Prepared using Rittal Travel — DEFRA 2025 GHG Conversion Factors — GHG Protocol Scope 3 Category 6 — Rittal UK Internal System").FontSize(9).FontColor(grey).Italic();
                });
            });
            // Summary
            c.Page(p => {
                p.Size(PageSizes.A4); p.Margin(40);
                p.Header().Background(navy).Padding(16).Row(r => { r.RelativeItem().Text("Rittal Travel — Audit Report Summary").FontColor(white).Bold().FontSize(14); r.AutoItem().Text("DEFRA 2025").FontColor(red).Bold(); });
                p.Content().PaddingTop(24).Column(col => {
                    col.Item().Background(navy).Padding(20).AlignCenter().Column(i => { i.Item().Text($"{totalTco2e:F3}").FontSize(48).Bold().FontColor(white).AlignCenter(); i.Item().Text("Total tCO2e — All Trips").FontSize(12).FontColor(grey).AlignCenter(); });
                    col.Item().PaddingTop(24).Text("Emissions Breakdown").FontSize(14).Bold();
                    col.Item().PaddingTop(8).Table(t => {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                        t.Header(h => { foreach (var hd in new[]{"Category","tCO2e","% of Total"}) h.Cell().Background(navy).Padding(8).Text(hd).FontColor(white).Bold(); });
                        void TR(string l, double v, uint i) { var bg = i%2==0?white:lgrey; double pct=totalTco2e>0?v/totalTco2e*100:0; t.Cell().Background(bg).Padding(6).Text(l); t.Cell().Background(bg).Padding(6).Text($"{v:F3}"); t.Cell().Background(bg).Padding(6).Text($"{pct:F1}%"); }
                        TR("Flights (Scope 3 Cat. 6)",flightTco2e,0); TR("Rail",railTco2e,1); TR("Road / Other",roadTco2e,2); TR("Total",totalTco2e,3);
                    });
                    col.Item().PaddingTop(16).Row(r => {
                        r.RelativeItem().Column(col2 => { col2.Item().Text($"Total Trips: {trips.Count}").FontSize(11); col2.Item().Text($"Unique Travellers: {trips.Select(t=>t.TravellerName).Distinct().Count()}").FontSize(11); });
                        r.AutoItem().Background(navy).Padding(12).Column(col2 => { col2.Item().Text("DEFRA 2025 VERIFIED").FontColor(white).Bold().FontSize(10); col2.Item().Text("GHG PROTOCOL ALIGNED").FontColor(grey).FontSize(9); col2.Item().Text("SCOPE 3 CATEGORY 6").FontColor(grey).FontSize(9); });
                    });
                });
                p.Footer().AlignCenter().Text(t => t.Span("Rittal Travel — DEFRA 2025 GHG Conversion Factors — GHG Protocol Scope 3 Category 6 — Rittal UK Internal System").FontSize(8).FontColor(grey));
            });
            // Formula Trail
            c.Page(p => {
                p.Size(PageSizes.A4.Landscape()); p.Margin(30);
                p.Header().Background(navy).Padding(12).Row(r => { r.RelativeItem().Text("Appendix A — Full Formula Trail (All Trips, No Aggregation)").FontColor(white).Bold().FontSize(12); r.AutoItem().Text("DEFRA 2025").FontColor(red).Bold(); });
                p.Content().PaddingTop(16).Table(t => {
                    t.ColumnsDefinition(cd => { cd.ConstantColumn(58); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(1.5f); cd.ConstantColumn(55); cd.ConstantColumn(55); cd.RelativeColumn(4); cd.ConstantColumn(55); cd.RelativeColumn(1.5f); });
                    t.Header(h => { foreach (var hd in new[]{"Date","Traveller","Origin","Destination","Mode","Dist km","Factor","Formula","kgCO2e","Methodology"}) h.Cell().Background(navy).Padding(4).Text(hd).FontColor(white).Bold().FontSize(8); });
                    uint idx=0;
                    foreach (var trip in trips) {
                        var bg=idx++%2==0?white:lgrey;
                        void Cell(string text, bool mono=false) { var cell=t.Cell().Background(bg).Padding(3); if(mono) cell.Text(text).FontSize(6.5f).FontFamily("Courier New"); else cell.Text(text).FontSize(7.5f); }
                        Cell(trip.TripDate.ToString("dd/MM/yy")); Cell(trip.TravellerName); Cell(trip.Origin); Cell(trip.Destination);
                        Cell(trip.TransportMode); Cell($"{trip.DistanceKm:F1}"); Cell($"{trip.EmissionFactor:F5}");
                        Cell(trip.Formula,mono:true); Cell($"{trip.KgCO2e:F3}"); Cell(trip.DistanceMethodology);
                    }
                });
                p.Footer().AlignCenter().Text(t => { t.Span("Rittal Travel — DEFRA 2025 — Rittal UK Internal System  |  Page ").FontSize(7).FontColor(grey); t.CurrentPageNumber().FontSize(7).FontColor(grey); t.Span(" of ").FontSize(7).FontColor(grey); t.TotalPages().FontSize(7).FontColor(grey); });
            });
            // Methodology
            c.Page(p => {
                p.Size(PageSizes.A4); p.Margin(50);
                p.Header().Background(navy).Padding(16).Text("Methodology Statement").FontColor(white).Bold().FontSize(14);
                p.Content().PaddingTop(24).Column(col => {
                    col.Item().Text("Distance Calculation").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text("Distance calculations for air travel use the Haversine Great Circle formula as specified by DEFRA guidance. Road and rail distances use Google Maps Distance Matrix API for actual route distance.");
                    col.Item().PaddingTop(16).Text("Emission Factors").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text("All emission factors sourced from DEFRA 2025 GHG Conversion Factor tables, Table 5: Business Travel. Factors are expressed as kgCO2e per passenger-km. Flight class multipliers: Economy ×1.0, Business ×2.0, First ×2.4.");
                    col.Item().PaddingTop(16).Text("Standards Alignment").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text("Calculations align with GHG Protocol Corporate Standard, Scope 3 Category 6 (Business Travel). Supports SECR compliance.");
                    col.Item().PaddingTop(16).Text("Disclaimer").Bold().FontSize(12);
                    col.Item().PaddingTop(8).Text("This report was generated by Rittal Travel and has not been independently verified.").Italic();
                    col.Item().PaddingTop(24).LineHorizontal(1).LineColor(red);
                    col.Item().PaddingTop(8).Text($"Generated: {now:dd MMMM yyyy HH:mm} UTC  |  Rittal Travel — Rittal UK Internal System  |  DEFRA 2025").FontSize(9).FontColor(grey);
                });
            });
        }).GeneratePdf();
    }
}

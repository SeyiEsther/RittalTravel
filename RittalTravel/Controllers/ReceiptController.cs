using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RittalTravel.Data;
using RittalTravel.Models;
using RittalTravel.Services;

namespace RittalTravel.Controllers;

public class ReceiptController : Controller
{
    private readonly RittalTravelContext _db;
    private readonly ReceiptParserService _parser;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ReceiptController> _logger;

    public ReceiptController(RittalTravelContext db, ReceiptParserService parser,
        IWebHostEnvironment env, ILogger<ReceiptController> logger)
    {
        _db = db; _parser = parser; _env = env; _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Receipts.Where(r => r.OrganisationId == 1).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(r =>
                (r.TravellerName != null && r.TravellerName.ToLower().Contains(s)) ||
                (r.Origin != null && r.Origin.ToLower().Contains(s)) ||
                (r.Destination != null && r.Destination.ToLower().Contains(s)) ||
                (r.OriginalFileName.ToLower().Contains(s)) ||
                (r.TransportMode != null && r.TransportMode.ToLower().Contains(s)) ||
                (r.DocumentType != null && r.DocumentType.ToLower().Contains(s)) ||
                (r.Notes != null && r.Notes.ToLower().Contains(s)));
        }

        var receipts = await query.OrderByDescending(r => r.UploadedAt).ToListAsync();
        ViewBag.Search = search ?? "";
        return View(receipts);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file, string? notes, string? documentType)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, error = "No file received." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
            return Json(new { success = false, error = "Only PDF, JPG and PNG files are supported." });

        try
        {
            var uploadsDir = Path.Combine(_env.ContentRootPath, "Uploads");
            Directory.CreateDirectory(uploadsDir);
            var storedName = Guid.NewGuid() + ext;
            var filePath = Path.Combine(uploadsDir, storedName);

            await using (var fs = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(fs);

            ParsedReceiptData? parsed = null;
            if (ext == ".pdf")
            {
                await using var fs = System.IO.File.OpenRead(filePath);
                parsed = _parser.ParsePdf(fs);
            }

            var receipt = new Receipt
            {
                FileName         = storedName,
                OriginalFileName = file.FileName,
                UploadedAt       = DateTime.UtcNow,
                OrganisationId   = 1,
                TravellerName    = parsed?.TravellerName,
                Origin           = parsed?.Origin,
                Destination      = parsed?.Destination,
                TripDate         = parsed?.TripDate,
                TransportMode    = parsed?.TransportMode,
                Notes            = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                DocumentType     = string.IsNullOrWhiteSpace(documentType) ? null : documentType.Trim(),
            };

            _db.Receipts.Add(receipt);
            await _db.SaveChangesAsync();

            return Json(new { success = true, id = receipt.Id, parsed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt upload failed");
            return Json(new { success = false, error = "Could not process file." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var receipt = await _db.Receipts.FindAsync(id);
        if (receipt == null) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "Uploads", receipt.FileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var ext = Path.GetExtension(receipt.FileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf"            => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            _                 => "application/octet-stream"
        };

        var safeName = string.IsNullOrWhiteSpace(receipt.OriginalFileName)
            ? "Receipt" + ext
            : receipt.OriginalFileName;

        return PhysicalFile(filePath, contentType, safeName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var receipt = await _db.Receipts.FindAsync(id);
            if (receipt == null) { TempData["Error"] = "Receipt not found."; return RedirectToAction(nameof(Index)); }

            var filePath = Path.Combine(_env.ContentRootPath, "Uploads", receipt.FileName);
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _db.Receipts.Remove(receipt);
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Receipt \"{receipt.OriginalFileName}\" deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete receipt {Id} failed", id);
            TempData["Error"] = "Could not delete receipt. Please try again.";
        }
        return RedirectToAction(nameof(Index));
    }
}

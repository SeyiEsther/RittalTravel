using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly RittalTravelOptions _options;

    public ReceiptController(RittalTravelContext db, ReceiptParserService parser,
        IWebHostEnvironment env, ILogger<ReceiptController> logger,
        IOptions<RittalTravelOptions> options)
    {
        _db = db; _parser = parser; _env = env; _logger = logger;
        _options = options.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Receipts.Where(r => r.OrganisationId == _options.OrganisationId).AsQueryable();

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
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, string? notes, string? documentType)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "No file was selected. Please choose a PDF, JPG, or PNG and try again.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
        {
            TempData["Error"] = "Only PDF, JPG and PNG files are supported.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var uploadsDir = UploadPathHelper.GetUploadsDirectory(_env);
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
                OrganisationId   = _options.OrganisationId,
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

            var extracted = new List<string>();
            if (parsed?.TravellerName != null) extracted.Add("name");
            if (parsed?.Origin != null) extracted.Add("origin");
            if (parsed?.Destination != null) extracted.Add("destination");
            if (parsed?.TripDate != null) extracted.Add("date");
            if (parsed?.TransportMode != null) extracted.Add("mode");

            TempData["Success"] = extracted.Count > 0
                ? $"\"{receipt.OriginalFileName}\" uploaded. Extracted: {string.Join(", ", extracted)}."
                : $"\"{receipt.OriginalFileName}\" uploaded successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Receipt upload failed — no write permission to Uploads folder");
            TempData["Error"] = "Could not save the file. Ensure the app has write permission to the Uploads folder.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receipt upload failed");
            TempData["Error"] = "Could not upload the file. Please try again or contact IT support.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var receipt = await _db.Receipts.FindAsync(id);
        if (receipt == null) return NotFound();

        var filePath = UploadPathHelper.ResolveUploadFilePath(_env, receipt.FileName);
        if (filePath == null || !System.IO.File.Exists(filePath)) return NotFound();

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
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

            UploadPathHelper.TryDeleteUploadFile(_env, receipt.FileName);

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

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Services.Storage;

namespace ShiftingGuru.Controllers;

/// <summary>
/// Serves public images that admins upload (city hero photos), because
/// Cloud Run's own disk is wiped on every restart and can't hold them.
///
/// Only files that a Location row actually points at are served, so this
/// can never be used to read anything else in storage - partner documents
/// live under a different prefix and are never matched here.
/// </summary>
[Route("media")]
public class MediaController : Controller
{
    // Only names we generate ourselves: lowercase letters, digits, dashes.
    private static readonly Regex SafeFileName =
        new("^[a-z0-9-]{1,120}\\.(jpg|png|webp)$", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly IDocumentStorage _storage;

    public MediaController(ApplicationDbContext db, IDocumentStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    // GET /media/locations/agra-3f2a....webp
    [HttpGet("locations/{fileName}")]
    public async Task<IActionResult> Location(string fileName, CancellationToken ct)
    {
        if (!SafeFileName.IsMatch(fileName)) return NotFound();

        var path = "media/locations/" + fileName;

        if (!await _db.Locations.AsNoTracking().AnyAsync(l => l.HeroImagePath == path, ct))
        {
            return NotFound();
        }

        var content = await _storage.OpenReadAsync(path, ct);
        if (content is null) return NotFound();

        // Every upload gets a new file name, so a photo at a given URL never
        // changes. Browsers can keep it for a year: repeat visits are instant.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        var type = Path.GetExtension(fileName) switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            _ => "image/webp"
        };

        return File(content, type);
    }
}
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Data;
using ShiftingGuru.Services.Auth;
using ShiftingGuru.Services.Places;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): city search for the request form.
/// Signed-in people only, so strangers can't use your Google key through it.
/// </summary>
[ApiController]
[Route("api/v1/places")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = AdminSeeder.VendorRole + "," + CustomerRoles.Customer)]
public class PlacesController : ControllerBase
{
    private readonly IPlacesClient _places;

    public PlacesController(IPlacesClient places) => _places = places;

    // GET /api/v1/places/cities?q=gurg&session=abc123
    [HttpGet("cities")]
    public async Task<IActionResult> Cities([FromQuery] string? q, [FromQuery] string? session, CancellationToken ct)
    {
        var input = (q ?? "").Trim();

        // Too short to be useful, or suspiciously long: no call to Google, no cost.
        if (input.Length < 2 || input.Length > 60) return Ok(Array.Empty<CitySuggestion>());

        if (!_places.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiError("City search isn't available right now. Type the city name instead.", "placesOff"));
        }

        var cities = await _places.CitiesAsync(input, session?.Trim(), ct);

        return cities is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiError("City search isn't available right now. Type the city name instead.", "placesOff"))
            : Ok(cities);
    }
}
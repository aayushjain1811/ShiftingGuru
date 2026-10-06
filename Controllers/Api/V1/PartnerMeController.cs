using Microsoft.AspNetCore.Mvc;
using ShiftingGuru.Services.Api;

namespace ShiftingGuru.Controllers.Api.V1;

/// <summary>
/// NEW (mobile API): "who am I" for a signed-in partner. The app calls this
/// when it opens, to check a saved session still works.
///
/// CHANGED: sign-in and approval checks now come from ApiPartnerControllerBase,
/// shared with every other partner endpoint.
/// </summary>
[Route("api/v1/partner")]
public class PartnerMeController : ApiPartnerControllerBase
{
    private readonly IPartnerProfileReader _profiles;

    public PartnerMeController(IPartnerProfileReader profiles) => _profiles = profiles;

    // GET /api/v1/partner/me
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        Ok(await _profiles.ReadAsync(CurrentVendor, ct));
}
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShiftingGuru.Services.Jobs;

namespace ShiftingGuru.Controllers;

/// <summary>
/// NEW: the scheduled checks, called every hour by Google Cloud Scheduler.
///
/// Cloud Run only runs your code while it's answering a request, so a timer
/// inside the app can't be trusted to fire. Cloud Scheduler calls this
/// address on a fixed schedule instead, with a secret key in the
/// X-Job-Key header. Without the right key, the address doesn't exist.
///
/// Each call:
///   - completes jobs the customer didn't answer within 3 days
///   - sends the admin's morning email, once a day after 9 am India time
/// </summary>
[ApiController]
[Route("internal/jobs")]
[AllowAnonymous]
public class ScheduledJobsController : ControllerBase
{
    private readonly IJobCompletionService _jobs;
    private readonly JobsOptions _options;

    public ScheduledJobsController(IJobCompletionService jobs, IOptions<JobsOptions> options)
    {
        _jobs = jobs;
        _options = options.Value;
    }

    // POST /internal/jobs/run   (header X-Job-Key: <secret>)
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        if (!KeyMatches(Request.Headers["X-Job-Key"].ToString())) return NotFound();

        var autoConfirmed = await _jobs.AutoConfirmDueAsync(ct);
        var digestSent = await _jobs.SendDailyDigestAsync(ct);

        return Ok(new { autoConfirmed, digestSent });
    }

    /// <summary>Constant-time comparison, so the key can't be guessed by timing.</summary>
    private bool KeyMatches(string given)
    {
        if (string.IsNullOrEmpty(_options.Key) || string.IsNullOrEmpty(given)) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(_options.Key));
    }
}
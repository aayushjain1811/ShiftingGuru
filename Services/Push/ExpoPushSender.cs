using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShiftingGuru.Data;
using ShiftingGuru.Models;

namespace ShiftingGuru.Services.Push;

/// <summary>
/// One push notification: what the phone shows, which screen a tap opens, and
/// which switch controls it (PushCategory). No category = always sent.
/// </summary>
public record PushMessage(string Title, string Body, string? Screen = null, string? Category = null);

public interface IPushSender
{
    /// <summary>
    /// Sends to every phone these people are signed in on. Never throws - a
    /// push that fails must never undo the thing it was announcing.
    /// </summary>
    Task SendAsync(IEnumerable<string> identityUserIds, PushMessage message, CancellationToken ct = default);
}

/// <summary>
/// NEW (mobile apps): sends push notifications through Expo's push service,
/// which passes them on to Google (Android) and Apple (iOS).
///
/// Phones Expo reports as uninstalled are removed, so the list cleans itself.
/// </summary>
public class ExpoPushSender : IPushSender
{
    private const int BatchSize = 100;   // Expo's limit per request

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(HttpClient http, ApplicationDbContext db, ILogger<ExpoPushSender> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task SendAsync(IEnumerable<string> identityUserIds, PushMessage message, CancellationToken ct = default)
    {
        try
        {
            var ids = identityUserIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (ids.Count == 0) return;

            // Leave out anyone who switched this kind of notification off.
            if (message.Category is { } category)
            {
                var muted = await MutedAsync(ids, category, ct);
                ids = ids.Where(id => !muted.Contains(id)).ToList();
                if (ids.Count == 0) return;
            }

            var tokens = await _db.DeviceTokens.AsNoTracking()
                .Where(d => ids.Contains(d.IdentityUserId))
                .Select(d => d.Token)
                .ToListAsync(ct);

            if (tokens.Count == 0) return;

            var data = message.Screen is null ? null : new Dictionary<string, string> { ["screen"] = message.Screen };

            foreach (var batch in tokens.Chunk(BatchSize))
            {
                var payload = batch.Select(token => new ExpoMessage(
                    To: token,
                    Title: message.Title,
                    Body: message.Body,
                    Data: data,
                    Sound: "default",
                    ChannelId: "default",
                    Priority: "high")).ToList();

                using var response = await _http.PostAsJsonAsync("--/api/v2/push/send", payload, Json, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Expo push request failed with {Status}.", (int)response.StatusCode);
                    continue;
                }

                var result = await response.Content.ReadFromJsonAsync<ExpoResponse>(Json, ct);
                await RemoveUninstalledAsync(batch, result, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't send a push notification.");
        }
    }

    /// <summary>The people (of these) who turned this category off. No row = everything on.</summary>
    private async Task<HashSet<string>> MutedAsync(List<string> ids, string category, CancellationToken ct)
    {
        var query = _db.NotificationPreferences.AsNoTracking().Where(p => ids.Contains(p.IdentityUserId));

        query = category switch
        {
            PushCategory.Leads => query.Where(p => !p.Leads),
            PushCategory.Quotes => query.Where(p => !p.Quotes),
            PushCategory.Bookings => query.Where(p => !p.Bookings),
            PushCategory.Reviews => query.Where(p => !p.Reviews),
            _ => query.Where(p => false)
        };

        return (await query.Select(p => p.IdentityUserId).ToListAsync(ct)).ToHashSet();
    }

    /// <summary>Expo answers in the same order as the messages; "DeviceNotRegistered" means the app is gone.</summary>
    private async Task RemoveUninstalledAsync(string[] batch, ExpoResponse? result, CancellationToken ct)
    {
        if (result?.Data is null) return;

        var gone = result.Data
            .Select((ticket, index) => (ticket, index))
            .Where(x => x.ticket.Status == "error"
                     && x.ticket.Details?.Error == "DeviceNotRegistered"
                     && x.index < batch.Length)
            .Select(x => batch[x.index])
            .ToList();

        if (gone.Count == 0) return;

        await _db.DeviceTokens.Where(d => gone.Contains(d.Token)).ExecuteDeleteAsync(ct);
        _logger.LogInformation("Removed {Count} uninstalled device(s) from push.", gone.Count);
    }

    private record ExpoMessage(
        string To, string Title, string Body, Dictionary<string, string>? Data,
        string Sound, string ChannelId, string Priority);

    private record ExpoResponse(List<ExpoTicket>? Data);
    private record ExpoTicket(string? Status, string? Message, ExpoTicketDetails? Details);
    private record ExpoTicketDetails(string? Error);
}
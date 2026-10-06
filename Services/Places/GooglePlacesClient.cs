using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Places;

/// <summary>NEW: settings for Google Places. The key is a secret: user secrets locally, Secret Manager on Cloud Run.</summary>
public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";
    public string ApiKey { get; set; } = "";
}

/// <summary>One city suggestion, e.g. City "Gurugram", Region "Haryana".</summary>
public record CitySuggestion(string PlaceId, string City, string? Region);

public interface IPlacesClient
{
    /// <summary>True when an API key is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Indian cities matching what the person typed. Null when Google can't be
    /// reached - the app then lets them use the name exactly as typed.
    /// </summary>
    Task<IReadOnlyList<CitySuggestion>?> CitiesAsync(string input, string? sessionToken, CancellationToken ct = default);
}

/// <summary>
/// NEW (mobile apps): city search with Google Places (Autocomplete, New API).
///
/// The app never talks to Google directly - it asks our backend, which adds
/// the secret key. So the key never ships inside the app, where anyone could
/// copy it and run up your bill.
///
/// Cities only, India only. The session token groups one person's typing
/// into one search, which is how Google bills it.
/// </summary>
public class GooglePlacesClient : IPlacesClient
{
    private const int MaxSuggestions = 8;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GooglePlacesClient> _logger;

    public GooglePlacesClient(HttpClient http, IOptions<GoogleMapsOptions> options, ILogger<GooglePlacesClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<CitySuggestion>?> CitiesAsync(
        string input, string? sessionToken, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:autocomplete")
            {
                Content = JsonContent.Create(new
                {
                    input,
                    includedPrimaryTypes = new[] { "(cities)" },
                    includedRegionCodes = new[] { "in" },
                    languageCode = "en",
                    sessionToken = string.IsNullOrWhiteSpace(sessionToken) ? null : sessionToken
                }, options: Json)
            };
            request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                // Never log the key. The status is enough to investigate.
                _logger.LogWarning("Google Places returned {Status}.", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(Json, ct);

            return (body?.Suggestions ?? new List<Suggestion>())
                .Select(s => s.PlacePrediction)
                .Where(p => p?.PlaceId is not null && p.StructuredFormat?.MainText?.Text is not null)
                .Select(p => new CitySuggestion(
                    p!.PlaceId!,
                    p.StructuredFormat!.MainText!.Text!,
                    RegionOf(p.StructuredFormat.SecondaryText?.Text)))
                .DistinctBy(c => (c.City, c.Region))
                .Take(MaxSuggestions)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't reach Google Places.");
            return null;
        }
    }

    /// <summary>"Haryana, India" -> "Haryana".</summary>
    private static string? RegionOf(string? secondary)
    {
        if (string.IsNullOrWhiteSpace(secondary)) return null;
        var trimmed = secondary.Trim();
        return trimmed.EndsWith(", India", StringComparison.OrdinalIgnoreCase) ? trimmed[..^7] : trimmed;
    }

    private record AutocompleteResponse(List<Suggestion>? Suggestions);
    private record Suggestion(PlacePrediction? PlacePrediction);
    private record PlacePrediction(string? PlaceId, StructuredFormat? StructuredFormat);
    private record StructuredFormat(TextValue? MainText, TextValue? SecondaryText);
    private record TextValue(string? Text);
}
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

namespace ShiftingGuru.Services.Verification;

/// <summary>
/// Checks the token the browser got from Firebase after a correct SMS code.
///
/// The browser can't be trusted to say "this number is verified", so the
/// server asks Firebase directly. Firebase is only set up the first time a
/// check is needed, so the rest of the site keeps working even if Firebase
/// isn't configured on a developer's machine.
///
/// Credentials: on Cloud Run, the service's own Google identity. Locally, run
/// "gcloud auth application-default login" once.
/// </summary>
public class FirebasePhoneVerificationService : IPhoneVerificationService
{
    private static readonly object Gate = new();

    private readonly string? _projectId;
    private readonly ILogger<FirebasePhoneVerificationService> _logger;
    private FirebaseAuth? _auth;

    public FirebasePhoneVerificationService(
        IConfiguration configuration, ILogger<FirebasePhoneVerificationService> logger)
    {
        _projectId = configuration["Firebase:ProjectId"];
        _logger = logger;
    }

    public async Task<bool> IsVerifiedAsync(string? idToken, string mobileDigits, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(mobileDigits)) return false;

        try
        {
            var token = await GetAuth().VerifyIdTokenAsync(idToken.Trim(), ct);

            return token.Claims.TryGetValue("phone_number", out var claim)
                && claim is string phone
                && phone == $"+91{mobileDigits}";
        }
        catch (FirebaseAuthException ex)
        {
            // Expired, tampered with, or from another project. Normal, not an outage.
            _logger.LogWarning("Rejected a mobile verification token: {Reason}", ex.AuthErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't check a mobile verification token with Firebase.");
            return false;
        }
    }

    private FirebaseAuth GetAuth()
    {
        if (_auth is not null) return _auth;

        lock (Gate)
        {
            if (_auth is not null) return _auth;

            if (string.IsNullOrWhiteSpace(_projectId))
            {
                throw new InvalidOperationException("Firebase:ProjectId is not configured.");
            }

            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.GetApplicationDefault(),
                ProjectId = _projectId
            });

            _auth = FirebaseAuth.GetAuth(app);
            return _auth;
        }
    }
}
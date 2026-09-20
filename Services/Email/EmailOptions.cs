namespace ShiftingGuru.Services.Email;

/// <summary>
/// Bound from the "Email" configuration section. Host, port and addresses come
/// from appsettings; Username and Password must come from user secrets or
/// environment variables and never from a committed file.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>When false, nothing is sent externally. Local default.</summary>
    public bool Enabled { get; set; }

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }
    public string? Password { get; set; }
        /// <summary>
    /// Resend API key. Comes from user secrets locally and Secret Manager in
    /// production - never from appsettings, which is committed.
    /// </summary>
    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = "no-reply@shiftingguru.com";
    public string FromName { get; set; } = "ShiftingGuru";

    /// <summary>Where internal notifications go. Comma-separated in config.</summary>
    public string AdminRecipients { get; set; } = "";

    public IReadOnlyList<string> AdminRecipientList =>
        AdminRecipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}

/// <summary>Bound from "App". Keeps the domain out of the code.</summary>
public class AppOptions
{
    public const string SectionName = "App";

    public string BaseUrl { get; set; } = "http://localhost:5077";
    public string SupportEmail { get; set; } = "support@shiftingguru.com";

    /// <summary>Absolute URL for an app-relative path.</summary>
    public string Url(string path) => $"{BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
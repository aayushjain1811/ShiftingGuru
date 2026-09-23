namespace ShiftingGuru.Services.Storage;

/// <summary>The "Storage" section of appsettings.json.</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Local" (development) or "Gcs" (production).</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>Private bucket name. Only used when Provider is "Gcs".</summary>
    public string? Bucket { get; set; }
}
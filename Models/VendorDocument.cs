namespace ShiftingGuru.Models;

/// <summary>
/// One uploaded partner document. The file itself lives in private storage
/// (a local folder in development, a private Google Cloud Storage bucket in
/// production). This row records where it is and what it is.
/// </summary>
public class VendorDocument
{
    public int Id { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public VendorDocumentType Type { get; set; }

    /// <summary>Path inside storage, e.g. vendors/SG-V-20260923-00004/pan-card-3f2a....jpg</summary>
    public string StoragePath { get; set; } = "";

    /// <summary>Worked out from the file's contents, not trusted from the browser.</summary>
    public string ContentType { get; set; } = "";

    /// <summary>The name the partner's file had. For display only.</summary>
    public string OriginalFileName { get; set; } = "";

    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public enum VendorDocumentType
{
    GstCertificate,
    PanCard,
    AadhaarFront,
    AadhaarBack,
    OfficePhoto
}
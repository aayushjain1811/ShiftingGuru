namespace ShiftingGuru.Models;

/// <summary>
/// One service a vendor offers. A row per service rather than a delimited
/// string, so it can be indexed and filtered in SQL.
///
/// ServiceSlug references the in-memory catalog (StaticServiceCatalog), not a
/// Services table - there isn't one. The slug is validated against the catalog
/// before any row is written.
/// </summary>
public class VendorService
{
    public int Id { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string ServiceSlug { get; set; } = "";

    /// <summary>Denormalised for display, so listings don't need the catalog.</summary>
    public string ServiceName { get; set; } = "";
}
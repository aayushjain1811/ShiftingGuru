using ShiftingGuru.ViewModels.Partner;

namespace ShiftingGuru.Services.Storage;

public record InspectedFile(string ContentType, string Extension);

/// <summary>
/// Decides what a file really is by reading its first bytes (its "signature"),
/// not by trusting its name or the type the browser claims. Renaming virus.exe
/// to photo.jpg doesn't get past this.
/// </summary>
public static class DocumentInspector
{
    public static async Task<InspectedFile?> InspectAsync(IFormFile file, bool allowPdf, CancellationToken ct = default)
    {
        if (file.Length == 0 || file.Length > PartnerRegistrationViewModel.MaxFileBytes) return null;

        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        return Detect(header, read, allowPdf);
    }

    private static InspectedFile? Detect(byte[] buffer, int read, bool allowPdf)
    {
        ReadOnlySpan<byte> h = buffer.AsSpan(0, read);

        if (h.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            return new InspectedFile("image/jpeg", ".jpg");

        if (h.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return new InspectedFile("image/png", ".png");

        if (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new InspectedFile("image/webp", ".webp");

        if (allowPdf && h.StartsWith("%PDF-"u8))
            return new InspectedFile("application/pdf", ".pdf");

        return null;
    }
}
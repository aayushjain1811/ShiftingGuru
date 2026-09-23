namespace ShiftingGuru.Services.Storage;

/// <summary>
/// Where partner documents are kept. Never publicly reachable: files are only
/// ever read back through an admin-only controller action.
/// </summary>
public interface IDocumentStorage
{
    Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Null if the file doesn't exist.</summary>
    Task<Stream?> OpenReadAsync(string objectName, CancellationToken ct = default);

    /// <summary>Does nothing if the file is already gone.</summary>
    Task DeleteAsync(string objectName, CancellationToken ct = default);
}
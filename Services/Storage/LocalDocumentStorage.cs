namespace ShiftingGuru.Services.Storage;

/// <summary>
/// Development only. Saves files under App_Data/partner-documents, which is
/// outside wwwroot, so the browser can never load them directly.
///
/// Not for Cloud Run: its disk is wiped on every restart. Program.cs refuses
/// to start outside Development with this provider.
/// </summary>
public class LocalDocumentStorage : IDocumentStorage
{
    private readonly string _root;

    public LocalDocumentStorage(IHostEnvironment env)
    {
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, "App_Data", "partner-documents"));
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        var path = PathFor(objectName);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        var path = PathFor(objectName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    // Stops a path like "../../appsettings.json" ever escaping the folder.
    private string PathFor(string objectName)
    {
        var full = Path.GetFullPath(Path.Combine(_root, objectName));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid document path.");
        }
        return full;
    }
}
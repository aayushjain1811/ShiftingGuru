using System.Net;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace ShiftingGuru.Services.Storage;

/// <summary>
/// Production storage: a PRIVATE Google Cloud Storage bucket.
///
/// No key file. On Cloud Run the client signs in as the service's own
/// service account, which is given access to this one bucket only.
/// </summary>
public class GcsDocumentStorage : IDocumentStorage
{
    private readonly StorageClient _client;
    private readonly string _bucket;

    public GcsDocumentStorage(IOptions<StorageOptions> options)
    {
        _bucket = options.Value.Bucket
            ?? throw new InvalidOperationException("Storage:Bucket is not configured.");

        if (string.IsNullOrWhiteSpace(_bucket))
        {
            throw new InvalidOperationException("Storage:Bucket is not configured.");
        }

        _client = StorageClient.Create();
    }

    public async Task SaveAsync(string objectName, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.UploadObjectAsync(_bucket, objectName, contentType, content, cancellationToken: ct);
    }

    public async Task<Stream?> OpenReadAsync(string objectName, CancellationToken ct = default)
    {
        // Files are at most 5 MB, so holding one in memory is fine.
        var buffer = new MemoryStream();
        try
        {
            await _client.DownloadObjectAsync(_bucket, objectName, buffer, cancellationToken: ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            await buffer.DisposeAsync();
            return null;
        }

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string objectName, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, objectName, cancellationToken: ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            // Already gone - that's the goal.
        }
    }
}
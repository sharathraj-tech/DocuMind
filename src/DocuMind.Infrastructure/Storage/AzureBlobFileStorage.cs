using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocuMind.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Storage;

public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _ready;

    public AzureBlobFileStorage(IOptions<StorageOptions> options, BlobServiceClient blobServiceClient)
    {
        var storage = options.Value;

        if (string.IsNullOrWhiteSpace(storage.ConnectionString))
            throw new InvalidOperationException("Storage:ConnectionString is missing or empty.");
        if (string.IsNullOrWhiteSpace(storage.Container))
            throw new InvalidOperationException("Storage:Container is missing or empty.");

        _blobServiceClient = blobServiceClient;
        _container = _blobServiceClient.GetBlobContainerClient(storage.Container.Trim().ToLowerInvariant());
        _container.CreateIfNotExists();
    }

    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobName);
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);
    }

    public async Task<Stream> OpenReadAsync(string blobName, CancellationToken ct)
    {
        var container = await EnsureContainerAsync(ct);
        return await container.GetBlobClient(blobName).OpenReadAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct)
    {
        var container = await EnsureContainerAsync(ct);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }

    private async Task<BlobContainerClient> EnsureContainerAsync(CancellationToken ct)
    {
        if (_ready) return _container;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_ready)
            {
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _ready = true;
            }
        }
        finally
        {
            _gate.Release();
        }

        return _container;
    }

}

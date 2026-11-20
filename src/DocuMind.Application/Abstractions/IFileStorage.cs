using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface IFileStorage
    {
        Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct);
        Task<Stream> OpenReadAsync(string blobName, CancellationToken ct);
        Task DeleteAsync(string blobName, CancellationToken ct);
    }
}

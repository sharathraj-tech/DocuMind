using DocuMind.Application.Common;
using DocuMind.Application.Documents;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface IDocumentService
    {
        Task<Result<DocumentDto>> UploadAsync(string fileName, Stream content, CancellationToken ct);
        Task<IReadOnlyList<DocumentDto>> ListAsync(int skip, int take, CancellationToken ct);
        Task<Result<DocumentDto>> GetAsync(Guid id, CancellationToken ct);
        Task<Result<DocumentFile>> OpenFileAsync(Guid id, CancellationToken ct);
        Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct);
        Task<Result<DocumentDto>> ReindexAsync(Guid id, CancellationToken ct);
        Task<Result<int>> ReindexOutdatedAsync(CancellationToken ct);
    }
}

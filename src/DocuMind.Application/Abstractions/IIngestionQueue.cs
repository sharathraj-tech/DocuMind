using DocuMind.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public sealed record IngestionJob(Guid TenantId, Guid DocumentId);
    public interface IIngestionQueue
    {
        ValueTask EnqueueAsync(IngestionJob job, CancellationToken ct = default);
    }

    public interface IDocumentIngestor
    {
        Task IngestAsync(Document document, CancellationToken ct);
    }
}

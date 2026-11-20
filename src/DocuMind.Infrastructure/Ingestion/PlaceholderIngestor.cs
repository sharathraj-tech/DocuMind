using DocuMind.Application.Abstractions;
using DocuMind.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Ingestion
{
    public sealed class PlaceholderIngestor : IDocumentIngestor
    {
        public Task IngestAsync(Document document, CancellationToken ct) => Task.CompletedTask;
    }
}

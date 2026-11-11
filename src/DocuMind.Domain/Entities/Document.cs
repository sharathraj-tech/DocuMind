using DocuMind.Domain.Common;
using DocuMind.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Entities
{
    public class Document:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string BlobName { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
        public int? PageCount { get; set; }
        public string? Error { get; set; }
        public Guid UploadedByUserId { get; set; }
        public List<DocumentChunk> Chunks { get; set; } = [];
    }
}

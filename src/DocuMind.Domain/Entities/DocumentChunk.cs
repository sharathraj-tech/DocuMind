using DocuMind.Domain.Common;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Domain.Entities
{
    public class DocumentChunk:BaseEntity,ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public int ChunkIndex { get; set; }
        public int? PageNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public int TokenCount { get; set; }
        public Vector? Embedding { get; set; }
        public string? EmbeddingModel { get; set; } 
    }
}

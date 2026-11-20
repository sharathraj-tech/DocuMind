using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Search
{
    public sealed record SearchRequest(string Query, int? TopK = null);

    // Score is cosine similarity (1 = identical direction, 0 = unrelated).
    public sealed record SearchHit(
        Guid ChunkId,
        Guid DocumentId,
        string DocumentName,
        int? PageNumber,
        int ChunkIndex,
        string Content,
        double Score);
}

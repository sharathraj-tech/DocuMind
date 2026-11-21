using DocuMind.Application.Common;
using DocuMind.Application.Search;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface ISearchService
    {
        Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(SearchRequest request, CancellationToken ct);
    }
}

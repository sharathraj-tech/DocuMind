using DocuMind.Application.Chat;
using DocuMind.Application.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface IConversationService
    {
        Task<IReadOnlyList<ConversationSummary>> ListAsync(int skip, int take, CancellationToken ct);
        Task<Result<ConversationDetail>> GetAsync(Guid id, CancellationToken ct);
        Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct);
        Task<Result<bool>> SubmitFeedbackAsync(Guid messageId, FeedbackRequest request, CancellationToken ct);
    }
}

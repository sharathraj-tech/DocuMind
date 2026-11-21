using DocuMind.Application.Chat;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface IChatService
    {
        Task StreamAsync(ChatRequest request, Func<ChatEvent, ValueTask> emit, CancellationToken ct);
    }
}

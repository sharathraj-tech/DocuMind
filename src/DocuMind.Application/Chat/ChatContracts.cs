using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Chat
{
    public sealed record ChatRequest(Guid? ConversationId, string Message);

    // A document chunk offered to the model. Index is the [n] it can cite.
    public sealed record ChatSource(
        int Index,
        Guid ChunkId,
        Guid DocumentId,
        string DocumentName,
        int? PageNumber,
        string Snippet,
        double Score);

    // The events streamed to the client. EventName() is the SSE "event:" line; the record is the JSON "data:".
    public abstract record ChatEvent
    {
        public abstract string EventName();
    }

    public sealed record ConversationEvent(Guid ConversationId) : ChatEvent
    {
        public override string EventName() => "conversation";
    }

    public sealed record SourcesEvent(IReadOnlyList<ChatSource> Sources) : ChatEvent
    {
        public override string EventName() => "sources";
    }

    public sealed record TokenEvent(string Text) : ChatEvent
    {
        public override string EventName() => "token";
    }

    public sealed record DoneEvent(
        Guid MessageId,
        IReadOnlyList<ChatSource> Citations,
        int? PromptTokens,
        int? CompletionTokens,
        int LatencyMs,
        bool Answered) : ChatEvent
    {
        public override string EventName() => "done";
    }

    public sealed record ErrorEvent(string Message) : ChatEvent
    {
        public override string EventName() => "error";
    }

    // Conversation history
    public sealed record ConversationSummary(Guid Id, string Title, DateTime CreatedAt, DateTime LastActivityAt);

    public sealed record CitationDto(
        int SourceIndex, Guid? ChunkId, Guid? DocumentId, string DocumentName, int? PageNumber, string Snippet, double Score);

    public sealed record MessageDto(
        Guid Id, string Role, string Content, DateTime CreatedAt, string? Model, IReadOnlyList<CitationDto> Citations);

    public sealed record ConversationDetail(Guid Id, string Title, IReadOnlyList<MessageDto> Messages);

    public sealed record FeedbackRequest(bool IsHelpful, string? Comment);
}

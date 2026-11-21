using DocuMind.Application.Abstractions;
using DocuMind.Application.Chat;
using DocuMind.Application.Common;
using DocuMind.Application.Ingestion;
using DocuMind.Application.Search;
using DocuMind.Domain.Entities;
using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.AI;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DocuMind.Infrastructure.Chat
{
    public sealed class ChatService(
    AppDbContext db,
    ISearchService search,
    IChatClient chat,
    ITokenCounter tokens,
    ICurrentUser user,
    IOptions<ChatSettings> settings,
    ILogger<ChatService> log) : IChatService
    {
        private const int MaxQuestionLength = 4000;
        private const int SnippetLength = 300;

        public async Task StreamAsync(ChatRequest request, Func<ChatEvent, ValueTask> emit, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            var s = settings.Value;

            if (user.UserId is not { } userId)
            {
                await emit(new ErrorEvent("Not authenticated."));
                return;
            }

            var question = (request.Message ?? string.Empty).Trim();
            if (question.Length == 0 || question.Length > MaxQuestionLength)
            {
                await emit(new ErrorEvent($"The message must be between 1 and {MaxQuestionLength} characters."));
                return;
            }

            Conversation conversation;
            if (request.ConversationId is { } existingId)
            {
                var found = await db.Conversations.FirstOrDefaultAsync(c => c.Id == existingId && c.UserId == userId, ct);
                if (found is null)
                {
                    await emit(new ErrorEvent("Conversation not found."));
                    return;
                }
                conversation = found;
            }
            else
            {
                conversation = new Conversation { UserId = userId, Title = MakeTitle(question) };
                db.Conversations.Add(conversation);
            }

            var history = request.ConversationId is null
                ? []
                : await LoadHistoryAsync(conversation.Id, s.HistoryMessages, ct);

            db.Messages.Add(new Message { ConversationId = conversation.Id, Role = MessageRole.User, Content = question });
            await db.SaveChangesAsync(ct);
            await emit(new ConversationEvent(conversation.Id));

            var answer = new StringBuilder();
            UsageDetails? usage = null;
            List<ChatSource> sources = [];
            var modelCalled = false;
            var cancelled = false;

            try
            {
                var query = await RewriteAsync(history, question, s, ct);
                var found = await search.SearchAsync(new SearchRequest(query, s.TopK), ct);
                if (!found.Succeeded)
                {
                    await emit(new ErrorEvent(found.Error ?? "Search failed."));
                    return;
                }

                var hits = found.Value!;
                log.LogInformation("Chat retrieval: {Count} hits, top score {Top:F3}", hits.Count, hits.Count > 0 ? hits[0].Score : 0);

                var usable = hits.Where(h => h.Score >= s.MinScore).ToList();
                var selected = PromptBuilder.SelectWithinBudget(usable, s.ContextTokenBudget, tokens.Count);

                sources = selected
                    .Select((h, i) => new ChatSource(i + 1, h.ChunkId, h.DocumentId, h.DocumentName, h.PageNumber,
                        Snippet(h.Content), h.Score))
                    .ToList();

                await emit(new SourcesEvent(sources));

                if (selected.Count == 0)
                {
                    answer.Append(ChatPrompts.NoAnswer);
                    await emit(new TokenEvent(ChatPrompts.NoAnswer));
                }
                else
                {
                    modelCalled = true;
                    var messages = BuildMessages(history, question, selected);

                    await foreach (var update in chat.GetStreamingResponseAsync(messages, AnswerOptions(s), ct))
                    {
                        if (!string.IsNullOrEmpty(update.Text))
                        {
                            answer.Append(update.Text);
                            await emit(new TokenEvent(update.Text));
                        }

                        foreach (var content in update.Contents.OfType<UsageContent>())
                            usage = content.Details;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Chat failed for conversation {ConversationId}", conversation.Id);
                await emit(new ErrorEvent("Something went wrong while generating the answer. Please try again."));
                return;
            }

            var text = answer.ToString().Trim();
            if (text.Length == 0) return;

            var answered = !text.StartsWith(ChatPrompts.NoAnswer, StringComparison.OrdinalIgnoreCase);
            var cited = answered ? CitationParser.ExtractIndexes(text, sources.Count) : [];

            var assistant = new Message
            {
                ConversationId = conversation.Id,
                Role = MessageRole.Assistant,
                Content = text,
                Model = modelCalled ? s.Model : null,
                PromptTokens = ToInt(usage?.InputTokenCount),
                CompletionTokens = ToInt(usage?.OutputTokenCount),
                LatencyMs = (int)clock.ElapsedMilliseconds
            };

            var citedSources = new List<ChatSource>();
            foreach (var index in cited)
            {
                var source = sources[index - 1];
                citedSources.Add(source);
                assistant.Citations.Add(new Citation
                {
                    SourceIndex = source.Index,
                    ChunkId = source.ChunkId,
                    DocumentId = source.DocumentId,
                    DocumentName = source.DocumentName,
                    PageNumber = source.PageNumber,
                    Snippet = source.Snippet,
                    Score = source.Score
                });
            }

            db.Messages.Add(assistant);

            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Could not save the answer for conversation {ConversationId}", conversation.Id);
                if (!cancelled) await emit(new ErrorEvent("The answer could not be saved."));
                return;
            }

            if (!cancelled)
            {
                await emit(new DoneEvent(
                    assistant.Id, citedSources, assistant.PromptTokens, assistant.CompletionTokens,
                    assistant.LatencyMs ?? 0, answered));
            }
        }

        private async Task<List<Message>> LoadHistoryAsync(Guid conversationId, int take, CancellationToken ct)
        {
            var recent = await db.Messages
                .Where(m => m.ConversationId == conversationId && m.Role != MessageRole.System)
                .OrderByDescending(m => m.CreatedAt)
                .Take(Math.Max(take, 0))
                .ToListAsync(ct);

            recent.Reverse();
            return recent;
        }
        private async Task<string> RewriteAsync(IReadOnlyList<Message> history, string question, ChatSettings s, CancellationToken ct)
        {
            if (history.Count == 0) return question;

            try
            {
                var transcript = string.Join("\n", history.Select(m =>
                    $"{(m.Role == MessageRole.User ? "User" : "Assistant")}: {Shorten(CitationParser.StripMarkers(m.Content), 500)}"));

                var response = await chat.GetResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, ChatPrompts.RewriteSystem),
                    new ChatMessage(ChatRole.User, $"Conversation so far:\n{transcript}\n\nLast user message: {question}")
                    ],
                    new ChatOptions { ModelId = s.RewriteModel, Temperature = 0, MaxOutputTokens = 200 },
                    ct);

                var rewritten = response.Text.Trim();
                return rewritten.Length is > 0 and <= 1000 ? rewritten : question;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Query rewrite failed; searching with the original question");
                return question;
            }
        }

        private static List<ChatMessage> BuildMessages(IReadOnlyList<Message> history, string question, IReadOnlyList<SearchHit> selected)
        {
            var messages = new List<ChatMessage> { new(ChatRole.System, ChatPrompts.System) };

            foreach (var m in history)
            {
                messages.Add(m.Role == MessageRole.User
                    ? new ChatMessage(ChatRole.User, m.Content)
                    : new ChatMessage(ChatRole.Assistant, CitationParser.StripMarkers(m.Content)));
            }

            messages.Add(new ChatMessage(ChatRole.User, $"{PromptBuilder.BuildContext(selected)}\n\nQuestion: {question}"));
            return messages;
        }

        private static ChatOptions AnswerOptions(ChatSettings s)
        {
            var options = new ChatOptions { ModelId = s.Model, Temperature = s.Temperature, MaxOutputTokens = s.MaxOutputTokens };

            if (!string.IsNullOrWhiteSpace(s.ThinkingLevel))
            {
                options.AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [GeminiChatClient.ThinkingLevelKey] = s.ThinkingLevel
                };
            }

            return options;
        }

        private static string MakeTitle(string question)
        {
            var oneLine = question.ReplaceLineEndings(" ");
            return oneLine.Length <= 60 ? oneLine : oneLine[..60].TrimEnd() + "...";
        }

        private static string Snippet(string content) => Shorten(content.ReplaceLineEndings(" "), SnippetLength);

        private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max];

        private static int? ToInt(long? value) => value is { } v ? (int)Math.Min(v, int.MaxValue) : null;

    }
}

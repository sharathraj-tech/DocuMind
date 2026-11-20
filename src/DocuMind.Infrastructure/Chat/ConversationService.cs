using DocuMind.Application.Abstractions;
using DocuMind.Application.Chat;
using DocuMind.Application.Common;
using DocuMind.Domain.Entities;
using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Chat
{
    public sealed class ConversationService(AppDbContext db, ICurrentUser user) : IConversationService
    {
        public async Task<IReadOnlyList<ConversationSummary>> ListAsync(int skip, int take, CancellationToken ct)
        {
            if (user.UserId is not { } userId) return [];

            var rows = await db.Conversations
                .Where(c => c.UserId == userId)
                .Select(c => new { c.Id, c.Title, c.CreatedAt, Last = c.Messages.Max(m => (DateTime?)m.CreatedAt) })
                .OrderByDescending(c => c.Last ?? c.CreatedAt)
                .Skip(Math.Max(skip, 0))
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);

            return rows.Select(r => new ConversationSummary(r.Id, r.Title, r.CreatedAt, r.Last ?? r.CreatedAt)).ToList();
        }

        public async Task<Result<ConversationDetail>> GetAsync(Guid id, CancellationToken ct)
        {
            if (user.UserId is not { } userId)
                return Result<ConversationDetail>.Fail("Not authenticated.", 401);

            var conversation = await db.Conversations
                .Where(c => c.Id == id && c.UserId == userId)
                .Select(c => new { c.Id, c.Title })
                .FirstOrDefaultAsync(ct);

            if (conversation is null)
                return Result<ConversationDetail>.Fail("Conversation not found.", 404);

            var messages = await db.Messages
                .Where(m => m.ConversationId == id)
                .Include(m => m.Citations)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);

            var dtos = messages.Select(m => new MessageDto(
                m.Id,
                m.Role.ToString(),
                m.Content,
                m.CreatedAt,
                m.Model,
                m.Citations
                    .OrderBy(c => c.SourceIndex)
                    .Select(c => new CitationDto(c.SourceIndex, c.ChunkId, c.DocumentId, c.DocumentName, c.PageNumber, c.Snippet, c.Score))
                    .ToList())).ToList();

            return Result<ConversationDetail>.Ok(new ConversationDetail(conversation.Id, conversation.Title, dtos));
        }

        public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
        {
            if (user.UserId is not { } userId)
                return Result<bool>.Fail("Not authenticated.", 401);

            var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
            if (conversation is null)
                return Result<bool>.Fail("Conversation not found.", 404);

            db.Conversations.Remove(conversation);   // messages, citations and feedback go with it (database cascade)
            await db.SaveChangesAsync(ct);
            return Result<bool>.Ok(true);
        }

        public async Task<Result<bool>> SubmitFeedbackAsync(Guid messageId, FeedbackRequest request, CancellationToken ct)
        {
            if (user.UserId is not { } userId)
                return Result<bool>.Fail("Not authenticated.", 401);

            var owner = await db.Messages
                .Where(m => m.Id == messageId && m.Role == MessageRole.Assistant)
                .Select(m => new { m.Conversation.UserId })
                .FirstOrDefaultAsync(ct);

            if (owner is null || owner.UserId != userId)
                return Result<bool>.Fail("Message not found.", 404);

            var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (comment is { Length: > 2000 }) comment = comment[..2000];

            // One feedback row per user per message; sending it again changes the rating.
            var feedback = await db.Feedbacks.FirstOrDefaultAsync(f => f.MessageId == messageId && f.UserId == userId, ct);
            if (feedback is null)
            {
                db.Feedbacks.Add(new Feedback { MessageId = messageId, UserId = userId, IsHelpful = request.IsHelpful, Comment = comment });
            }
            else
            {
                feedback.IsHelpful = request.IsHelpful;
                feedback.Comment = comment;
            }

            await db.SaveChangesAsync(ct);
            return Result<bool>.Ok(true);
        }
    }
}

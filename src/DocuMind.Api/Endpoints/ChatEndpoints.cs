using DocuMind.Application.Abstractions;
using DocuMind.Application.Chat;

namespace DocuMind.Api.Endpoints
{
    public static class ChatEndpoints
    {
        public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
        {
            // POST (not GET) because the body carries the message. The Angular client will use fetch-event-source.
            app.MapPost("/api/chat/stream", async (ChatRequest request, HttpContext http, IChatService chat, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.Problem(title: "A message is required.", statusCode: StatusCodes.Status400BadRequest);

                SseWriter.Start(http);

                try
                {
                    await chat.StreamAsync(request, e => SseWriter.WriteAsync(http, e, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    // client disconnected; nothing left to send
                }

                return Results.Empty;
            }).WithTags("Chat").RequireAuthorization();

            var conversations = app.MapGroup("/api/conversations").WithTags("Conversations").RequireAuthorization();

            conversations.MapGet("/", async (int? skip, int? take, IConversationService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(skip ?? 0, take ?? 50, ct)));

            conversations.MapGet("/{id:guid}", async (Guid id, IConversationService svc, CancellationToken ct) =>
            {
                var result = await svc.GetAsync(id, ct);
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : Results.Problem(title: result.Error, statusCode: result.Status);
            });

            conversations.MapDelete("/{id:guid}", async (Guid id, IConversationService svc, CancellationToken ct) =>
            {
                var result = await svc.DeleteAsync(id, ct);
                return result.Succeeded
                    ? Results.NoContent()
                    : Results.Problem(title: result.Error, statusCode: result.Status);
            });

            app.MapPost("/api/messages/{id:guid}/feedback", async (Guid id, FeedbackRequest request, IConversationService svc, CancellationToken ct) =>
            {
                var result = await svc.SubmitFeedbackAsync(id, request, ct);
                return result.Succeeded
                    ? Results.NoContent()
                    : Results.Problem(title: result.Error, statusCode: result.Status);
            }).WithTags("Conversations").RequireAuthorization();

            return app;
        }
    }
}

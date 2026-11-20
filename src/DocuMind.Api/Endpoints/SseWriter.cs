using DocuMind.Application.Chat;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;

namespace DocuMind.Api.Endpoints
{
    public static class SseWriter
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static void Start(HttpContext http)
        {
            var response = http.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";   // stop reverse proxies (nginx) from buffering the stream
            http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        }

        public static async ValueTask WriteAsync(HttpContext http, ChatEvent chatEvent, CancellationToken ct)
        {
            try
            {
                // Serialize by runtime type so each event's own fields are included.
                var data = JsonSerializer.Serialize(chatEvent, chatEvent.GetType(), Json);
                await http.Response.WriteAsync($"event: {chatEvent.EventName()}\ndata: {data}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
            catch (IOException)
            {
                throw new OperationCanceledException(ct);   // the client closed the connection
            }
        }
    }
}

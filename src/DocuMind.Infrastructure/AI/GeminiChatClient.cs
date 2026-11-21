using DocuMind.Application.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace DocuMind.Infrastructure.AI
{
    public sealed class GeminiChatClient(
    HttpClient http,
    IOptions<GeminiOptions> gemini,
    IOptions<ChatSettings> settings) : IChatClient
    {
        // ChatOptions.AdditionalProperties key for Gemini's thinking level ("minimal", "low", ...). Optional.
        public const string ThinkingLevelKey = "ThinkingLevel";

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            using var request = BuildRequest(messages, options, stream: false);
            using var response = await http.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            var text = ExtractText(root);
            if (text.Length == 0)
                throw new InvalidOperationException($"Gemini returned no text (finish reason: {ExtractFinishReason(root) ?? "unknown"}).");

            var result = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
            if (ExtractUsage(root) is { } usage) result.Usage = usage;
            return result;
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var request = BuildRequest(messages, options, stream: true);

            // ResponseHeadersRead: hand back the response as soon as headers arrive instead of buffering the whole body.
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var produced = false;
            string? finishReason = null;

            await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(item.Data)) continue;

                using var doc = JsonDocument.Parse(item.Data);
                var root = doc.RootElement;

                finishReason = ExtractFinishReason(root) ?? finishReason;

                var text = ExtractText(root);
                if (text.Length > 0)
                {
                    produced = true;
                    yield return new ChatResponseUpdate(ChatRole.Assistant, text);
                }

                if (ExtractUsage(root) is { } usage)
                    yield return new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new UsageContent(usage)] };
            }

            if (!produced)
                throw new InvalidOperationException($"Gemini returned no text (finish reason: {finishReason ?? "unknown"}).");
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }

        private HttpRequestMessage BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options, bool stream)
        {
            var apiKey = gemini.Value.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "Gemini:ApiKey is not configured. Run: dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\" --project src/DocuMind.Api");

            var model = options?.ModelId ?? settings.Value.Model;

            var system = new List<string>();
            var contents = new List<(string Role, StringBuilder Text)>();

            foreach (var message in messages)
            {
                var text = message.Text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (message.Role == ChatRole.System)
                {
                    system.Add(text);
                    continue;
                }

                var role = message.Role == ChatRole.Assistant ? "model" : "user";

                // Gemini expects alternating turns, so consecutive messages from the same side are merged
                // (this happens after a failed answer leaves two user messages in a row).
                if (contents.Count > 0 && contents[^1].Role == role)
                    contents[^1].Text.Append("\n\n").Append(text);
                else
                    contents.Add((role, new StringBuilder(text)));
            }

            var body = new Dictionary<string, object?>
            {
                ["contents"] = contents
                    .Select(c => new { role = c.Role, parts = new[] { new { text = c.Text.ToString() } } })
                    .ToList()
            };

            if (system.Count > 0)
                body["systemInstruction"] = new { parts = new[] { new { text = string.Join("\n\n", system) } } };

            var generation = new Dictionary<string, object?>();
            if (options?.Temperature is { } temperature) generation["temperature"] = temperature;
            if (options?.MaxOutputTokens is { } maxTokens) generation["maxOutputTokens"] = maxTokens;

            if (options?.AdditionalProperties?.TryGetValue(ThinkingLevelKey, out var level) == true
                && level?.ToString() is { Length: > 0 } thinkingLevel)
                generation["thinkingConfig"] = new { thinkingLevel };

            if (generation.Count > 0) body["generationConfig"] = generation;

            var path = stream
                ? $"v1beta/models/{model}:streamGenerateContent?alt=sse"
                : $"v1beta/models/{model}:generateContent";

            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("x-goog-api-key", apiKey);
            return request;
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode) return;

            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Gemini request failed ({(int)response.StatusCode}): {(error.Length <= 500 ? error : error[..500])}");
        }

        private static string ExtractText(JsonElement root)
        {
            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
                return "";

            var sb = new StringBuilder();
            if (candidates[0].TryGetProperty("content", out var content)
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True) continue;
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        sb.Append(text.GetString());
                }
            }
            return sb.ToString();
        }

        private static string? ExtractFinishReason(JsonElement root)
        {
            if (root.TryGetProperty("candidates", out var candidates)
                && candidates.ValueKind == JsonValueKind.Array
                && candidates.GetArrayLength() > 0
                && candidates[0].TryGetProperty("finishReason", out var reason)
                && reason.ValueKind == JsonValueKind.String)
                return reason.GetString();

            return null;
        }

        private static UsageDetails? ExtractUsage(JsonElement root)
        {
            if (!root.TryGetProperty("usageMetadata", out var usage) || usage.ValueKind != JsonValueKind.Object)
                return null;

            long? prompt = usage.TryGetProperty("promptTokenCount", out var p) && p.TryGetInt64(out var pv) ? pv : null;
            long? total = usage.TryGetProperty("totalTokenCount", out var t) && t.TryGetInt64(out var tv) ? tv : null;
            long? candidates = usage.TryGetProperty("candidatesTokenCount", out var c) && c.TryGetInt64(out var cv) ? cv : null;

            // Output includes any thinking tokens, which are billed as output: total minus prompt.
            var output = prompt is not null && total is not null ? total - prompt : candidates;

            return new UsageDetails { InputTokenCount = prompt, OutputTokenCount = output, TotalTokenCount = total };
        }
    }
}

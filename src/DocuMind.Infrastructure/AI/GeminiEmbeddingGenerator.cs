using DocuMind.Application.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DocuMind.Infrastructure.AI
{
    public sealed class GeminiEmbeddingGenerator(
    HttpClient http,
    IOptions<GeminiOptions> gemini,
    IOptions<EmbeddingOptions> embedding) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
        {
            var apiKey = gemini.Value.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "Gemini:ApiKey is not configured. Run: dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\" --project src/DocuMind.Api");

            var settings = embedding.Value;
            var model = options?.ModelId ?? settings.Model;
            var dimensions = options?.Dimensions ?? settings.Dimensions;

            string? taskType = null;
            if (options?.AdditionalProperties?.TryGetValue(EmbeddingTaskTypes.Key, out var value) == true)
                taskType = value?.ToString();
            taskType ??= EmbeddingTaskTypes.Document;

            var texts = values.ToList();
            var results = new List<Embedding<float>>(texts.Count);

            foreach (var batch in texts.Chunk(Math.Max(1, settings.BatchSize)))
            {
                var body = new
                {
                    requests = batch.Select(text => new
                    {
                        model = $"models/{model}",
                        content = new { parts = new[] { new { text } } },
                        taskType,
                        outputDimensionality = dimensions
                    })
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:batchEmbedContents")
                {
                    Content = JsonContent.Create(body)
                };
                request.Headers.Add("x-goog-api-key", apiKey);

                using var response = await http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"Gemini embeddings request failed ({(int)response.StatusCode}): {Truncate(error, 500)}");
                }

                var parsed = await response.Content.ReadFromJsonAsync<BatchResponse>(cancellationToken);
                if (parsed?.Embeddings is null || parsed.Embeddings.Count != batch.Length)
                    throw new InvalidOperationException(
                        $"Gemini returned {parsed?.Embeddings?.Count ?? 0} embeddings for {batch.Length} inputs.");

                foreach (var item in parsed.Embeddings)
                {
                    if (item.Values is null || item.Values.Count == 0)
                        throw new InvalidOperationException("Gemini returned an empty embedding.");
                    results.Add(new Embedding<float>(Normalize(item.Values)));
                }
            }

            return new GeneratedEmbeddings<Embedding<float>>(results);
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() { }   // the HttpClient belongs to IHttpClientFactory

        // Gemini only returns unit-length vectors at its full size; shorter (truncated) ones need normalizing.
        private static float[] Normalize(List<float> values)
        {
            var norm = MathF.Sqrt(values.Sum(v => v * v));
            var result = values.ToArray();
            if (norm > 0)
                for (var i = 0; i < result.Length; i++) result[i] /= norm;
            return result;
        }

        private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

        private sealed record BatchResponse([property: JsonPropertyName("embeddings")] List<ContentEmbedding>? Embeddings);

        private sealed record ContentEmbedding([property: JsonPropertyName("values")] List<float>? Values);

    }
}

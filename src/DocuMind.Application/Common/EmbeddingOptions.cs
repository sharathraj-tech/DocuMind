using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public class EmbeddingOptions
    {
        public const string SectionName = "Embedding";

        public string Model { get; set; } = "gemini-embedding-001";
        public int Dimensions { get; set; } = 768;
        public int BatchSize { get; set; } = 50;

        // Stored on every chunk, so we can tell which chunks need re-embedding after a model or size change.
        public string Tag => $"{Model}/{Dimensions}";
    }
}

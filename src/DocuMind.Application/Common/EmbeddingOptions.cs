using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public class EmbeddingOptions
    {
        public const string SectionName = "Embedding";
        public int Dimensions { get; set; } = 768;
    }
}

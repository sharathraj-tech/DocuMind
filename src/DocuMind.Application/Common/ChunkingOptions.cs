using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public class ChunkingOptions
    {
        public const string SectionName = "Chunking";
        public int TargetTokens { get; set; } = 600;
        public int OverlapTokens { get; set; } = 80;

    }
}

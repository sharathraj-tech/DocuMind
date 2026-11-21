using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public class ChatSettings
    {
        public const string SectionName = "Chat";

        public string Model { get; set; } = "gemini-3.6-flash";
        public string RewriteModel { get; set; } = "gemini-3.5-flash-lite";
        public int TopK { get; set; } = 6;
        public double MinScore { get; set; } = 0;
        public int ContextTokenBudget { get; set; } = 4000;
        public int HistoryMessages { get; set; } = 6;
        public float Temperature { get; set; } = 0.2f;
        public int MaxOutputTokens { get; set; } = 2048;
        public string? ThinkingLevel { get; set; } = "minimal";
    }
}

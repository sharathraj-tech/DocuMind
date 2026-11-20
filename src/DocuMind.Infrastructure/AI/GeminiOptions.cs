using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.AI
{
    public class GeminiOptions
    {
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    }
}

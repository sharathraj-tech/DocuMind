using DocuMind.Application.Ingestion;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Chunking
{
    public sealed class ApproximateTokenCounter:ITokenCounter
    {
        public int Count(string text) => text.Length == 0 ? 0 : (text.Length + 3) / 4;
    }
}

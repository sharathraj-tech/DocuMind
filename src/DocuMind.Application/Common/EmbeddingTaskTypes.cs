using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Common
{
    public static class EmbeddingTaskTypes
    {
        public const string Key = "TaskType";                 // key in EmbeddingGenerationOptions.AdditionalProperties
        public const string Document = "RETRIEVAL_DOCUMENT";  // when indexing chunks
        public const string Query = "RETRIEVAL_QUERY";        // when embedding a user question
    }
}

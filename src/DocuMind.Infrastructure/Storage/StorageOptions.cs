using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Storage
{
    public class StorageOptions
    {
        public const string SectionName = "Storage";
        public string ConnectionString { get; set; } = string.Empty;
        public string Container { get; set; } = "documents";
    }
}

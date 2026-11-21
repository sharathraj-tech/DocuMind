using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Documents
{
    public class DocumentContracts
    {
    }

    public sealed record DocumentDto(
        Guid Id,
        string FileName,
        string ContentType,
        long SizeBytes,
        string Status,
        int? PageCount,
        string? Error,
        DateTime CreatedAt);

    public sealed record DocumentFile(Stream Content, string ContentType);

    public static class UploadPolicy
    {
        public const long MaxBytes = 25L * 1024 * 1024;

        // We choose the content type from the extension and never trust the client's header.
        public static readonly IReadOnlyDictionary<string, string> ContentTypes =
            new Dictionary<string, string>
            {
                [".pdf"] = "application/pdf",
                [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                [".md"] = "text/markdown",
                [".txt"] = "text/plain",
                [".html"] = "text/html",
                [".htm"] = "text/html"
            };
    }
}

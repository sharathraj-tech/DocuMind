using Azure;
using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Application.Documents;
using DocuMind.Domain.Entities;
using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DocuMind.Infrastructure.Documents
{
    public sealed class DocumentService(
    AppDbContext db,
    IFileStorage storage,
    IIngestionQueue queue,
    ICurrentTenant tenant,
    ICurrentUser user,
    IOptions<EmbeddingOptions> embedding,
    ILogger<DocumentService> log) : IDocumentService
    {
        public async Task<Result<DocumentDto>> UploadAsync(string fileName, Stream content, CancellationToken ct)
        {
            if (tenant.TenantId is not { } tenantId || user.UserId is not { } userId)
                return Result<DocumentDto>.Fail("Not authenticated.", 401);

            var cleanName = Path.GetFileName(fileName ?? string.Empty);
            if (cleanName.Length > 500) cleanName = cleanName[^500..];

            var ext = Path.GetExtension(cleanName).ToLowerInvariant();
            if (!UploadPolicy.ContentTypes.TryGetValue(ext, out var contentType))
                return Result<DocumentDto>.Fail(
                    $"Unsupported file type. Allowed: {string.Join(", ", UploadPolicy.ContentTypes.Keys)}.", 415);

            if (content.Length == 0)
                return Result<DocumentDto>.Fail("The file is empty.", 400);
            if (content.Length > UploadPolicy.MaxBytes)
                return Result<DocumentDto>.Fail($"File is larger than {UploadPolicy.MaxBytes / 1024 / 1024} MB.", 413);

            if (!await LooksLikeAsync(ext, content, ct))
                return Result<DocumentDto>.Fail("File content does not match its extension.", 400);

            var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, ct));
            content.Position = 0;

            // Dedupe is per tenant: the query filter scopes this to the current workspace.
            var existingId = await db.Documents
                .Where(d => d.ContentHash == hash)
                .Select(d => d.Id)
                .FirstOrDefaultAsync(ct);
            if (existingId != Guid.Empty)
                return Result<DocumentDto>.Fail($"This file was already uploaded (document {existingId}).", 409);

            var id = Guid.NewGuid();
            var doc = new Document
            {
                Id = id,
                TenantId = tenantId,
                FileName = cleanName,
                ContentType = contentType,
                SizeBytes = content.Length,
                BlobName = $"{tenantId:N}/{id:N}",
                ContentHash = hash,
                UploadedByUserId = userId
            };

            await storage.UploadAsync(doc.BlobName, content, contentType, ct);

            try
            {
                db.Documents.Add(doc);
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Two identical uploads raced past the check above; the unique index caught it.
                await storage.DeleteAsync(doc.BlobName, CancellationToken.None);
                return Result<DocumentDto>.Fail("This file was already uploaded.", 409);
            }
            catch
            {
                await storage.DeleteAsync(doc.BlobName, CancellationToken.None);   // no orphan blobs
                throw;
            }

            await queue.EnqueueAsync(new IngestionJob(tenantId, id), ct);
            return Result<DocumentDto>.Ok(ToDto(doc));
        }

        public async Task<IReadOnlyList<DocumentDto>> ListAsync(int skip, int take, CancellationToken ct)
        {
            var docs = await db.Documents
                .OrderByDescending(d => d.CreatedAt)
                .Skip(Math.Max(skip, 0))
                .Take(Math.Clamp(take, 1, 100))
                .ToListAsync(ct);
            return docs.Select(ToDto).ToList();
        }

        public async Task<Result<DocumentDto>> GetAsync(Guid id, CancellationToken ct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            return doc is null
                ? Result<DocumentDto>.Fail("Document not found.", 404)
                : Result<DocumentDto>.Ok(ToDto(doc));
        }

        public async Task<Result<DocumentFile>> OpenFileAsync(Guid id, CancellationToken ct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null)
                return Result<DocumentFile>.Fail("Document not found.", 404);

            try
            {
                var stream = await storage.OpenReadAsync(doc.BlobName, ct);
                return Result<DocumentFile>.Ok(new DocumentFile(stream, doc.ContentType));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                log.LogError("Blob {BlobName} is missing for document {DocumentId}", doc.BlobName, id);
                return Result<DocumentFile>.Fail("The stored file is missing.", 404);
            }
        }

        public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null)
                return Result<bool>.Fail("Document not found.", 404);

            if (!user.IsAdmin && doc.UploadedByUserId != user.UserId)
                return Result<bool>.Fail("Only the uploader or a workspace admin can delete this document.", 403);

            // Chunks go with it (database cascade). Citations keep their snapshot and lose the chunk link.
            db.Documents.Remove(doc);
            await db.SaveChangesAsync(ct);

            try
            {
                await storage.DeleteAsync(doc.BlobName, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Document {DocumentId} deleted but blob {BlobName} could not be removed", id, doc.BlobName);
            }

            return Result<bool>.Ok(true);
        }

        private static async Task<bool> LooksLikeAsync(string ext, Stream s, CancellationToken ct)
        {
            var header = new byte[5];
            var read = await s.ReadAsync(header, ct);
            s.Position = 0;

            return ext switch
            {
                ".pdf" => read >= 5 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44
                                    && header[3] == 0x46 && header[4] == 0x2D,                       // %PDF-
                ".docx" => read >= 4 && header[0] == 0x50 && header[1] == 0x4B
                                     && header[2] == 0x03 && header[3] == 0x04,                       // PK\x03\x04 (zip)
                _ => true
            };
        }

        private static DocumentDto ToDto(Document d) => new(
            d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status.ToString(), d.PageCount, d.Error, d.CreatedAt);

        public async Task<Result<DocumentDto>> ReindexAsync(Guid id, CancellationToken ct)
        {
            var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (doc is null)
                return Result<DocumentDto>.Fail("Document not found.", 404);

            if (!user.IsAdmin && doc.UploadedByUserId != user.UserId)
                return Result<DocumentDto>.Fail("Only the uploader or a workspace admin can reindex this document.", 403);

            if (doc.Status is DocumentStatus.Pending or DocumentStatus.Processing)
                return Result<DocumentDto>.Fail("This document is already being processed.", 409);

            doc.Status = DocumentStatus.Pending;
            doc.Error = null;
            await db.SaveChangesAsync(ct);

            await queue.EnqueueAsync(new IngestionJob(doc.TenantId, doc.Id), ct);
            return Result<DocumentDto>.Ok(ToDto(doc));
        }

        public async Task<Result<int>> ReindexOutdatedAsync(CancellationToken ct)
        {
            if (!user.IsAdmin)
                return Result<int>.Fail("Only workspace admins can reindex outdated documents.", 403);

            var tag = embedding.Value.Tag;

            var outdated = await db.Documents
                .Where(d => (d.Status == DocumentStatus.Ready || d.Status == DocumentStatus.Failed)
                            && d.Chunks.Any(c => c.Embedding == null || c.EmbeddingModel != tag))
                .ToListAsync(ct);

            foreach (var doc in outdated)
            {
                doc.Status = DocumentStatus.Pending;
                doc.Error = null;
            }
            await db.SaveChangesAsync(ct);

            foreach (var doc in outdated)
                await queue.EnqueueAsync(new IngestionJob(doc.TenantId, doc.Id), ct);

            return Result<int>.Ok(outdated.Count);
        }
    }
}

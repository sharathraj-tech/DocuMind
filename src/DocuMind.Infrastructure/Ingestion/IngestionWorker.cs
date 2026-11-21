using DocuMind.Application.Abstractions;
using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Ingestion
{
    public sealed class IngestionWorker(
    ChannelIngestionQueue queue,
    IServiceScopeFactory scopes,
    ILogger<IngestionWorker> log) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeueUnfinishedAsync(stoppingToken);

            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
                await ProcessAsync(job, stoppingToken);
        }

        // After a restart, pick up anything that was queued or half-processed.
        private async Task RequeueUnfinishedAsync(CancellationToken ct)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Cross-tenant on purpose: this is the one place that must see every tenant's documents.
                var unfinished = await db.Documents
                    .IgnoreQueryFilters()
                    .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Processing)
                    .Select(d => new IngestionJob(d.TenantId, d.Id))
                    .ToListAsync(ct);

                foreach (var job in unfinished)
                    await queue.EnqueueAsync(job, ct);

                if (unfinished.Count > 0)
                    log.LogInformation("Requeued {Count} unfinished documents", unfinished.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Could not requeue unfinished documents at startup");
            }
        }

        private async Task ProcessAsync(IngestionJob job, CancellationToken ct)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sp = scope.ServiceProvider;

                // Background jobs set the tenant explicitly (see TenantContext).
                sp.GetRequiredService<TenantContext>().TenantId = job.TenantId;
                var db = sp.GetRequiredService<AppDbContext>();

                var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == job.DocumentId, ct);
                if (doc is null) return;   // deleted before we got to it

                doc.Status = DocumentStatus.Processing;
                doc.Error = null;
                await db.SaveChangesAsync(ct);

                try
                {
                    await sp.GetRequiredService<IDocumentIngestor>().IngestAsync(doc, ct);
                    doc.Status = DocumentStatus.Ready;
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Ingestion failed for document {DocumentId}", job.DocumentId);

                    db.ChangeTracker.Clear();   // drop any half-saved state before recording the failure
                    var message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;

                    await db.Documents
                        .Where(d => d.Id == job.DocumentId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(d => d.Status, DocumentStatus.Failed)
                            .SetProperty(d => d.Error, message), ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Ingestion job for document {DocumentId} crashed", job.DocumentId);
            }
        }
    }
}

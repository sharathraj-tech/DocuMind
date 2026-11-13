using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Domain.Common;
using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Persistence
{
    public class AppDbContext:IdentityDbContext<ApplicationUser,IdentityRole<Guid>,Guid>
    {
        private readonly ICurrentTenant _tenant;
        private readonly int _dimensions;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentTenant tenant,
            IOptions<EmbeddingOptions> embedding
            ):base(options)
        {
            _tenant = tenant;
            _dimensions = embedding.Value.Dimensions;
        }

        private Guid? CurrentTenantId => _tenant.TenantId;

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Citation> Citations => Set<Citation>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            if (_dimensions is < 1 or > 2000)
                throw new InvalidOperationException("Embedding:Dimensions must be between 1 and 2000  (pgvector HNSW limit for the vector type).");
            builder.HasPostgresExtension("vector");
            builder.Entity<Tenant>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.Slug).HasMaxLength(100);
                e.HasIndex(x => x.Slug).IsUnique();
            });

            builder.Entity<TenantMember>(e =>
            {
                e.HasKey(x => new { x.TenantId, x.UserId });
                e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
                e.HasOne(x=>x.Tenant).WithMany(t=>t.Members).HasForeignKey(x=>x.TenantId);
                e.HasIndex(x => x.UserId);
            });

            builder.Entity<Document>(e =>
            {
                e.Property(x => x.FileName).HasMaxLength(500);
                e.Property(x => x.ContentType).HasMaxLength(200);
                e.Property(x=>x.BlobName).HasMaxLength(500);
                e.Property(x=>x.ContentHash).HasMaxLength(64);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                e.HasOne<Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.Status });
                e.HasIndex(x => new {x.TenantId,x.ContentHash});
                e.HasMany(x => x.Chunks).WithOne(c => c.Document)
                    .HasForeignKey(c => c.DocumentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DocumentChunk>(e =>
            {
                e.Property(x => x.Embedding).HasColumnType($"vector({_dimensions})");
                e.Property(x => x.EmbeddingModel).HasMaxLength(100);
                e.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
                e.HasIndex(x => x.TenantId);
                e.HasIndex(x => x.Embedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops")
                    .HasStorageParameter("m", 16)
                    .HasStorageParameter("ef_construction", 64);
            });

            builder.Entity<Conversation>(e =>
            {
                e.Property(x => x.Title).HasMaxLength(300);
                e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.UserId });
                e.HasMany(x => x.Messages).WithOne(m => m.Conversation)
                    .HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Message>(e =>
            {
                e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
                e.Property(x => x.Model).HasMaxLength(100);
                e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
                e.HasMany(x => x.Citations).WithOne(c => c.Message)
                .HasForeignKey(c => c.ChunkId).OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Citation>(e =>
            {
                e.Property(x => x.DocumentName).HasMaxLength(500);
                e.HasOne<DocumentChunk>().WithMany()
                    .HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Feedback>(e =>
            {
                e.Property(x => x.Comment).HasMaxLength(2000);
                e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
            });

            builder.Entity<RefreshToken>(e =>
            {
                e.Property(x => x.TokenHash).HasMaxLength(64);
                e.HasIndex(x => x.TokenHash).IsUnique();
                e.HasIndex(x => x.UserId);
                e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            });

            ApplyTenantFilter<Document>(builder);
            ApplyTenantFilter<DocumentChunk>(builder);
            ApplyTenantFilter<Conversation>(builder);
            ApplyTenantFilter<Message>(builder);
            ApplyTenantFilter<Citation>(builder);
            ApplyTenantFilter<Feedback>(builder);
            
        }

        private void ApplyTenantFilter<T>(ModelBuilder b) where T : class, ITenantOwned
            => b.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        //public override Task<int> SaveChangesASync(bool acceptAllChangesOnSuccess,CancellationToken ct = default)
        //{
        //    StampTenant();
        //    return base.SaveChanges(acceptAllChangesOnSuccess);
        //}

        private void StampTenant()
        {
            foreach(var entry in ChangeTracker.Entries<ITenantOwned>()
                .Where(e=>e.State==EntityState.Added))
            {
                if(entry.Entity.TenantId==Guid.Empty)
                {
                    entry.Entity.TenantId = CurrentTenantId
                        ?? throw new InvalidOperationException("Cannot save tenant data without a current tenant.");

                }
                else if(CurrentTenantId is { } current && entry.Entity.TenantId!=current)
                {
                    throw new InvalidOperationException("Cross-tenant write blocked.");
                }
            }
        }

    }
}

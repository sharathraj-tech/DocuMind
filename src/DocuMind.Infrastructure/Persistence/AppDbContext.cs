using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
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
        }

    }
}

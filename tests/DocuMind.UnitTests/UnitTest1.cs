using DocuMind.Application.Common;
using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace DocuMind.UnitTests;

public class UnitTest1
{
    [Fact]
    public void CitationRelationshipsUseTheirDedicatedForeignKeys()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused", npgsql => npgsql.UseVector())
            .Options;
        using var db = new AppDbContext(options, new TenantContext(), Options.Create(new EmbeddingOptions { Dimensions = 1 }));

        var citation = db.Model.FindEntityType(typeof(Citation))!;
        var messageForeignKey = citation.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Message));
        var chunkForeignKey = citation.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(DocumentChunk));

        messageForeignKey.Properties.Select(property => property.Name)
            .Should().Equal(nameof(Citation.MessageId));
        messageForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        chunkForeignKey.Properties.Select(property => property.Name)
            .Should().Equal(nameof(Citation.ChunkId));
        chunkForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.SetNull);
    }
}

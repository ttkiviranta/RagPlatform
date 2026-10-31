using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentMetadata> DocumentMetadata => Set<DocumentMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(document => document.Content)
                .IsRequired();
            entity.Property(document => document.FileName)
                .HasMaxLength(255);
            entity.Property(document => document.ContentType)
                .HasMaxLength(200);
            entity.Property(document => document.FileSize)
                .IsRequired();
            entity.Property(document => document.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(document => document.Metadata)
                .WithOne(metadata => metadata.Document)
                .HasForeignKey<DocumentMetadata>(metadata => metadata.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentMetadata>(entity =>
        {
            entity.HasKey(metadata => metadata.Id);
            entity.Property(metadata => metadata.DocumentId)
                .IsRequired();
            entity.Property(metadata => metadata.FileName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(metadata => metadata.ContentType)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(metadata => metadata.CharacterCount)
                .IsRequired();
            entity.Property(metadata => metadata.WordCount)
                .IsRequired();
            entity.Property(metadata => metadata.LineCount)
                .IsRequired();
            entity.Property(metadata => metadata.ProcessedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(metadata => metadata.DocumentId)
                .IsUnique();
        });
    }
}

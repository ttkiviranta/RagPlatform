using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
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

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<DocumentEmbedding> DocumentEmbeddings => Set<DocumentEmbedding>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

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
            entity.Property(document => document.SourceUrl)
                .HasMaxLength(2048)
                .HasDefaultValue(string.Empty);
            entity.Property(document => document.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(document => document.TenantId)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(document => document.OwnerUserId);
            entity.HasOne(document => document.Owner)
                .WithMany(user => user.OwnedDocuments)
                .HasForeignKey(document => document.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(document => new { document.TenantId, document.OwnerUserId, document.CreatedAt });
            entity.HasOne(document => document.Metadata)
                .WithOne(metadata => metadata.Document)
                .HasForeignKey<DocumentMetadata>(metadata => metadata.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(document => document.Chunks)
                .WithOne(chunk => chunk.Document)
                .HasForeignKey(chunk => chunk.DocumentId)
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

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.DocumentId)
                .IsRequired();
            entity.Property(chunk => chunk.ChunkIndex)
                .IsRequired();
            entity.Property(chunk => chunk.Content)
                .IsRequired();
            entity.Property(chunk => chunk.CharacterCount)
                .IsRequired();
            entity.Property(chunk => chunk.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
                .IsUnique();
            entity.HasMany(chunk => chunk.Embeddings)
                .WithOne(embedding => embedding.Chunk)
                .HasForeignKey(embedding => embedding.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentEmbedding>(entity =>
        {
            entity.HasKey(embedding => embedding.Id);
            entity.Property(embedding => embedding.ChunkId)
                .IsRequired();
            entity.Property(embedding => embedding.EmbeddingVector)
                .HasColumnType("vector")
                .IsRequired();
            entity.Property(embedding => embedding.VectorDimensions)
                .IsRequired();
            entity.Property(embedding => embedding.ModelName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(embedding => embedding.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(embedding => new { embedding.ChunkId, embedding.ModelName })
                .IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.EntraObjectId)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(user => user.Email)
                .HasMaxLength(255);
            entity.Property(user => user.UserName)
                .HasMaxLength(255);
            entity.Property(user => user.FirstName)
                .HasMaxLength(100);
            entity.Property(user => user.LastName)
                .HasMaxLength(100);
            entity.Property(user => user.IdentityProvider)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("MicrosoftEntraId");
            entity.Property(user => user.TenantId)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(user => user.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(user => user.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(user => user.LastLoginAt);
            entity.HasIndex(user => new { user.TenantId, user.EntraObjectId })
                .IsUnique();
            entity.HasIndex(user => user.Email)
                .IsUnique(false);
            entity.HasIndex(user => user.TenantId);
            entity.HasMany(user => user.UserRoles)
                .WithOne(userRole => userRole.User)
                .HasForeignKey(userRole => userRole.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(role => role.Name);
            entity.Property(role => role.Name)
                .HasMaxLength(50);
            entity.Property(role => role.Description)
                .IsRequired()
                .HasMaxLength(200);
            entity.HasData(
                new Role { Name = RoleNames.User, Description = "Access to owned documents." },
                new Role { Name = RoleNames.PowerUser, Description = "Tenant-wide document management." },
                new Role { Name = RoleNames.SuperUser, Description = "Tenant-wide document and role administration." });
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(userRole => new { userRole.UserId, userRole.RoleName });
            entity.Property(userRole => userRole.RoleName)
                .HasMaxLength(50);
            entity.Property(userRole => userRole.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(userRole => userRole.Role)
                .WithMany(role => role.UserRoles)
                .HasForeignKey(userRole => userRole.RoleName)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(conversation => conversation.Id);
            entity.Property(conversation => conversation.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(conversation => conversation.UserId)
                .IsRequired();
            entity.Property(conversation => conversation.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(conversation => conversation.UpdatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(conversation => new { conversation.UserId, conversation.UpdatedAtUtc });
            entity.HasOne(conversation => conversation.User)
                .WithMany(user => user.Conversations)
                .HasForeignKey(conversation => conversation.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(conversation => conversation.Messages)
                .WithOne(message => message.Conversation)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.ToTable("ConversationMessages", table => table.HasCheckConstraint(
                "CK_ConversationMessages_Role",
                "\"Role\" IN ('User', 'Assistant', 'System')"));
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(message => message.Content)
                .IsRequired();
            entity.Property(message => message.CreatedAtUtc)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAtUtc, message.Id });
        });
    }
}

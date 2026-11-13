using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private const int MaxEmbeddingPersistenceAttempts = 3;
    private const string EmbeddingUniqueIndexName = "IX_DocumentEmbeddings_ChunkId_ModelName";

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly FoundryOptions _foundryOptions;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IEmbeddingProvider embeddingProvider,
        IAuditEventWriter auditEventWriter,
        IOptions<FoundryOptions> foundryOptions,
        ITelemetryService telemetryService,
        ILogger<EmbeddingService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _embeddingProvider = embeddingProvider;
        _auditEventWriter = auditEventWriter;
        _foundryOptions = foundryOptions.Value;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task<DocumentEmbedding> GenerateEmbeddingAsync(
        Guid chunkId,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var documentId = await _dbContext.DocumentChunks
                .Where(chunk => chunk.Id == chunkId)
                .Select(chunk => (Guid?)chunk.DocumentId)
                .FirstOrDefaultAsync(cancellationToken);
            if (documentId is null)
            {
                throw new KeyNotFoundException($"Chunk {chunkId} was not found.");
            }

            if (await _documentAccessService.GetAccessibleDocumentAsync(
                    documentId.Value,
                    DocumentPermission.Write,
                    cancellationToken) is null)
            {
                throw new KeyNotFoundException($"Chunk {chunkId} was not found.");
            }

            var chunk = await _dbContext.DocumentChunks
                .FirstAsync(item => item.Id == chunkId, cancellationToken);
            var resolvedModelName = ResolveModelName(modelName);
            var vector = await GenerateVectorAsync(
                chunk.Content,
                resolvedModelName,
                expectedDimensions: null,
                cancellationToken);

            var embedding = new DocumentEmbedding
            {
                ChunkId = chunkId,
                EmbeddingVector = new Vector(vector.ToArray()),
                VectorDimensions = vector.Count,
                ModelName = resolvedModelName,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.DocumentEmbeddings.Add(embedding);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated embedding for chunk {ChunkId} using model {ModelName} with {VectorDimensions} dimensions.",
                chunkId,
                resolvedModelName,
                embedding.VectorDimensions);

            _telemetryService.TrackEmbeddingGeneration(
                resolvedModelName,
                stopwatch.Elapsed.TotalMilliseconds,
                embedding.VectorDimensions,
                true);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation failed for chunk {ChunkId}.", chunkId);
            _telemetryService.TrackEmbeddingGeneration(
                modelName ?? "unknown",
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                false);
            throw;
        }
    }

    public Task<IReadOnlyList<DocumentEmbedding>> PrepareEmbeddingsForImportAsync(
        Document validatedDocument,
        CancellationToken cancellationToken = default)
    {
        return PrepareEmbeddingsForImportAsync(validatedDocument, batchCounts: null, cancellationToken);
    }

    private async Task<IReadOnlyList<DocumentEmbedding>> PrepareEmbeddingsForImportAsync(
        Document validatedDocument,
        EmbeddingBatchCounts? batchCounts,
        CancellationToken cancellationToken)
    {
        var expectedDimensions = _foundryOptions.EmbeddingDimensions;
        if (expectedDimensions <= 0)
        {
            throw new InvalidOperationException("Foundry:EmbeddingDimensions must be greater than zero.");
        }

        var modelName = ResolveModelName(modelName: null);
        var chunks = await GetValidatedChunksForImportAsync(validatedDocument, cancellationToken);
        var chunkIds = chunks.Select(chunk => chunk.Id).ToArray();
        var existingEmbeddings = await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .Where(embedding => chunkIds.Contains(embedding.ChunkId)
                && embedding.ModelName == modelName)
            .ToListAsync(cancellationToken);
        var existingByChunkId = existingEmbeddings.ToDictionary(embedding => embedding.ChunkId);
        var preparedEmbeddings = new List<DocumentEmbedding>(chunks.Count);

        foreach (var chunk in chunks)
        {
            if (existingByChunkId.TryGetValue(chunk.Id, out var existingEmbedding))
            {
                ValidateStoredEmbedding(existingEmbedding, expectedDimensions);
                preparedEmbeddings.Add(existingEmbedding);
                if (batchCounts is not null)
                {
                    batchCounts.ReusedEmbeddingCount++;
                }

                continue;
            }

            var vector = await GenerateVectorForImportAsync(
                chunk.Content,
                modelName,
                expectedDimensions,
                cancellationToken);
            if (batchCounts is not null)
            {
                batchCounts.GeneratedEmbeddingCount++;
            }

            preparedEmbeddings.Add(new DocumentEmbedding
            {
                ChunkId = chunk.Id,
                EmbeddingVector = new Vector(vector.ToArray()),
                VectorDimensions = vector.Count,
                ModelName = modelName,
                CreatedAt = DateTime.UtcNow
            });
        }

        return preparedEmbeddings;
    }

    public async Task<IReadOnlyList<DocumentEmbedding>> GenerateAndPersistEmbeddingsForImportAsync(
        Document validatedDocument,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchCounts = new EmbeddingBatchCounts();
        var modelName = "unknown";
        EmbeddingPersistenceResult persistenceResult;

        try
        {
            modelName = ResolveModelName(modelName: null);
            var preparedEmbeddings = await PrepareEmbeddingsForImportAsync(
                validatedDocument,
                batchCounts,
                cancellationToken);
            persistenceResult = await PersistPreparedEmbeddingsForImportAsync(
                validatedDocument,
                preparedEmbeddings,
                modelName,
                stopwatch,
                cancellationToken);
        }
        catch
        {
            _telemetryService.TrackEmbeddingBatch(
                modelName,
                stopwatch.Elapsed.TotalMilliseconds,
                success: false,
                batchCounts.GeneratedEmbeddingCount,
                batchCounts.ReusedEmbeddingCount);
            throw;
        }

        if (persistenceResult.SuccessAuditCreated)
        {
            _telemetryService.TrackEmbeddingBatch(
                modelName,
                stopwatch.Elapsed.TotalMilliseconds,
                success: true,
                persistenceResult.GeneratedEmbeddingCount,
                persistenceResult.ReusedEmbeddingCount);
        }

        return persistenceResult.Embeddings;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetValidatedChunksForImportAsync(
        Document validatedDocument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validatedDocument);

        var document = await _dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == validatedDocument.Id, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Validated document {validatedDocument.Id} was not found.");
        }

        if (!string.Equals(document.TenantId, validatedDocument.TenantId, StringComparison.Ordinal)
            || document.OwnerUserId != validatedDocument.OwnerUserId
            || !string.Equals(document.StagingBlobKey, validatedDocument.StagingBlobKey, StringComparison.Ordinal)
            || document.ImportStatus != validatedDocument.ImportStatus
            || !string.Equals(document.Content, validatedDocument.Content, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Validated document {validatedDocument.Id} no longer matches its persisted record.");
        }

        if (document.ImportStatus != DocumentImportStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Document {document.Id} must be in Processing status before import embeddings can be prepared.");
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            throw new InvalidOperationException("Document content is empty and cannot be embedded.");
        }

        var chunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == document.Id)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException($"Document {document.Id} has no persisted chunks to embed.");
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            if (chunk.ChunkIndex != index
                || string.IsNullOrWhiteSpace(chunk.Content)
                || chunk.CharacterCount != chunk.Content.Length)
            {
                throw new InvalidDataException($"Document {document.Id} has an invalid persisted chunk set.");
            }
        }

        var documentTargetId = document.Id.ToString();
        var hasMatchingChunkAudit = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentChunksGenerated
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentTargetId
                && auditEvent.ResultCount == chunks.Count,
            cancellationToken);
        if (!hasMatchingChunkAudit)
        {
            throw new InvalidDataException(
                $"Document {document.Id} does not have a matching persisted chunk-generation audit event.");
        }

        return chunks;
    }

    public async Task<IReadOnlyList<DocumentEmbedding>> GetEmbeddingsForChunkAsync(
        Guid chunkId,
        CancellationToken cancellationToken = default)
    {
        var documentId = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(item => item.Id == chunkId)
            .Select(item => (Guid?)item.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (documentId is null || await _documentAccessService.GetAccessibleDocumentAsync(
                documentId.Value,
                DocumentPermission.Read,
                cancellationToken) is null)
        {
            return Array.Empty<DocumentEmbedding>();
        }

        return await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .Where(embedding => embedding.ChunkId == chunkId)
            .OrderBy(embedding => embedding.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentEmbedding?> GetByIdAsync(
        Guid embeddingId,
        CancellationToken cancellationToken = default)
    {
        var chunkId = await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .Where(embedding => embedding.Id == embeddingId)
            .Select(embedding => (Guid?)embedding.ChunkId)
            .FirstOrDefaultAsync(cancellationToken);
        if (chunkId is null)
        {
            return null;
        }

        var documentId = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(item => item.Id == chunkId.Value)
            .Select(item => (Guid?)item.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (documentId is null || await _documentAccessService.GetAccessibleDocumentAsync(
                documentId.Value,
                DocumentPermission.Read,
                cancellationToken) is null)
        {
            return null;
        }

        return await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(embedding => embedding.Id == embeddingId, cancellationToken);
    }

    private async Task<EmbeddingPersistenceResult> PersistPreparedEmbeddingsForImportAsync(
        Document validatedDocument,
        IReadOnlyList<DocumentEmbedding> preparedEmbeddings,
        string modelName,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var chunkIds = preparedEmbeddings.Select(embedding => embedding.ChunkId).ToArray();
        for (var attempt = 0; attempt < MaxEmbeddingPersistenceAttempts; attempt++)
        {
            await using var transaction = _dbContext.Database.IsNpgsql()
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var pendingEmbeddings = new List<DocumentEmbedding>();
            AuditEvent? pendingSuccessAudit = null;

            try
            {
                var auditDocument = validatedDocument;
                if (transaction is not null)
                {
                    var lockedDocuments = await _dbContext.Documents
                        .FromSqlInterpolated($"SELECT * FROM \"Documents\" WHERE \"Id\" = {validatedDocument.Id} FOR UPDATE")
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);
                    auditDocument = lockedDocuments.SingleOrDefault()
                        ?? throw new KeyNotFoundException($"Validated document {validatedDocument.Id} was not found.");

                    if (!string.Equals(auditDocument.TenantId, validatedDocument.TenantId, StringComparison.Ordinal)
                        || auditDocument.OwnerUserId != validatedDocument.OwnerUserId
                        || !string.Equals(auditDocument.StagingBlobKey, validatedDocument.StagingBlobKey, StringComparison.Ordinal)
                        || auditDocument.ImportStatus != DocumentImportStatus.Processing
                        || !string.Equals(auditDocument.Content, validatedDocument.Content, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Document {validatedDocument.Id} changed before its embeddings could be persisted.");
                    }
                }

                var existingEmbeddings = await _dbContext.DocumentEmbeddings
                    .AsNoTracking()
                    .Where(embedding => chunkIds.Contains(embedding.ChunkId)
                        && embedding.ModelName == modelName)
                    .ToListAsync(cancellationToken);
                var existingByChunkId = existingEmbeddings.ToDictionary(embedding => embedding.ChunkId);
                var resultByChunkId = new Dictionary<Guid, DocumentEmbedding>(preparedEmbeddings.Count);
                var expectedDimensions = _foundryOptions.EmbeddingDimensions;

                foreach (var preparedEmbedding in preparedEmbeddings)
                {
                    if (existingByChunkId.TryGetValue(preparedEmbedding.ChunkId, out var existingEmbedding))
                    {
                        ValidateStoredEmbedding(existingEmbedding, expectedDimensions);
                        resultByChunkId.Add(preparedEmbedding.ChunkId, existingEmbedding);
                        continue;
                    }

                    ValidateStoredEmbedding(preparedEmbedding, expectedDimensions);
                    pendingEmbeddings.Add(preparedEmbedding);
                    resultByChunkId.Add(preparedEmbedding.ChunkId, preparedEmbedding);
                }

                var generatedEmbeddingCount = pendingEmbeddings.Count;
                var reusedEmbeddingCount = existingByChunkId.Count;
                pendingSuccessAudit = await AddEmbeddingSuccessAuditIfMissingAsync(
                    auditDocument,
                    modelName,
                    generatedEmbeddingCount,
                    reusedEmbeddingCount,
                    stopwatch.Elapsed.TotalMilliseconds,
                    cancellationToken);

                if (pendingEmbeddings.Count > 0 || pendingSuccessAudit is not null)
                {
                    _dbContext.DocumentEmbeddings.AddRange(pendingEmbeddings);
                    await _auditEventWriter.FlushAsync(cancellationToken);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new EmbeddingPersistenceResult(
                    preparedEmbeddings.Select(embedding => resultByChunkId[embedding.ChunkId]).ToArray(),
                    pendingSuccessAudit is not null,
                    generatedEmbeddingCount,
                    reusedEmbeddingCount);
            }
            catch (DbUpdateException exception) when (IsEmbeddingUniquenessConflict(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                DetachPendingEmbeddings(pendingEmbeddings, pendingSuccessAudit);
                if (attempt == MaxEmbeddingPersistenceAttempts - 1)
                {
                    throw;
                }

                _logger.LogInformation(
                    "Embedding persistence encountered a uniqueness conflict for document {DocumentId}; retrying with persisted embeddings.",
                    validatedDocument.Id);
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                DetachPendingEmbeddings(pendingEmbeddings, pendingSuccessAudit);
                throw;
            }
        }

        throw new InvalidOperationException("Embedding persistence did not complete after retrying uniqueness conflicts.");
    }

    private async Task<AuditEvent?> AddEmbeddingSuccessAuditIfMissingAsync(
        Document document,
        string modelName,
        int generatedEmbeddingCount,
        int reusedEmbeddingCount,
        double durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var documentId = document.Id.ToString();
        var auditExists = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentEmbeddingsGenerated
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentId
                && auditEvent.ModelDeployment == modelName,
            cancellationToken);
        if (auditExists)
        {
            return null;
        }

        _auditEventWriter.Add(
            document.OwnerUserId,
            document.TenantId,
            AuditEventTypes.DocumentEmbeddingsGenerated,
            "Succeeded",
            targetType: "Document",
            targetId: documentId,
            durationMilliseconds: durationMilliseconds,
            resultCount: generatedEmbeddingCount,
            modelProvider: _embeddingProvider.ProviderName,
            modelDeployment: modelName,
            modelOperation: "EmbeddingGeneration",
            generatedEmbeddingCount: generatedEmbeddingCount,
            reusedEmbeddingCount: reusedEmbeddingCount);

        return _dbContext.ChangeTracker.Entries<AuditEvent>()
            .Single(entry => entry.State == EntityState.Added
                && entry.Entity.EventType == AuditEventTypes.DocumentEmbeddingsGenerated
                && entry.Entity.TargetType == "Document"
                && entry.Entity.TargetId == documentId
                && entry.Entity.ModelDeployment == modelName)
            .Entity;
    }

    private void DetachPendingEmbeddings(
        IReadOnlyList<DocumentEmbedding> pendingEmbeddings,
        AuditEvent? pendingSuccessAudit)
    {
        foreach (var embedding in pendingEmbeddings)
        {
            _dbContext.Entry(embedding).State = EntityState.Detached;
        }

        if (pendingSuccessAudit is not null)
        {
            _dbContext.Entry(pendingSuccessAudit).State = EntityState.Detached;
        }
    }

    private async Task<IReadOnlyList<float>> GenerateVectorForImportAsync(
        string content,
        string modelName,
        int expectedDimensions,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var vector = await GenerateVectorAsync(content, modelName, expectedDimensions, cancellationToken);
            _telemetryService.TrackEmbeddingGeneration(
                modelName,
                stopwatch.Elapsed.TotalMilliseconds,
                vector.Count,
                success: true);
            return vector;
        }
        catch
        {
            _telemetryService.TrackEmbeddingGeneration(
                modelName,
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                success: false);
            throw;
        }
    }

    private string ResolveModelName(string? modelName)
    {
        var resolvedModelName = string.IsNullOrWhiteSpace(modelName)
            ? _embeddingProvider.ModelName
            : modelName.Trim();
        if (!string.Equals(resolvedModelName, _embeddingProvider.ModelName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Only the configured embedding deployment '{_embeddingProvider.ModelName}' can be used.",
                nameof(modelName));
        }

        return resolvedModelName;
    }

    private async Task<IReadOnlyList<float>> GenerateVectorAsync(
        string content,
        string modelName,
        int? expectedDimensions,
        CancellationToken cancellationToken)
    {
        var vector = await _embeddingProvider.GenerateEmbeddingAsync(content, modelName, cancellationToken);
        if (vector.Count == 0)
        {
            throw new InvalidOperationException("Embedding provider returned an empty vector.");
        }

        if (expectedDimensions is int dimensions && vector.Count != dimensions)
        {
            throw new InvalidDataException(
                $"Embedding provider returned {vector.Count} dimensions; the configured dimension is {dimensions}.");
        }

        return vector;
    }

    private static void ValidateStoredEmbedding(DocumentEmbedding embedding, int expectedDimensions)
    {
        var vectorDimensions = embedding.EmbeddingVector.ToArray().Length;
        if (vectorDimensions == 0
            || vectorDimensions != expectedDimensions
            || vectorDimensions != embedding.VectorDimensions)
        {
            throw new InvalidDataException(
                $"Stored embedding {embedding.Id} has invalid vector dimensions.");
        }
    }

    private static bool IsEmbeddingUniquenessConflict(DbUpdateException exception)
    {
        for (var innerException = exception.InnerException; innerException is not null; innerException = innerException.InnerException)
        {
            if (innerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: EmbeddingUniqueIndexName
                })
            {
                return true;
            }
        }

        return false;
    }

    private sealed class EmbeddingBatchCounts
    {
        public int GeneratedEmbeddingCount { get; set; }

        public int ReusedEmbeddingCount { get; set; }
    }

    private sealed record EmbeddingPersistenceResult(
        IReadOnlyList<DocumentEmbedding> Embeddings,
        bool SuccessAuditCreated,
        int GeneratedEmbeddingCount,
        int ReusedEmbeddingCount);
}

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentChunkingService : IDocumentChunkingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<DocumentChunkingService> _logger;

    public DocumentChunkingService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IAuditEventWriter auditEventWriter,
        ITelemetryService telemetryService,
        ILogger<DocumentChunkingService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _auditEventWriter = auditEventWriter;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public Task<IReadOnlyList<DocumentChunk>> GenerateChunksAsync(
        Guid documentId,
        int chunkSize,
        int chunkOverlap,
        CancellationToken cancellationToken = default) => ExecuteWithChunkingTelemetryAsync(async telemetryCounts =>
        {
            ValidateChunkOptions(chunkSize, chunkOverlap);

            var document = await _documentAccessService.GetAccessibleDocumentAsync(
                documentId,
                DocumentPermission.Write,
                cancellationToken);

            if (document is null)
            {
                throw new KeyNotFoundException($"Document {documentId} was not found.");
            }

            return await GenerateChunksCoreAsync(document, chunkSize, chunkOverlap, telemetryCounts, cancellationToken);
        });

    public Task<IReadOnlyList<DocumentChunk>> GenerateChunksForImportAsync(
        Document validatedDocument,
        int chunkSize,
        int chunkOverlap,
        CancellationToken cancellationToken = default) => ExecuteWithChunkingTelemetryAsync(async telemetryCounts =>
        {
            ArgumentNullException.ThrowIfNull(validatedDocument);
            ValidateChunkOptions(chunkSize, chunkOverlap);

            var document = await _dbContext.Documents
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
                    $"Document {document.Id} must be in Processing status before import chunking.");
            }

            return await GenerateChunksCoreAsync(document, chunkSize, chunkOverlap, telemetryCounts, cancellationToken);
        });

    private async Task<IReadOnlyList<DocumentChunk>> ExecuteWithChunkingTelemetryAsync(
        Func<ChunkingTelemetryCounts, Task<ChunkGenerationResult>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var counts = new ChunkingTelemetryCounts();
        ChunkGenerationResult result;
        try
        {
            result = await operation(counts);
        }
        catch
        {
            _telemetryService.TrackDocumentChunking(
                stopwatch.Elapsed.TotalMilliseconds,
                success: false,
                counts.GeneratedChunkCount,
                counts.ReusedChunkCount);
            throw;
        }

        _telemetryService.TrackDocumentChunking(
            stopwatch.Elapsed.TotalMilliseconds,
            success: true,
            counts.GeneratedChunkCount,
            counts.ReusedChunkCount);
        return result.Chunks;
    }

    private async Task<ChunkGenerationResult> GenerateChunksCoreAsync(
        Document document,
        int chunkSize,
        int chunkOverlap,
        ChunkingTelemetryCounts telemetryCounts,
        CancellationToken cancellationToken)
    {
        var generationStopwatch = Stopwatch.StartNew();
        await using var transaction = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        List<DocumentChunk>? generatedChunks = null;
        AuditEvent? generatedAuditEvent = null;
        var transactionCommitted = false;
        try
        {
            var sourceDocument = document;
            if (transaction is not null)
            {
                var lockedDocuments = await _dbContext.Documents
                    .FromSqlInterpolated($"SELECT * FROM \"Documents\" WHERE \"Id\" = {document.Id} FOR UPDATE")
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);
                sourceDocument = lockedDocuments.SingleOrDefault()
                    ?? throw new KeyNotFoundException($"Document {document.Id} was not found.");

                if (!string.Equals(sourceDocument.Content, document.Content, StringComparison.Ordinal)
                    || sourceDocument.ImportStatus != document.ImportStatus)
                {
                    throw new InvalidDataException(
                        $"Document {document.Id} changed before its chunks could be generated.");
                }
            }

            if (string.IsNullOrWhiteSpace(sourceDocument.Content))
            {
                throw new InvalidOperationException("Document content is empty and cannot be chunked.");
            }

            var expectedChunkContents = CreateChunks(sourceDocument.Content, chunkSize, chunkOverlap);
            var existingChunks = await _dbContext.DocumentChunks
                .AsNoTracking()
                .Where(chunk => chunk.DocumentId == sourceDocument.Id)
                .OrderBy(chunk => chunk.ChunkIndex)
                .ToListAsync(cancellationToken);

            if (existingChunks.Count > 0)
            {
                var completeSet = ValidateExistingChunkSet(existingChunks, expectedChunkContents, sourceDocument.Id);
                telemetryCounts.ReusedChunkCount = completeSet.Count;
                generatedAuditEvent = await AddSuccessAuditIfMissingAsync(
                    sourceDocument,
                    completeSet.Count,
                    generationStopwatch.Elapsed.TotalMilliseconds,
                    cancellationToken);
                if (generatedAuditEvent is not null)
                {
                    await _auditEventWriter.FlushAsync(cancellationToken);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    transactionCommitted = true;
                }

                _logger.LogInformation(
                    "Document {DocumentId} already has a complete set of {ChunkCount} chunks. Reusing them.",
                    sourceDocument.Id,
                    completeSet.Count);

                return new ChunkGenerationResult(completeSet);
            }

            if (expectedChunkContents.Count == 0)
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    transactionCommitted = true;
                }

                return new ChunkGenerationResult(Array.Empty<DocumentChunk>());
            }

            generatedChunks = expectedChunkContents
                .Select((content, index) => new DocumentChunk
                {
                    DocumentId = sourceDocument.Id,
                    ChunkIndex = index,
                    Content = content,
                    CharacterCount = content.Length,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();
            telemetryCounts.GeneratedChunkCount = generatedChunks.Count;

            try
            {
                if (transaction is not null)
                {
                    await transaction.CreateSavepointAsync("ChunkInsert", cancellationToken);
                }

                _dbContext.DocumentChunks.AddRange(generatedChunks);
                generatedAuditEvent = await AddSuccessAuditIfMissingAsync(
                    sourceDocument,
                    generatedChunks.Count,
                    generationStopwatch.Elapsed.TotalMilliseconds,
                    cancellationToken);
                await _auditEventWriter.FlushAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    transactionCommitted = true;
                }
            }
            catch (DbUpdateException exception) when (IsChunkIndexUniquenessConflict(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackToSavepointAsync("ChunkInsert", CancellationToken.None);
                }

                DetachGeneratedEntities(generatedChunks, generatedAuditEvent);
                generatedAuditEvent = null;

                var concurrentlyPersistedChunks = await _dbContext.DocumentChunks
                    .AsNoTracking()
                    .Where(chunk => chunk.DocumentId == sourceDocument.Id)
                    .OrderBy(chunk => chunk.ChunkIndex)
                    .ToListAsync(cancellationToken);
                if (concurrentlyPersistedChunks.Count == 0)
                {
                    throw;
                }

                var completeSet = ValidateExistingChunkSet(
                    concurrentlyPersistedChunks,
                    expectedChunkContents,
                    sourceDocument.Id);
                telemetryCounts.GeneratedChunkCount = 0;
                telemetryCounts.ReusedChunkCount = completeSet.Count;
                generatedAuditEvent = await AddSuccessAuditIfMissingAsync(
                    sourceDocument,
                    completeSet.Count,
                    generationStopwatch.Elapsed.TotalMilliseconds,
                    cancellationToken);
                if (generatedAuditEvent is not null)
                {
                    await _auditEventWriter.FlushAsync(cancellationToken);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    transactionCommitted = true;
                }

                return new ChunkGenerationResult(completeSet);
            }

            _logger.LogInformation(
                "Generated {ChunkCount} chunks for document {DocumentId} with chunk size {ChunkSize} and overlap {ChunkOverlap}.",
                generatedChunks.Count,
                sourceDocument.Id,
                chunkSize,
                chunkOverlap);

            return new ChunkGenerationResult(generatedChunks);
        }
        catch
        {
            if (transaction is not null && !transactionCommitted)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            DetachGeneratedEntities(generatedChunks, generatedAuditEvent);
            throw;
        }
    }

    private async Task<AuditEvent?> AddSuccessAuditIfMissingAsync(
        Document document,
        int chunkCount,
        double durationMilliseconds,
        CancellationToken cancellationToken)
    {
        var documentId = document.Id.ToString();
        var auditExists = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentChunksGenerated
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentId,
            cancellationToken);
        if (auditExists)
        {
            return null;
        }

        _auditEventWriter.Add(
            document.OwnerUserId,
            document.TenantId,
            AuditEventTypes.DocumentChunksGenerated,
            "Succeeded",
            "Document",
            documentId,
            durationMilliseconds: durationMilliseconds,
            resultCount: chunkCount);

        return _dbContext.ChangeTracker.Entries<AuditEvent>()
            .Single(entry => entry.State == EntityState.Added
                && entry.Entity.EventType == AuditEventTypes.DocumentChunksGenerated
                && entry.Entity.TargetType == "Document"
                && entry.Entity.TargetId == documentId)
            .Entity;
    }

    private void DetachGeneratedEntities(
        IReadOnlyList<DocumentChunk>? chunks,
        AuditEvent? auditEvent)
    {
        if (chunks is not null)
        {
            foreach (var chunk in chunks)
            {
                _dbContext.Entry(chunk).State = EntityState.Detached;
            }
        }

        if (auditEvent is not null)
        {
            _dbContext.Entry(auditEvent).State = EntityState.Detached;
        }
    }

    private static IReadOnlyList<DocumentChunk> ValidateExistingChunkSet(
        IReadOnlyList<DocumentChunk> existingChunks,
        IReadOnlyList<string> expectedChunkContents,
        Guid documentId)
    {
        if (existingChunks.Count != expectedChunkContents.Count)
        {
            throw new InvalidDataException(
                $"Document {documentId} has {existingChunks.Count} chunks; {expectedChunkContents.Count} are required. The set will not be regenerated automatically.");
        }

        for (var index = 0; index < existingChunks.Count; index++)
        {
            var chunk = existingChunks[index];
            if (chunk.ChunkIndex != index)
            {
                throw new InvalidDataException(
                    $"Document {documentId} has a missing or non-contiguous chunk index at position {index}.");
            }

            if (chunk.CharacterCount != chunk.Content.Length)
            {
                throw new InvalidDataException(
                    $"Document {documentId} has an inconsistent character count for chunk {chunk.ChunkIndex}.");
            }

            if (!string.Equals(chunk.Content, expectedChunkContents[index], StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Document {documentId} has chunk content that does not match the persisted document.");
            }
        }

        return existingChunks;
    }

    private static bool IsChunkIndexUniquenessConflict(DbUpdateException exception)
    {
        for (var innerException = exception.InnerException; innerException is not null; innerException = innerException.InnerException)
        {
            if (innerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_DocumentChunks_DocumentId_ChunkIndex"
                })
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateChunkOptions(int chunkSize, int chunkOverlap)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");
        }

        if (chunkOverlap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkOverlap), "Chunk overlap cannot be negative.");
        }

        if (chunkOverlap >= chunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkOverlap), "Chunk overlap must be smaller than chunk size.");
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (await _documentAccessService.GetAccessibleDocumentAsync(
                documentId,
                DocumentPermission.Read,
                cancellationToken) is null)
        {
            return Array.Empty<DocumentChunk>();
        }

        return await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentChunk?> GetByIdAsync(Guid chunkId, CancellationToken cancellationToken = default)
    {
        var documentId = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Id == chunkId)
            .Select(chunk => (Guid?)chunk.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (documentId is null)
        {
            return null;
        }

        var document = await _documentAccessService.GetAccessibleDocumentAsync(
            documentId.Value,
            DocumentPermission.Read,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        return await _dbContext.DocumentChunks
            .AsNoTracking()
            .FirstOrDefaultAsync(chunk => chunk.Id == chunkId, cancellationToken);
    }

    private static IReadOnlyList<string> CreateChunks(string content, int chunkSize, int chunkOverlap)
    {
        var normalizedContent = NormalizeContent(content);
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return Array.Empty<string>();
        }

        var sentences = SplitIntoSentenceSegments(normalizedContent);
        if (sentences.Count == 0)
        {
            return CreateWordChunks(normalizedContent, chunkSize, chunkOverlap);
        }

        var chunks = new List<string>();
        var current = new StringBuilder();
        var overlapWords = new Queue<string>();

        foreach (var sentence in sentences)
        {
            var trimmedSentence = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmedSentence))
            {
                continue;
            }

            var candidate = string.IsNullOrEmpty(current.ToString())
                ? trimmedSentence
                : $" {trimmedSentence}";

            if (current.Length + candidate.Length <= chunkSize)
            {
                current.Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                var chunkText = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(chunkText);
                    overlapWords = new Queue<string>(chunkText.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(Math.Min(chunkOverlap, chunkSize)));
                }

                current.Clear();
            }

            if (trimmedSentence.Length <= chunkSize)
            {
                if (overlapWords.Count > 0)
                {
                    var overlapText = string.Join(' ', overlapWords);
                    var combined = string.IsNullOrWhiteSpace(overlapText)
                        ? trimmedSentence
                        : $"{overlapText} {trimmedSentence}";

                    current.Append(combined.Length <= chunkSize ? combined : trimmedSentence);
                }
                else
                {
                    current.Append(trimmedSentence);
                }

                continue;
            }

            var fallbackChunks = CreateWordChunks(trimmedSentence, chunkSize, chunkOverlap);
            for (var i = 0; i < fallbackChunks.Count; i++)
            {
                chunks.Add(fallbackChunks[i]);
            }
        }

        if (current.Length > 0)
        {
            var finalChunk = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks.Select(chunk => chunk.Trim())
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
            .ToList();
    }

    private static IReadOnlyList<string> CreateWordChunks(string content, int chunkSize, int chunkOverlap)
    {
        var words = Regex.Matches(content, @"\S+")
            .Select(match => match.Value)
            .ToList();

        if (words.Count == 0)
        {
            return Array.Empty<string>();
        }

        var chunks = new List<string>();
        var index = 0;

        while (index < words.Count)
        {
            var windowSize = Math.Min(chunkSize, words.Count - index);
            var chunkWords = words.Skip(index).Take(windowSize).ToList();
            if (chunkWords.Count == 0)
            {
                break;
            }

            chunks.Add(string.Join(' ', chunkWords));

            if (index + windowSize >= words.Count)
            {
                break;
            }

            var step = Math.Max(1, chunkSize - chunkOverlap);
            index += step;
        }

        return chunks;
    }

    private static List<string> SplitIntoSentenceSegments(string content)
    {
        var segments = Regex.Split(content, @"(?<=[.!?])\s+")
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(segment => segment.Trim())
            .ToList();

        return segments.Count > 0 ? segments : new List<string> { content.Trim() };
    }

    private static string NormalizeContent(string content)
    {
        var normalized = Regex.Replace(content, @"\r\n?", "\n");
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        normalized = Regex.Replace(normalized, @"[ \t]*\n[ \t]*", "\n");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s+([,.!?;:])", "$1");
        return normalized.Trim();
    }

    private sealed class ChunkingTelemetryCounts
    {
        public int GeneratedChunkCount { get; set; }

        public int ReusedChunkCount { get; set; }
    }

    private sealed record ChunkGenerationResult(IReadOnlyList<DocumentChunk> Chunks);
}

using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class AzureStagingBlobStore : IStagingBlobStore
{
    private const int CopyBufferSize = 81920;

    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _blobStorageOptions;

    public AzureStagingBlobStore(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> blobStorageOptions)
    {
        _blobServiceClient = blobServiceClient;
        _blobStorageOptions = blobStorageOptions.Value;
    }

    public async Task<Stream> DownloadAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.FileSize <= 0
            || document.FileSize > PdfDocumentUploadRequest.MaximumFileSizeBytes)
        {
            throw new InvalidDataException("The persisted PDF file size is outside the allowed upload limit.");
        }

        if (string.IsNullOrWhiteSpace(document.StagingBlobKey))
        {
            throw new InvalidDataException("The persisted document does not have a staging blob key.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var blobClient = _blobServiceClient
            .GetBlobContainerClient(_blobStorageOptions.StagingContainerName)
            .GetBlobClient(document.StagingBlobKey);
        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        await using var source = download.Value.Content;

        var downloaded = new MemoryStream((int)document.FileSize);
        try
        {
            var buffer = new byte[CopyBufferSize];
            long downloadedSize = 0;
            while (true)
            {
                var bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                downloadedSize += bytesRead;
                if (downloadedSize > PdfDocumentUploadRequest.MaximumFileSizeBytes)
                {
                    throw new InvalidDataException("The staging blob exceeds the 25 MB upload limit.");
                }

                if (downloadedSize > document.FileSize)
                {
                    throw new InvalidDataException("The downloaded staging blob size does not match the persisted document size.");
                }

                await downloaded.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (downloadedSize != document.FileSize)
            {
                throw new InvalidDataException("The downloaded staging blob size does not match the persisted document size.");
            }

            downloaded.Position = 0;
            return downloaded;
        }
        catch
        {
            await downloaded.DisposeAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(document.StagingBlobKey))
        {
            throw new InvalidDataException("The persisted document does not have a staging blob key.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var blobClient = _blobServiceClient
            .GetBlobContainerClient(_blobStorageOptions.StagingContainerName)
            .GetBlobClient(document.StagingBlobKey);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}

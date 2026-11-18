using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using NoMorePaper.Infrastructure.Telemetry;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Writes a document file so that readers see the old file or the complete new one: the bytes go to
/// a temporary file in the target folder, are flushed to disk and renamed into place, which is atomic
/// on one file system; then the folder is flushed (<see cref="IDirectorySync"/>).
/// </summary>
internal sealed class AtomicFileWriter(IDirectorySync directories, StorageCleanup cleanup, ILogger logger)
{
    private const string KindTag = "nomorepaper.storage.kind";
    private const int BufferSize = 81920;

    private static readonly Counter<long> s_storedBytes = InfrastructureTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.storage.stored_bytes", unit: "By", description: "Bytes written to the document storage");

    /// <param name="area">Storage area for logs (<c>inbox</c>, <c>archive</c>).</param>
    /// <returns>Size and SHA-256 of what was written.</returns>
    public async Task<StoredFile> WriteAsync(
        Guid id, DocumentFileKind kind, string area, string target, Stream content, CancellationToken cancellationToken)
    {
        using var activity = InfrastructureTelemetry.Source.StartActivity("nomorepaper.storage.write");
        activity?.SetTag(KindTag, kind.ToString());

        var folder = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            StoredFile stored;
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            var file = new FileStream(temporary, options);
            await using (file.ConfigureAwait(false))
            {
                stored = await CopyAndHashAsync(content, file, cancellationToken).ConfigureAwait(false);

                // Durable before the rename publishes it; otherwise a power loss could leave an empty file.
                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);

            // Only the folder entry makes the new name survive a power loss (Linux).
            directories.Flush(folder);
            s_storedBytes.Add(stored.SizeBytes, new KeyValuePair<string, object?>(KindTag, kind.ToString()));
            logger.Stored(kind, id, area, stored.SizeBytes);
            return stored;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            cleanup.TryCleanUp(id, area, () => File.Delete(temporary));
            if (exception is not OperationCanceledException)
            {
                logger.StoreFailed(kind, id, exception.GetType().Name);
            }

            throw;
        }
    }

    private static async Task<StoredFile> CopyAndHashAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long size = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                size += read;
            }

            return new StoredFile(size, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

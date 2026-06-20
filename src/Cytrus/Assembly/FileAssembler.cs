using System.Buffers;
using System.Security.Cryptography;
using Cytrus.Exceptions;
using Cytrus.Hash;
using Cytrus.Planning;
using Cytrus.Storage;
using Microsoft.Extensions.Logging;

namespace Cytrus.Assembly;

public sealed partial class FileAssembler(ILogger<FileAssembler> logger) : IFileAssembler
{
    private const int BlockSize = 1 << 20;
    private const string TempSuffix = ".cytmp";

    public async Task<FileAssemblyResult> AssembleAsync(
        FilePlan plan,
        IBundleStore store,
        string outputRoot,
        AssemblyOptions options,
        CancellationToken cancellationToken = default)
    {
        var file = plan.File;
        var destination = PathSafety.ResolveWithinRoot(outputRoot, file.Name);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (file.IsSymlink)
            return CreateSymlink(outputRoot, destination, file.Name, file.Symlink!);

        if (options.SkipUpToDate && await IsUpToDateAsync(destination, file.Size, file.Hash, options, cancellationToken).ConfigureAwait(false))
            return new FileAssemblyResult(file.Name, file.Size, FileAssemblyStatus.Skipped);

        var tempPath = destination + TempSuffix;
        var fileHasher = options.VerifyFiles && !file.Hash.IsEmpty ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1) : null;
        var buffer = ArrayPool<byte>.Shared.Rent(BlockSize);

        try
        {
            await using (var output = new FileStream(tempPath, new FileStreamOptions
                         {
                             Mode = FileMode.Create,
                             Access = FileAccess.Write,
                             Share = FileShare.None,
                             Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                             PreallocationSize = file.Size
                         }))
            {
                foreach (var chunk in plan.Chunks)
                    await WriteChunkAsync(chunk, store, output, fileHasher, buffer, options.VerifyChunks, file.Name, cancellationToken).ConfigureAwait(false);
            }

            if (fileHasher is not null)
            {
                var actual = fileHasher.GetHashAndReset();

                if (!actual.AsSpan().SequenceEqual(file.Hash.Span))
                {
                    TryDelete(tempPath);

                    throw new IntegrityException($"File hash mismatch for '{file.Name}': expected {file.Hash.Hex}, got {Convert.ToHexStringLower(actual)}.");
                }
            }

            ApplyExecutableBit(tempPath, file.Executable);
            File.Move(tempPath, destination, overwrite: true);

            return new FileAssemblyResult(file.Name, file.Size, FileAssemblyStatus.Written);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            fileHasher?.Dispose();
        }
    }

    private static async Task WriteChunkAsync(
        ChunkPlacement chunk,
        IBundleStore store,
        Stream output,
        IncrementalHash? fileHasher,
        byte[] buffer,
        bool verifyChunk,
        string fileName,
        CancellationToken cancellationToken)
    {
        using var chunkHasher = verifyChunk && !chunk.ChunkHash.IsEmpty
            ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1)
            : null;

        var remaining = chunk.Size;
        var sourceOffset = chunk.Offset;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var slice = buffer.AsMemory(0, toRead);
            await store.ReadExactAsync(chunk.BundleHash, sourceOffset, slice, cancellationToken).ConfigureAwait(false);

            await output.WriteAsync(slice, cancellationToken).ConfigureAwait(false);
            fileHasher?.AppendData(buffer, 0, toRead);
            chunkHasher?.AppendData(buffer, 0, toRead);

            sourceOffset += toRead;
            remaining -= toRead;
        }

        if (chunkHasher is not null)
        {
            var actual = chunkHasher.GetHashAndReset();

            if (!actual.AsSpan().SequenceEqual(chunk.ChunkHash.Span))
                throw new IntegrityException($"Chunk hash mismatch in '{fileName}' (bundle {chunk.BundleHash.Hex} @ {chunk.Offset}): " + $"expected {chunk.ChunkHash.Hex}, got {Convert.ToHexStringLower(actual)}.");
        }
    }

    private async Task<bool> IsUpToDateAsync(
        string destination,
        long expectedSize,
        HashId expectedHash,
        AssemblyOptions options,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(destination);
        if (!info.Exists || info.Length != expectedSize)
            return false;

        if (!options.VerifyFiles || expectedHash.IsEmpty)
            return true;

        var actual = await ComputeFileSha1Async(destination, cancellationToken).ConfigureAwait(false);
        var matches = actual.AsSpan().SequenceEqual(expectedHash.Span);

        if (matches)
            LogSkippingUpToDateFileFile(destination);

        return matches;
    }

    private static async Task<byte[]> ComputeFileSha1Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        using var sha1 = SHA1.Create();
        return await sha1.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private FileAssemblyResult CreateSymlink(string outputRoot, string linkPath, string name, string target)
    {
        if (!PathSafety.IsSymlinkTargetSafe(outputRoot, linkPath, target))
        {
            LogSkippingSymlinkNameTargetTargetEscapesOutputDirectory(name, target);
            return new FileAssemblyResult(name, 0, FileAssemblyStatus.SymlinkUnsupported);
        }

        try
        {
            if (File.Exists(linkPath) || Directory.Exists(linkPath))
                File.Delete(linkPath);

            File.CreateSymbolicLink(linkPath, target);

            return new FileAssemblyResult(name, 0, FileAssemblyStatus.SymlinkCreated);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            LogCouldNotCreateSymlinkNameTarget(name, target, ex);
            return new FileAssemblyResult(name, 0, FileAssemblyStatus.SymlinkUnsupported);
        }
    }

    private static void ApplyExecutableBit(string path, bool executable)
    {
        if (!executable || OperatingSystem.IsWindows())
            return;

        var mode = File.GetUnixFileMode(path);

        mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(path, mode);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // ignore
        }
    }

    [LoggerMessage(LogLevel.Debug, "Skipping up-to-date file '{File}'.")]
    private partial void LogSkippingUpToDateFileFile(string file);

    [LoggerMessage(LogLevel.Warning, "Skipping symlink '{Name}' -> '{Target}' (target escapes output directory).")]
    private partial void LogSkippingSymlinkNameTargetTargetEscapesOutputDirectory(string name, string target);

    [LoggerMessage(LogLevel.Warning, "Could not create symlink '{Name}' -> '{Target}'.")]
    private partial void LogCouldNotCreateSymlinkNameTarget(string name, string target, Exception exception);
}

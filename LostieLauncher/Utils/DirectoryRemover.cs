using LostieLauncher.Models;
using System.IO;

namespace LostieLauncher.Utils;

public static class DirectoryRemover
{
    private const int DefaultMaxAttempts = 3;
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(300);

    public static DirectoryDeletionResult Delete(string path) => Delete(path, DefaultMaxAttempts, DefaultRetryDelay);

    internal static DirectoryDeletionResult Delete(string path, int maxAttempts, TimeSpan retryDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        DirectoryDeletionResult? failure = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!Directory.Exists(path)) return new DirectoryDeletionResult(true, attempt);

            try
            {
                DeleteTree(path);
                return new DirectoryDeletionResult(true, attempt);
            }
            catch (DeletionBlockedException ex)
            {
                var cause = ex.InnerException ?? ex;
                failure = new DirectoryDeletionResult(false, attempt, ex.BlockingPath, cause);

                if (attempt >= maxAttempts) break;

                var backoff = retryDelay * attempt;
                Logs.InfoLogManager($"Delete attempt {attempt}/{maxAttempts} blocked at '{ex.BlockingPath}' ({cause.Message}), retrying in {backoff.TotalMilliseconds:0} ms...");
                if (backoff > TimeSpan.Zero) Thread.Sleep(backoff);
            }
        }

        return failure ?? new DirectoryDeletionResult(false, maxAttempts, path);
    }

    private static void DeleteTree(string directory)
    {
        try
        {
            if (IsReparsePoint(directory))
            {
                RemoveDirectory(directory);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(directory)) DeleteFile(file);
            foreach (var subdirectory in Directory.EnumerateDirectories(directory)) DeleteTree(subdirectory);

            RemoveDirectory(directory);
        }
        catch (DeletionBlockedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DeletionBlockedException(directory, ex);
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            ClearReadOnly(path);
            File.Delete(path);
        }
        catch (Exception ex)
        {
            throw new DeletionBlockedException(path, ex);
        }
    }

    private static void RemoveDirectory(string path)
    {
        try
        {
            ClearReadOnly(path);
            Directory.Delete(path);
        }
        catch (Exception ex)
        {
            throw new DeletionBlockedException(path, ex);
        }
    }

    private static void ClearReadOnly(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == 0) return;

            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return false;
        }
    }

    private sealed class DeletionBlockedException(string blockingPath, Exception cause)
        : Exception($"Failed to delete '{blockingPath}'.", cause)
    {
        public string BlockingPath { get; } = blockingPath;
    }
}

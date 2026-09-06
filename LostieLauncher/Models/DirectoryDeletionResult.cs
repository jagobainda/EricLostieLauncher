namespace LostieLauncher.Models;

public sealed record DirectoryDeletionResult(bool Deleted, int Attempts, string? BlockingPath = null, Exception? Error = null);

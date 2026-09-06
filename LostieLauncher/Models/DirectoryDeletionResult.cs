namespace LostieLauncher.Models;

/// <summary>
/// Outcome of a recursive removal: whether the tree is gone and, when it is not, which entry blocked
/// it. The blocking path is what the user needs to be told about.
/// </summary>
public sealed record DirectoryDeletionResult(bool Deleted, int Attempts, string? BlockingPath = null, Exception? Error = null);

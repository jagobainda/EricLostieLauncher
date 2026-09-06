namespace LostieLauncher.Models;

public enum UninstallOutcome
{
    /// <summary>The folder was removed and the game unregistered.</summary>
    Completed,

    /// <summary>There was nothing to delete; only the stale registry entry was cleaned up.</summary>
    FilesNotFound,

    /// <summary>
    /// Deletion could not finish. The game is unregistered anyway — the recursive delete has already
    /// destroyed the installation, so keeping the entry would leave the user with a broken game they
    /// can neither launch nor remove — and <see cref="UninstallResult.BlockingPath"/> names what is
    /// left on disk.
    /// </summary>
    FilesLeftBehind,

    /// <summary>The game is still running; nothing was deleted or unregistered.</summary>
    GameRunning
}

public sealed record UninstallResult(UninstallOutcome Outcome, string? BlockingPath = null);

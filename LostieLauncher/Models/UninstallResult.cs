namespace LostieLauncher.Models;

public enum UninstallOutcome
{
    Completed,

    FilesNotFound,

    FilesLeftBehind,

    NothingDeleted,

    GameRunning
}

public sealed record UninstallResult(UninstallOutcome Outcome, string? BlockingPath = null);

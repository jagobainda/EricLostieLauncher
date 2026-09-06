namespace LostieLauncher.Models;

public enum UninstallOutcome
{
    Completed,

    FilesNotFound,

    FilesLeftBehind,

    GameRunning
}

public sealed record UninstallResult(UninstallOutcome Outcome, string? BlockingPath = null);

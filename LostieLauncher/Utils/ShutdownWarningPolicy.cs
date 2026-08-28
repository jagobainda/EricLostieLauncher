namespace LostieLauncher.Utils;

public enum ShutdownWarning
{
    None,

    Download,

    Game,

    Both
}

public static class ShutdownWarningPolicy
{
    public static ShutdownWarning Decide(bool isDownloading, bool isGameRunning) => (isDownloading, isGameRunning) switch
    {
        (true, true) => ShutdownWarning.Both,
        (true, false) => ShutdownWarning.Download,
        (false, true) => ShutdownWarning.Game,
        _ => ShutdownWarning.None
    };
}

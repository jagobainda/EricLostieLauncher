namespace LostieLauncher.Models;

public readonly record struct DownloadProgressInfo(double Percent, double BytesPerSecond, long TotalBytes = -1, long DownloadedBytes = 0);

public enum DownloadOutcome
{
    Success,
    Cancelled,
    Failed,
    PermissionDenied
}

public class DownloadResult
{
    public DownloadOutcome Outcome { get; init; }
    public string? ErrorMessage { get; init; }

    public static DownloadResult Succeeded() => new() { Outcome = DownloadOutcome.Success };

    public static DownloadResult Cancelled() => new() { Outcome = DownloadOutcome.Cancelled };

    public static DownloadResult Failed(string message) => new() { Outcome = DownloadOutcome.Failed, ErrorMessage = message };

    public static DownloadResult PermissionDenied() => new() { Outcome = DownloadOutcome.PermissionDenied };
}

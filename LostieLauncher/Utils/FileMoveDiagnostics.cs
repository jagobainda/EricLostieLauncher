using System.IO;
using System.Text;

namespace LostieLauncher.Utils;

public static class FileMoveDiagnostics
{
    private const int HResultWin32Facility = unchecked((int)0x80070000);

    public static string Describe(string? sourcePath, string? destinationPath, Exception? error)
    {
        var builder = new StringBuilder();

        builder.Append("source=").Append(Quote(sourcePath)).Append(" [").Append(DescribeEntry(sourcePath, probeLock: true)).Append(']');
        builder.Append(" destination=").Append(Quote(destinationPath)).Append(" [").Append(DescribeEntry(destinationPath, probeLock: true)).Append(']');

        var destinationDirectory = TryGetDirectoryName(destinationPath);
        builder.Append(" destinationDirectory=").Append(Quote(destinationDirectory))
               .Append(" [exists=").Append(destinationDirectory is not null && Directory.Exists(destinationDirectory)).Append(']');

        builder.Append(" win32=").Append(TryGetWin32Error(error, out var win32) ? $"{win32} ({DescribeWin32Error(win32)})" : "n/a");
        builder.Append(" error=").Append(error is null ? "n/a" : $"{error.GetType().Name}: {error.Message}");

        return builder.ToString();
    }

    internal static string DescribeEntry(string? path, bool probeLock)
    {
        if (string.IsNullOrWhiteSpace(path)) return "no path";

        try
        {
            if (Directory.Exists(path)) return $"directory, attributes={File.GetAttributes(path)}";
            if (!File.Exists(path)) return "missing";

            var info = new FileInfo(path);
            var entry = $"file, size={info.Length}, attributes={info.Attributes}";

            return probeLock ? $"{entry}, lockedByAnotherProcess={FileLockProbe.IsLockedByAnotherProcess(path)}" : entry;
        }
        catch (Exception ex)
        {
            return $"unreadable ({ex.GetType().Name}: {ex.Message})";
        }
    }

    internal static bool TryGetWin32Error(Exception? error, out int code)
    {
        code = 0;
        if (error is null) return false;

        if ((error.HResult & unchecked((int)0xFFFF0000)) != HResultWin32Facility) return false;

        code = error.HResult & 0xFFFF;
        return true;
    }

    internal static string DescribeWin32Error(int code) => code switch
    {
        2 => "ERROR_FILE_NOT_FOUND",
        3 => "ERROR_PATH_NOT_FOUND",
        5 => "ERROR_ACCESS_DENIED",
        17 => "ERROR_NOT_SAME_DEVICE",
        19 => "ERROR_WRITE_PROTECT",
        32 => "ERROR_SHARING_VIOLATION",
        33 => "ERROR_LOCK_VIOLATION",
        80 => "ERROR_FILE_EXISTS",
        112 => "ERROR_DISK_FULL",
        145 => "ERROR_DIR_NOT_EMPTY",
        183 => "ERROR_ALREADY_EXISTS",
        206 => "ERROR_FILENAME_EXCED_RANGE",
        _ => "unknown",
    };

    private static string? TryGetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Quote(string? value) => value is null ? "<null>" : $"'{value}'";
}

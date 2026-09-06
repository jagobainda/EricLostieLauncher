using System.IO;

namespace LostieLauncher.Utils;

/// <summary>
/// Tells whether a file is held open by another process.
/// <para>
/// The probe opens the file for <b>reading</b> while denying sharing: a running executable is kept
/// open by Windows with FILE_SHARE_READ, so the request loses and raises a sharing violation
/// (<see cref="IOException"/>). Asking for write access instead would also fail on a read-only
/// attribute or a denying ACL, which are permission problems — reporting those as "in use" would
/// block an uninstall that is perfectly safe to run.
/// </para>
/// </summary>
public static class FileLockProbe
{
    public static bool IsLockedByAnotherProcess(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (Exception)
        {
            // Cannot be checked (permissions, path length, ...) — that is not evidence of a lock.
            return false;
        }
    }
}

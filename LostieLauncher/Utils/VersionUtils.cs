namespace LostieLauncher.Utils;

public static class VersionUtils
{
    private const string UnknownVersion = "unknown";

    public static bool IsNewerVersion(string remoteVersion, string localVersion)
    {
        var remoteParsed = ParseBaseVersion(remoteVersion);
        var localParsed = ParseBaseVersion(localVersion);

        if (remoteParsed is null || localParsed is null)
        {
            Logs.InfoLogManager($"IsNewerVersion: versión no comparable (remota='{remoteVersion}', local='{localVersion}'); no se marca actualización.");
            return false;
        }

        return remoteParsed > localParsed;
    }

    public static string FormatDisplayVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return UnknownVersion;

        var normalized = version.Trim().TrimStart('v', 'V');
        return normalized.Length == 0 ? UnknownVersion : $"v{normalized}";
    }

    internal static Version? ParseBaseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var v = version.TrimStart('v', 'V');
        var dashIndex = v.IndexOf('-');
        if (dashIndex >= 0) v = v[..dashIndex];
        return Version.TryParse(v, out var result) ? result : null;
    }
}

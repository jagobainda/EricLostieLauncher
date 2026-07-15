using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LostieLauncher.Content;
using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.Views.Dialogs;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace LostieLauncher.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IContentService _contentService;
    private readonly ISettingsService _settingsService;
    private readonly IDownloadService _downloadService;
    private readonly GlobalViewModel _globalViewModel;
    private readonly DownloadOptions _downloadOptions;
    private readonly TaskCompletionSource _libraryLoadedTcs = new();

    private readonly Dictionary<string, DownloadSession> _sessions = [];
    private DownloadSession? _activeSession;
    private bool _serverActionsBlockedMessageShown;

    private static readonly Regex KeyFormatRegex = new(@"^[A-Za-z0-9]{4}(-[A-Za-z0-9]{4}){4}$", RegexOptions.Compiled);
    private static readonly Regex ArchivoFormatRegex = new(@"^[A-Za-z0-9._-]+\.zip$", RegexOptions.Compiled);
    private static readonly Regex Sha256FormatRegex = new(@"^[A-Fa-f0-9]{64}$", RegexOptions.Compiled);

    public event Action<string, string, string?>? GameInstalled;
    public event Action<string>? ScrollToGameRequested;
    public string? PendingScrollGameId { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial ObservableCollection<GameInfo> Games { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial bool IsLoading { get; set; }

    public bool IsEmpty => !IsLoading && Games.Count == 0;
    public bool IsListVisible => !IsLoading && Games.Count > 0;

    public Task LibraryLoadedTask => _libraryLoadedTcs.Task;

    public LibraryViewModel(IContentService contentService, ISettingsService settingsService,
        IDownloadService downloadService, GlobalViewModel globalViewModel, DownloadOptions downloadOptions)
    {
        _contentService = contentService;
        _settingsService = settingsService;
        _downloadService = downloadService;
        _globalViewModel = globalViewModel;
        _downloadOptions = downloadOptions;
        _ = LoadGamesAsync();
    }

    public async Task RefreshAsync() => await LoadGamesAsync();

    private async Task LoadGamesAsync()
    {
        IsLoading = true;

        try
        {
            var result = await _contentService.GetGamesAsync();
            var localGames = await _contentService.GetLocalGamesAsync();
            var playtimes = await _contentService.GetAllPlaytimesAsync();

            var installedById = localGames
                .Where(g => g.Id != Guid.Empty)
                .ToDictionary(g => g.Id);
            var installedByName = localGames
                .Where(g => g.Id == Guid.Empty)
                .ToDictionary(g => g.Nombre, StringComparer.OrdinalIgnoreCase);

            foreach (var game in result)
            {
                var local =
                    (game.Id != Guid.Empty && installedById.TryGetValue(game.Id, out var byId)) ? byId :
                    installedByName.TryGetValue(game.Nombre, out var byName) ? byName : null;

                if (local is not null)
                    game.DownloadStatus = Utils.VersionUtils.IsNewerVersion(game.Version, local.Version) ? GameDownloadStatus.UpdateAvailable : GameDownloadStatus.Downloaded;

                if (game.Id != Guid.Empty && playtimes.TryGetValue(game.Id, out var pt))
                    game.PlaytimeMinutes = pt;
            }

            Games = new ObservableCollection<GameInfo>(result);
            Logs.DebugLogManager($"Games library loaded: {result.Count} games.");
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
        finally
        {
            IsLoading = false;
            _libraryLoadedTcs.TrySetResult();
        }
    }

    [RelayCommand]
    private async Task StartDownloadAsync(GameDownloadArgs args)
    {
        if (_globalViewModel.IsDownloading)
        {
            Logs.DebugLogManager($"Download request ignored for {args.GameId}: another download is already active.");
            return;
        }

        if (!await EnsureServerActionsAvailableAsync()) return;

        var game = Games.FirstOrDefault(g => g.GameId == args.GameId);
        if (game is null) return;

        if (game.DownloadStatus == GameDownloadStatus.Paused && _sessions.TryGetValue(game.GameId, out var paused))
        {
            await ExecuteDownloadAndInstallAsync(game, paused);
            return;
        }

        var strings = SettingsViewModel.Instance.Strings;
        var downloadPath = _contentService.GetGameDirectory(game.Nombre);

        var confirmed = DownloadConfirmDialog.Show(game, args, downloadPath, strings);
        if (confirmed is null) return;

        args = confirmed;
        SpecialVersionConfig? specialConfig = null;
        string url;

        if (!string.IsNullOrEmpty(args.Key))
        {
            if (!KeyFormatRegex.IsMatch(args.Key!))
            {
                Logs.InfoLogManager($"Download key rejected for {args.GameId}: invalid format.");
                CustomMessageBox.Show(strings.DownloadKeyInvalidTitle, strings.DownloadKeyInvalidMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }

            Logs.DebugLogManager($"Fetching special version config for key: {args.Key}.");
            var result = await _downloadService.FetchSpecialVersionConfigAsync(args.Key!);

            if (result.Outcome != SpecialVersionConfigOutcome.Success)
            {
                if (GetSpecialVersionConfigErrorMessage(result.Outcome, strings) is { } error)
                    CustomMessageBox.Show(error.Title, error.Message, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }

            var config = result.Config!;

            if (!IsValidSpecialVersionConfig(config))
            {
                Logs.ErrorLogManager($"Invalid special version config for key {args.Key}: archivo='{config.Archivo}'.");
                CustomMessageBox.Show(strings.DownloadKeyNotFoundTitle, strings.DownloadKeyNotFoundMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }

            if (config.JuegoPrincipal != game.Id)
            {
                CustomMessageBox.Show(strings.DownloadKeyMismatchTitle, strings.DownloadKeyMismatchMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }

            specialConfig = config;
            args = args with { Version = config.Version };
            url = $"{_downloadOptions.BaseUrl}/{args.Key}/{config.Archivo}";
        }
        else
        {
            url = $"{_downloadOptions.BaseUrl}{args.RutaRelativa}";
        }

        var session = CreateSession(game, args, url, specialConfig, isUpdate: false);
        var downloadType = specialConfig is not null ? $" ({specialConfig.Tipo})" : " (regular)";
        Logs.DebugLogManager($"Starting download session for {args.GameId}{downloadType}.");
        await ExecuteDownloadAndInstallAsync(game, session);
    }

    [RelayCommand]
    private async Task StartUpdateAsync(GameDownloadArgs args)
    {
        if (_globalViewModel.IsDownloading)
        {
            Logs.DebugLogManager($"Update request ignored for {args.GameId}: another download is already active.");
            return;
        }

        if (!await EnsureServerActionsAvailableAsync()) return;

        var game = Games.FirstOrDefault(g => g.GameId == args.GameId);
        if (game is null) return;

        var url = $"{_downloadOptions.BaseUrl}{args.RutaRelativa}";
        var session = CreateSession(game, args, url, specialConfig: null, isUpdate: true);

        Logs.DebugLogManager($"Starting update session for {args.GameId}.");
        PendingScrollGameId = args.GameId;
        ScrollToGameRequested?.Invoke(args.GameId);
        await ExecuteDownloadAndInstallAsync(game, session);
        PendingScrollGameId = null;
    }

    [RelayCommand]
    private async Task SwitchToSpecialVersionAsync(GameDownloadArgs args)
    {
        if (_globalViewModel.IsDownloading)
        {
            Logs.DebugLogManager($"Switch request ignored for {args.GameId}: another download is already active.");
            return;
        }

        if (!await EnsureServerActionsAvailableAsync()) return;

        var game = Games.FirstOrDefault(g => g.GameId == args.GameId);
        if (game is null) return;

        var strings = SettingsViewModel.Instance.Strings;
        var key = SpecialVersionDialog.Show(strings);
        if (key is null)
        {
            Logs.DebugLogManager($"Special version switch cancelled by user for {args.GameId}.");
            return;
        }

        if (!KeyFormatRegex.IsMatch(key))
        {
            Logs.InfoLogManager($"Special version key rejected for {args.GameId}: invalid format.");
            CustomMessageBox.Show(strings.DownloadKeyInvalidTitle, strings.DownloadKeyInvalidMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
            return;
        }

        var result = await _downloadService.FetchSpecialVersionConfigAsync(key);

        if (result.Outcome != SpecialVersionConfigOutcome.Success)
        {
            if (GetSpecialVersionConfigErrorMessage(result.Outcome, strings) is { } error)
                CustomMessageBox.Show(error.Title, error.Message, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
            return;
        }

        var config = result.Config!;

        if (!IsValidSpecialVersionConfig(config))
        {
            Logs.ErrorLogManager($"Invalid special version config for key {key}: archivo='{config.Archivo}'.");
            CustomMessageBox.Show(strings.DownloadKeyNotFoundTitle, strings.DownloadKeyNotFoundMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
            return;
        }

        if (config.JuegoPrincipal != game.Id)
        {
            CustomMessageBox.Show(strings.DownloadKeyMismatchTitle, strings.DownloadKeyMismatchMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
            return;
        }

        var switchArgs = new GameDownloadArgs(args.GameId, config.Version, args.RutaRelativa, key);
        var url = $"{_downloadOptions.BaseUrl}/{key}/{config.Archivo}";
        var session = CreateSession(game, switchArgs, url, config, isUpdate: true);

        Logs.DebugLogManager($"Starting special version switch session for {args.GameId} ({config.Tipo} v{config.Version}).");
        PendingScrollGameId = args.GameId;
        ScrollToGameRequested?.Invoke(args.GameId);
        await ExecuteDownloadAndInstallAsync(game, session);
        PendingScrollGameId = null;
    }

    private DownloadSession CreateSession(GameInfo game, GameDownloadArgs args, string url, SpecialVersionConfig? specialConfig, bool isUpdate)
    {
        var session = new DownloadSession
        {
            GameId = game.GameId,
            Args = args,
            SpecialConfig = specialConfig,
            Url = url,
            ZipPath = BuildZipPath(args),
            ExtractDir = _contentService.GetGameDirectory(game.Nombre),
            IsUpdate = isUpdate,
        };

        if (_sessions.TryGetValue(game.GameId, out var previous) && !ReferenceEquals(previous, session))
            previous.Cts?.Dispose();

        _sessions[game.GameId] = session;
        return session;
    }

    private string BuildZipPath(GameDownloadArgs args)
    {
        var gamesRoot = _settingsService.GetGamesRootDirectory();
        return Path.Combine(gamesRoot, ".downloads", Utils.DownloadPathUtils.GetZipFileName(args));
    }

    private async Task ExecuteDownloadAndInstallAsync(GameInfo game, DownloadSession session)
    {
        game.DownloadStatus = GameDownloadStatus.Downloading;
        game.DownloadProgressValue = 0;
        _globalViewModel.IsDownloading = true;
        _activeSession = session;

        try
        {
            Logs.DebugLogManager($"Executing download/install: {session.Args.GameId} v{session.Args.Version}.");
            session.IsCancelling = false;
            session.Cts?.Dispose();
            session.Cts = new CancellationTokenSource();

            var progress = new Progress<DownloadProgressInfo>(p =>
            {
                game.DownloadProgressValue = p.Percent;
                game.DownloadSpeedBytesPerSec = p.BytesPerSecond;
                game.DownloadRemainingText = p.BytesPerSecond > 0 && p.TotalBytes > 0
                    ? $"· {FormatRemainingTime((p.TotalBytes - p.DownloadedBytes) / p.BytesPerSecond)}"
                    : string.Empty;
            });

            var isSpecial = session.SpecialConfig is not null;
            Logs.InfoLogManager($"Downloading: {session.Args.GameId} v{session.Args.Version}{(isSpecial ? $" (special: {session.SpecialConfig!.Tipo})" : "")}.");
            var result = await _downloadService.DownloadAsync(session.Url, session.ZipPath, progress, session.Cts.Token);

            switch (result.Outcome)
            {
                case DownloadOutcome.Success:
                    await HandleDownloadSuccessAsync(game, session);
                    break;
                case DownloadOutcome.Cancelled:
                    HandleDownloadCancelled(game, session);
                    break;
                case DownloadOutcome.Failed:
                    HandleDownloadFailed(game, session, result.ErrorMessage);
                    break;
            }
        }
        finally
        {
            if (_activeSession is null || ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
                _globalViewModel.IsDownloading = false;
            }

            game.DownloadSpeedBytesPerSec = 0;
            game.DownloadRemainingText = string.Empty;
        }
    }

    private async Task<bool> EnsureServerActionsAvailableAsync()
    {
        var blocked = await _contentService.IsServerActionBlockedAsync();
        if (!blocked)
        {
            _serverActionsBlockedMessageShown = false;
            return true;
        }

        Logs.InfoLogManager("Server-backed action blocked by maintenance flag.");

        if (!_serverActionsBlockedMessageShown)
        {
            var strings = SettingsViewModel.Instance.Strings;
            CustomMessageBox.Show(strings.ServerActionsUnavailableTitle, strings.ServerActionsUnavailableMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Information);
            _serverActionsBlockedMessageShown = true;
        }

        return false;
    }

    private async Task HandleDownloadSuccessAsync(GameInfo game, DownloadSession session)
    {
        try
        {
            Logs.InfoLogManager($"Download complete, extracting: {session.Args.GameId}.");

            var expectedHash = session.SpecialConfig?.Sha256 ?? game.Sha256;
            if (!await VerifyIntegrityAsync(game, session.ZipPath, expectedHash))
            {
                try { File.Delete(session.ZipPath); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
                Logs.ErrorLogManager(IsWellFormedSha256(expectedHash)
                    ? $"Hash mismatch for {session.Args.GameId}. Expected: {expectedHash}"
                    : $"Integrity verification refused for {session.Args.GameId}: no valid SHA-256 available in catalog/config.");
                ResetDownloadState(game, session);
                var strings = SettingsViewModel.Instance.Strings;
                CustomMessageBox.Show(strings.HashMismatchTitle, strings.HashMismatchMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }

            game.DownloadStatus = GameDownloadStatus.Extracting;
            game.DownloadProgressValue = 100;
            game.DownloadRemainingText = string.Empty;

            await ExtractArchiveAsync(session.ZipPath, session.ExtractDir);

            var tipo = session.SpecialConfig?.Tipo;
            await _contentService.RegisterGameAsync(game.Id, game.Nombre, session.Args.Version, tipo);

            game.DownloadStatus = GameDownloadStatus.Downloaded;
            game.DownloadProgressValue = 100;
            RemoveSession(session);
            Logs.InfoLogManager($"Game installed: {session.Args.GameId} v{session.Args.Version}{(tipo is not null ? $" ({tipo})" : "")}.");
            GameInstalled?.Invoke(game.Nombre, session.Args.Version, tipo);
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            ResetDownloadState(game, session);
        }
    }

    private static bool IsWellFormedSha256(string? hash) => !string.IsNullOrEmpty(hash) && Sha256FormatRegex.IsMatch(hash);

    internal static async Task<bool> VerifyIntegrityAsync(GameInfo game, string zipPath, string? expectedHash)
    {
        if (!IsWellFormedSha256(expectedHash)) return false;

        game.DownloadStatus = GameDownloadStatus.VerifyingIntegrity;
        return await Task.Run(() =>
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(zipPath);
            var actualHash = Convert.ToHexString(sha.ComputeHash(fs));
            return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static async Task ExtractArchiveAsync(string zipPath, string extractDir) => await Task.Run(() =>
    {
        var tempDir = extractDir + ".tmp";
        var backupDir = extractDir + ".old";

        try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
        try { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true); } catch (Exception ex) { Logs.ErrorLogManager(ex); }

        Directory.CreateDirectory(tempDir);
        try
        {
            Logs.DebugLogManager($"Extracting archive: {Path.GetFileName(zipPath)}.");
            var readerOptions = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 }
            };

            var tempDirFull = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;
            var entryCount = 0;
            using (var stream = File.OpenRead(zipPath))
            using (var archive = ArchiveFactory.OpenArchive(stream, readerOptions))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory && e.Key is not null))
                {
                    var destPath = Path.GetFullPath(Path.Combine(tempDir, entry.Key!));
                    if (!destPath.StartsWith(tempDirFull, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Zip Slip attempt detected in entry: {entry.Key}");
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    using var entryStream = entry.OpenEntryStream();
                    using var outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    entryStream.CopyTo(outStream);
                    entryCount++;
                }
            }
            Logs.DebugLogManager($"Temp extraction complete: {entryCount} files.");

            AtomicSwapDirectories(tempDir, backupDir, extractDir);

            try { File.Delete(zipPath); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
            Logs.DebugLogManager($"Extraction complete: {entryCount} files installed.");
        }
        catch
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            throw;
        }
    });

    internal static void AtomicSwapDirectories(string sourceDir, string backupDir, string targetDir)
    {
        var hadExisting = Directory.Exists(targetDir);
        if (hadExisting)
        {
            try { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
            Directory.Move(targetDir, backupDir);
        }

        try
        {
            Directory.Move(sourceDir, targetDir);
        }
        catch
        {
            if (hadExisting)
            {
                try { Directory.Move(backupDir, targetDir); } catch { }
            }
            throw;
        }

        if (hadExisting)
        {
            try { Directory.Delete(backupDir, true); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
        }
    }

    private void HandleDownloadCancelled(GameInfo game, DownloadSession session)
    {
        if (session.IsCancelling)
        {
            Logs.InfoLogManager($"Download cancelled: {session.Args.GameId}.");
            CleanupDownloadFiles(session);
            ResetDownloadState(game, session);
        }
        else
        {
            Logs.InfoLogManager($"Download paused: {session.Args.GameId}.");
            game.DownloadStatus = GameDownloadStatus.Paused;
        }
    }

    private void HandleDownloadFailed(GameInfo game, DownloadSession session, string? errorMessage)
    {
        var strings = SettingsViewModel.Instance.Strings;
        Logs.ErrorLogManager($"Download failed: {session.Args.GameId}: {errorMessage}");
        ResetDownloadState(game, session);
        CustomMessageBox.Show(strings.DownloadErrorTitle, strings.DownloadErrorMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
    }

    private void ResetDownloadState(GameInfo game, DownloadSession session)
    {
        game.DownloadStatus = session.IsUpdate ? GameDownloadStatus.UpdateAvailable : GameDownloadStatus.Available;
        game.DownloadProgressValue = 0;
        RemoveSession(session);
    }

    private void RemoveSession(DownloadSession session)
    {
        session.Cts?.Dispose();
        session.Cts = null;
        _sessions.Remove(session.GameId);
        if (ReferenceEquals(_activeSession, session)) _activeSession = null;
    }

    private static string FormatRemainingTime(double seconds)
    {
        if (seconds <= 0) return string.Empty;
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    internal static bool IsValidSpecialVersionConfig(SpecialVersionConfig config) => ArchivoFormatRegex.IsMatch(config.Archivo) &&
        IsWellFormedSha256(config.Sha256) &&
        !string.IsNullOrWhiteSpace(config.Version);

    internal static (string Title, string Message)? GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome outcome, IStrings strings) => outcome switch
    {
        SpecialVersionConfigOutcome.NotFound => (strings.DownloadKeyNotFoundTitle, strings.DownloadKeyNotFoundMessage),
        SpecialVersionConfigOutcome.NetworkError or SpecialVersionConfigOutcome.InvalidResponse => (strings.DownloadErrorTitle, strings.DownloadErrorMessage),
        _ => null,
    };

    [RelayCommand]
    private void PauseDownload(string? gameId)
    {
        Logs.DebugLogManager($"Pausing download: {gameId ?? "active session"}.");
        var session = ResolveSession(gameId);
        session?.Cts?.Cancel();
    }

    [RelayCommand]
    private void CancelDownload(string? gameId)
    {
        var session = ResolveSession(gameId);
        if (session is null) return;

        var game = Games.FirstOrDefault(g => g.GameId == session.GameId);
        if (game is null) return;

        var strings = SettingsViewModel.Instance.Strings;
        var confirmed = CustomMessageBox.Show(
            strings.CancelDownloadConfirmTitle,
            strings.CancelDownloadConfirmMessage,
            CustomMessageBoxButton.YesNo,
            CustomMessageBoxIcon.Information);

        if (confirmed != true)
        {
            Logs.DebugLogManager($"Cancel download aborted by user for {session.GameId}.");
            return;
        }

        if (!_sessions.TryGetValue(session.GameId, out var current) || !ReferenceEquals(current, session)) return;

        if (game.DownloadStatus == GameDownloadStatus.Paused)
        {
            Logs.InfoLogManager($"Cancelling paused download: {session.GameId}.");
            var wasActive = ReferenceEquals(_activeSession, session);
            CleanupDownloadFiles(session);
            ResetDownloadState(game, session);
            if (wasActive) _globalViewModel.IsDownloading = false;
            return;
        }

        session.IsCancelling = true;
        Logs.InfoLogManager($"Cancelling download: {session.GameId}.");
        session.Cts?.Cancel();
    }

    private DownloadSession? ResolveSession(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId)) return _activeSession;
        return _sessions.TryGetValue(gameId, out var session) ? session : null;
    }

    private static void CleanupDownloadFiles(DownloadSession session)
    {
        var zipPath = session.ZipPath;
        var partPath = Utils.DownloadPathUtils.GetPartFilePath(zipPath);
        var metaPath = Utils.DownloadPathUtils.GetMetaFilePath(partPath);

        try { if (File.Exists(partPath)) File.Delete(partPath); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
        try { if (File.Exists(metaPath)) File.Delete(metaPath); } catch (Exception ex) { Logs.ErrorLogManager(ex); }
    }

    private sealed class DownloadSession
    {
        public required string GameId { get; init; }
        public required GameDownloadArgs Args { get; init; }
        public SpecialVersionConfig? SpecialConfig { get; init; }
        public required string Url { get; init; }
        public required string ZipPath { get; init; }
        public required string ExtractDir { get; init; }
        public required bool IsUpdate { get; init; }
        public CancellationTokenSource? Cts { get; set; }
        public bool IsCancelling { get; set; }
    }
}

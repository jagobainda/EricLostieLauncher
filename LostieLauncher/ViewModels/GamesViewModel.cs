using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.Views.Dialogs;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LostieLauncher.ViewModels;

public partial class GamesViewModel : ObservableObject, IDisposable
{
    private readonly IContentService _contentService;
    private readonly LibraryViewModel _libraryViewModel;
    private const string HelpFolderName = "ayuda";
    private bool _disposed;

    public event Action? NavigateToLibraryRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial ObservableCollection<InstalledGameInfo> InstalledGames { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsListVisible))]
    public partial bool IsLoading { get; set; }

    public bool IsEmpty => !IsLoading && InstalledGames.Count == 0;
    public bool IsListVisible => !IsLoading && InstalledGames.Count > 0;

    public GamesViewModel(IContentService contentService, LibraryViewModel libraryViewModel)
    {
        _contentService = contentService;
        _libraryViewModel = libraryViewModel;
        _libraryViewModel.GameInstalled += OnGameInstalled;
        _ = LoadInstalledGamesAsync(waitForLibrary: true);
    }

    public async Task RefreshAsync() => await LoadInstalledGamesAsync(waitForLibrary: false);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _libraryViewModel.GameInstalled -= OnGameInstalled;
        GC.SuppressFinalize(this);
    }

    private async void OnGameInstalled(string gameName, string version, string? tipo)
    {
        try
        {
            var playtimes = await _contentService.GetAllPlaytimesAsync().ConfigureAwait(false);

            var app = Application.Current;
            if (app is null) return;

            app.Dispatcher.Invoke(() =>
            {
                var remote = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
                var local = new LocalGameInfo { Id = remote?.Id ?? Guid.Empty, Nombre = gameName, Version = version, Tipo = tipo };
                var info = BuildInstalledGameInfo(local, playtimes);

                var existing = InstalledGames.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
                if (existing != null) InstalledGames.Remove(existing);
                InstalledGames.Add(info);
            });

            Logs.DebugLogManager($"Games list updated after install: {gameName} {VersionUtils.FormatDisplayVersion(version)}{(tipo is not null ? $" ({tipo})" : "")}.");
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
    }

    private async Task LoadInstalledGamesAsync(bool waitForLibrary = true)
    {
        IsLoading = true;

        try
        {
            if (waitForLibrary) await _libraryViewModel.LibraryLoadedTask;

            var localGames = await _contentService.GetLocalGamesAsync();
            var playtimes = await _contentService.GetAllPlaytimesAsync();

            IEnumerable<InstalledGameInfo> installed = [.. localGames.Select(local => BuildInstalledGameInfo(local, playtimes))];

            InstalledGames = new ObservableCollection<InstalledGameInfo>(installed);
            Logs.DebugLogManager($"Installed games loaded: {InstalledGames.Count} games.");
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
        finally
        {
            IsLoading = false;
        }

        if (!waitForLibrary || !SettingsViewModel.Instance.AutoUpdate) return;
        foreach (var game in InstalledGames.Where(g => g.HasUpdate && !g.IsSpecialVersion).ToList()) await UpdateCoreAsync(game.Nombre, navigateToLibrary: false);
    }

    [RelayCommand]
    private Task UpdateAsync(string gameName) => UpdateCoreAsync(gameName, navigateToLibrary: true);

    internal InstalledGameInfo BuildInstalledGameInfo(LocalGameInfo local, IReadOnlyDictionary<Guid, int> playtimes)
    {
        var remote = _libraryViewModel.Games.FirstOrDefault(r => (local.Id != Guid.Empty && r.Id == local.Id) || string.Equals(r.Nombre, local.Nombre, StringComparison.OrdinalIgnoreCase));
        var hasUpdate = remote != null && Utils.VersionUtils.IsNewerVersion(remote.Version, local.Version);
        var playtimeMinutes = local.Id != Guid.Empty && playtimes.TryGetValue(local.Id, out var pt) ? pt : 0;

        return new InstalledGameInfo
        {
            Id = local.Id,
            Nombre = local.Nombre,
            InstalledVersion = local.Version,
            HasUpdate = hasUpdate,
            UpdateVersion = hasUpdate && remote != null ? remote.Version : string.Empty,
            Logo = remote?.Logo ?? string.Empty,
            Tipo = local.Tipo,
            PlaytimeMinutes = playtimeMinutes,
            HasHelpFolder = SafeHasHelpFolder(local.Nombre)
        };
    }

    private bool SafeHasHelpFolder(string gameName)
    {
        try
        {
            return HasHelpSubfolder(_contentService.GetGameDirectory(gameName));
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
            return false;
        }
    }

    private static bool HasHelpSubfolder(string gameDir)
    {
        if (!Directory.Exists(gameDir)) return false;
        return Directory.EnumerateDirectories(gameDir)
            .Any(d => string.Equals(Path.GetFileName(d), HelpFolderName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task UpdateCoreAsync(string gameName, bool navigateToLibrary)
    {
        Logs.DebugLogManager($"Starting update: {gameName}.");
        var libraryGame = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        if (libraryGame is null) return;

        var installedGame = InstalledGames.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        installedGame?.IsUpdating = true;

        if (navigateToLibrary) NavigateToLibraryRequested?.Invoke();

        var args = new GameDownloadArgs(libraryGame.GameId, libraryGame.Version, libraryGame.RutaRelativa);
        await _libraryViewModel.StartUpdateCommand.ExecuteAsync(args);

        installedGame?.IsUpdating = false;
        Logs.DebugLogManager($"Update completed: {gameName}.");
    }

    [RelayCommand]
    private void NavigateToLibrary() => NavigateToLibraryRequested?.Invoke();

    [RelayCommand]
    private async Task SwitchToSpecialVersionAsync(string gameName)
    {
        Logs.DebugLogManager($"Switching to special version: {gameName}.");
        var libraryGame = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        if (libraryGame is null) return;

        NavigateToLibraryRequested?.Invoke();

        var args = new GameDownloadArgs(libraryGame.GameId, libraryGame.Version, libraryGame.RutaRelativa);
        await _libraryViewModel.SwitchToSpecialVersionCommand.ExecuteAsync(args);
    }

    [RelayCommand]
    private void Play(string gameName)
    {
        Logs.DebugLogManager($"Launching game: {gameName}.");
        var exePath = Path.Combine(_contentService.GetGameDirectory(gameName), "Game.exe");
        var installedGame = InstalledGames.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        var gameGuid = installedGame?.Id ?? Guid.Empty;

        if (!File.Exists(exePath))
        {
            Logs.InfoLogManager($"Game executable not found for '{gameName}' at: {exePath}");
            CustomMessageBox.Show(SettingsViewModel.Instance.Strings.GameExeNotFoundTitle, SettingsViewModel.Instance.Strings.GameExeNotFoundMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
            return;
        }

        try
        {
            var startTime = DateTime.UtcNow;
            var process = Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exePath)! });
            if (process is not null)
            {
                Logs.InfoLogManager($"Game launched: {gameName} (PID: {process.Id}).");
                SetMainWindowState(WindowState.Minimized);
                TrackPlaySession(process, gameName, gameGuid, startTime);
            }
        }
        catch (Exception ex) { Logs.ErrorLogManager(ex); }
    }

    private void TrackPlaySession(Process process, string gameName, Guid gameGuid, DateTime startTime)
    {
        var handled = 0;
        Task RunOnce() => Interlocked.Exchange(ref handled, 1) == 0
            ? OnGameExitedAsync(process, gameName, gameGuid, startTime)
            : Task.CompletedTask;

        var exitHandler = AsyncEventHandler.Wrap((_, _) => RunOnce());
        process.Exited += exitHandler;
        process.EnableRaisingEvents = true;
        Logs.DebugLogManager($"Tracking play session for: {gameName}.");

        if (process.HasExited) exitHandler.Invoke(process, EventArgs.Empty);
    }

    private async Task OnGameExitedAsync(Process process, string gameName, Guid gameGuid, DateTime startTime)
    {
        using (process)
        {
            var minutes = (int)(DateTime.UtcNow - startTime).TotalMinutes;
            Logs.DebugLogManager($"Game process exited: {gameName}. Session: {minutes} min.");

            await RecordPlaySessionAsync(gameGuid, minutes).ConfigureAwait(false);
            ApplyPlaytimeAndRestoreWindow(gameName, minutes);
        }
    }

    internal Task RecordPlaySessionAsync(Guid gameGuid, int minutes)
    {
        if (minutes <= 0 || gameGuid == Guid.Empty) return Task.CompletedTask;
        return _contentService.AddPlaytimeAsync(gameGuid, minutes);
    }

    private void ApplyPlaytimeAndRestoreWindow(string gameName, int minutes)
    {
        try
        {
            var app = Application.Current;
            if (app is null) return;

            app.Dispatcher.Invoke(() =>
            {
                if (minutes > 0)
                {
                    var installedGame = InstalledGames.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
                    installedGame?.PlaytimeMinutes += minutes;
                    var libraryGame = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
                    libraryGame?.PlaytimeMinutes += minutes;
                }

                if (app.MainWindow is { } mainWindow)
                {
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            });
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
    }

    private static void SetMainWindowState(WindowState state)
    {
        if (Application.Current?.MainWindow is { } mainWindow)
            mainWindow.WindowState = state;
    }

    [RelayCommand]
    private async Task OpenFolderAsync(string gameName)
    {
        Logs.DebugLogManager($"Opening folder for: {gameName}.");
        var path = _contentService.GetGameDirectory(gameName);

        if (!Directory.Exists(path))
        {
            Logs.InfoLogManager($"Game folder not found for '{gameName}', offering to download.");
            var result = CustomMessageBox.Show(SettingsViewModel.Instance.Strings.FolderNotFoundTitle, SettingsViewModel.Instance.Strings.FolderNotFoundMessage, CustomMessageBoxButton.YesNo, CustomMessageBoxIcon.Information);

            if (result == true)
            {
                var libraryGame = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
                if (libraryGame is null) return;

                NavigateToLibraryRequested?.Invoke();
                var args = new GameDownloadArgs(libraryGame.GameId, libraryGame.Version, libraryGame.RutaRelativa);
                await _libraryViewModel.StartDownloadCommand.ExecuteAsync(args);
            }
            return;
        }

        FolderLauncher.OpenFolder(path);
    }

    [RelayCommand]
    private void OpenHelpFolder(string gameName)
    {
        Logs.DebugLogManager($"Opening help folder for: {gameName}.");
        var gameDir = _contentService.GetGameDirectory(gameName);

        if (!Directory.Exists(gameDir))
        {
            Logs.DebugLogManager($"Game folder not found for: {gameName}.");
            return;
        }

        var helpDir = Directory.EnumerateDirectories(gameDir)
            .FirstOrDefault(d => string.Equals(Path.GetFileName(d), HelpFolderName, StringComparison.OrdinalIgnoreCase));

        if (helpDir is null)
        {
            Logs.DebugLogManager($"No help folder found for: {gameName}.");
            return;
        }

        FolderLauncher.OpenFolder(helpDir);
    }

    [RelayCommand]
    private async Task UninstallAsync(string gameName)
    {
        var strings = SettingsViewModel.Instance.Strings;

        var confirm = CustomMessageBox.Show(strings.UninstallConfirmTitle, string.Format(strings.UninstallConfirmMessage, gameName), CustomMessageBoxButton.YesNo, CustomMessageBoxIcon.Information);

        if (confirm != true)
        {
            Logs.DebugLogManager($"Uninstall cancelled by user: {gameName}.");
            return;
        }

        Logs.InfoLogManager($"Uninstalling game: {gameName}.");
        var path = _contentService.GetGameDirectory(gameName);
        var folderExisted = Directory.Exists(path);

        var target = InstalledGames.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        target?.IsUninstalling = true;

        if (folderExisted)
        {
            try
            {
                await Task.Run(() => Directory.Delete(path, recursive: true));
            }
            catch (Exception ex)
            {
                Logs.ErrorLogManager(ex);
                target?.IsUninstalling = false;
                CustomMessageBox.Show(strings.UninstallErrorTitle, strings.UninstallErrorMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Error);
                return;
            }
        }

        await _contentService.RemoveGameRegistryAsync(gameName);

        if (target != null)
        {
            InstalledGames.Remove(target);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsListVisible));
        }

        var libraryGame = _libraryViewModel.Games.FirstOrDefault(g => string.Equals(g.Nombre, gameName, StringComparison.OrdinalIgnoreCase));
        libraryGame?.DownloadStatus = GameDownloadStatus.Available;

        Logs.InfoLogManager($"Game uninstalled: {gameName}.");

        if (!folderExisted) CustomMessageBox.Show(strings.UninstallNotFoundTitle, strings.UninstallNotFoundMessage, CustomMessageBoxButton.OK, CustomMessageBoxIcon.Information);
    }
}

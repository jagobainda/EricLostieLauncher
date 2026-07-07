using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace LostieLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GlobalViewModel _globalViewModel;
    private readonly HomeViewModel _homeViewModel;
    private readonly GamesViewModel _gamesViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly FaqsViewModel _faqsViewModel;
    private readonly SettingsViewModel _settingsViewModel;

    [ObservableProperty]
    public partial string CurrentTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeActive))]
    [NotifyPropertyChangedFor(nameof(IsGamesActive))]
    [NotifyPropertyChangedFor(nameof(IsLibraryActive))]
    [NotifyPropertyChangedFor(nameof(IsFaqsActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    public partial ObservableObject CurrentViewModel { get; set; } = null!;

    public bool IsHomeActive => CurrentViewModel is HomeViewModel;
    public bool IsGamesActive => CurrentViewModel is GamesViewModel;
    public bool IsLibraryActive => CurrentViewModel is LibraryViewModel;
    public bool IsFaqsActive => CurrentViewModel is FaqsViewModel;
    public bool IsSettingsActive => CurrentViewModel is SettingsViewModel;
    public bool IsOfflineMode => _homeViewModel.IsOfflineMode;

    public MainViewModel(GlobalViewModel globalViewModel, HomeViewModel homeViewModel, GamesViewModel gamesViewModel, LibraryViewModel libraryViewModel, FaqsViewModel faqsViewModel, SettingsViewModel settingsViewModel)
    {
        _globalViewModel = globalViewModel;
        _homeViewModel = homeViewModel;
        _gamesViewModel = gamesViewModel;
        _libraryViewModel = libraryViewModel;
        _faqsViewModel = faqsViewModel;
        _settingsViewModel = settingsViewModel;
        CurrentViewModel = _homeViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleHome;

        _gamesViewModel.NavigateToLibraryRequested += () =>
        {
            CurrentViewModel = _libraryViewModel;
            CurrentTitle = _settingsViewModel.Strings.TitleLibrary;
        };

        _settingsViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.Strings)) UpdateCurrentTitle();
            if (e.PropertyName == nameof(SettingsViewModel.DownloadDirectory)) _ = _libraryViewModel.RefreshAsync();
        };

        _globalViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GlobalViewModel.IsDownloading) or nameof(GlobalViewModel.IsRefreshing)) RefreshDataCommand.NotifyCanExecuteChanged();
        };

        _libraryViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.IsLoading)) RefreshDataCommand.NotifyCanExecuteChanged();
        };

        _gamesViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GamesViewModel.IsLoading)) RefreshDataCommand.NotifyCanExecuteChanged();
        };

        _homeViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.IsLoading)) RefreshDataCommand.NotifyCanExecuteChanged();
            if (e.PropertyName == nameof(HomeViewModel.IsOfflineMode)) OnPropertyChanged(nameof(IsOfflineMode));
        };
    }

    private void UpdateCurrentTitle() => CurrentTitle = CurrentViewModel switch
    {
        HomeViewModel => _settingsViewModel.Strings.TitleHome,
        GamesViewModel => _settingsViewModel.Strings.TitleGames,
        LibraryViewModel => _settingsViewModel.Strings.TitleLibrary,
        FaqsViewModel => _settingsViewModel.Strings.TitleFaqs,
        _ => _settingsViewModel.Strings.TitleSettings
    };

    [RelayCommand]
    private void NavigateToHome()
    {
        Logs.DebugLogManager("Navigating to Home.");
        CurrentViewModel = _homeViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleHome;
    }

    [RelayCommand]
    private void NavigateToGames()
    {
        Logs.DebugLogManager("Navigating to Games.");
        CurrentViewModel = _gamesViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleGames;
    }

    [RelayCommand]
    private void NavigateToLibrary()
    {
        Logs.DebugLogManager("Navigating to Library.");
        CurrentViewModel = _libraryViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleLibrary;
    }

    [RelayCommand]
    private void NavigateToFaqs()
    {
        Logs.DebugLogManager("Navigating to FAQs.");
        CurrentViewModel = _faqsViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleFaqs;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        Logs.DebugLogManager("Navigating to Settings.");
        CurrentViewModel = _settingsViewModel;
        CurrentTitle = _settingsViewModel.Strings.TitleSettings;
    }

    [RelayCommand]
    private static void OpenSavedGames()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games");

        if (Directory.Exists(path))
        {
            FolderLauncher.OpenFolder(path);
        }
        else
        {
            Logs.DebugLogManager($"Saved Games folder not found: {path}");
        }
    }

    [RelayCommand]
    private static void OpenGitHub() => UrlLauncher.OpenHttps("https://github.com/jagobainda/LostieLauncher");

    [RelayCommand]
    private static void OpenTwitch() => UrlLauncher.OpenHttps("https://www.twitch.tv/ericlostie");

    [RelayCommand]
    private static void OpenYouTube() => UrlLauncher.OpenHttps("https://www.youtube.com/@EricLostie");

    [RelayCommand]
    private static void OpenTwitter() => UrlLauncher.OpenHttps("https://x.com/Eric_Lostie");

    [RelayCommand(CanExecute = nameof(CanRefreshData))]
    private async Task RefreshDataAsync()
    {
        Logs.DebugLogManager("Data refresh started.");
        _globalViewModel.IsRefreshing = true;
        try
        {
            await Task.WhenAll(_homeViewModel.RefreshAsync(), _libraryViewModel.RefreshAsync());
            await _gamesViewModel.RefreshAsync();
            Logs.DebugLogManager("Data refresh completed.");
        }
        catch (Exception ex)
        {
            Logs.ErrorLogManager(ex);
        }
        finally
        {
            _globalViewModel.IsRefreshing = false;
        }
    }

    private bool CanRefreshData() => !_globalViewModel.IsDownloading &&
        !_globalViewModel.IsRefreshing &&
        !_homeViewModel.IsLoading &&
        !_libraryViewModel.IsLoading &&
        !_gamesViewModel.IsLoading;
}


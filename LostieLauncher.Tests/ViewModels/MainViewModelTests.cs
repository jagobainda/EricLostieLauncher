using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;

namespace LostieLauncher.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class MainViewModelTests
{
    private readonly IContentService _contentService = Substitute.For<IContentService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IWindowsStartupService _startupService = Substitute.For<IWindowsStartupService>();
    private readonly IDownloadService _downloadService = Substitute.For<IDownloadService>();
    private readonly DownloadOptions _downloadOptions = new(BaseUrl: "https://download.test");

    public MainViewModelTests(WpfApplicationFixture _)
    {
        _settingsService.Load().Returns(new AppSettings());
        _contentService.GetGamesAsync().Returns([]);
        _contentService.GetLocalGamesAsync().Returns([]);
        _contentService.GetAllPlaytimesAsync().Returns(new Dictionary<Guid, int>());
        _contentService.GetHomeContentAsync(Arg.Any<bool>()).Returns(new HomeContent());
        _contentService.IsServerActionBlockedAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(false);
        _contentService.GetGameDirectory(Arg.Any<string>()).Returns(ci =>
            Path.Combine(Path.GetTempPath(), "LostieLauncherTests-nonexistent", ci.Arg<string>()!));
    }

    private async Task<MainViewModel> CreateSutAsync()
    {
        var global = new GlobalViewModel();
        var settings = new SettingsViewModel(_settingsService, _startupService, global, Substitute.For<IUpdateService>());
        var library = new LibraryViewModel(_contentService, _settingsService, _downloadService, global, _downloadOptions);
        await library.LibraryLoadedTask;
        var home = new HomeViewModel(_contentService, settings);
        await home.RefreshAsync();
        var games = new GamesViewModel(_contentService, library, global);
        await games.RefreshAsync();
        var faqs = new FaqsViewModel(settings);
        return new MainViewModel(global, home, games, library, faqs, settings);
    }

    [Fact]
    public async Task Constructor_DefaultsToHomeViewModelAsCurrent()
    {
        // Arrange & Act
        var vm = await CreateSutAsync();

        // Assert
        vm.IsHomeActive.ShouldBeTrue();
        vm.IsGamesActive.ShouldBeFalse();
        vm.IsLibraryActive.ShouldBeFalse();
        vm.IsSettingsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task NavigateToGamesCommand_SwitchesCurrentViewModelAndUpdatesTitle()
    {
        // Arrange
        var vm = await CreateSutAsync();
        var expectedTitle = SettingsViewModel.Instance.Strings.TitleGames;

        // Act
        vm.NavigateToGamesCommand.Execute(null);

        // Assert
        vm.IsGamesActive.ShouldBeTrue();
        vm.CurrentTitle.ShouldBe(expectedTitle);
    }

    [Fact]
    public async Task NavigateToLibraryCommand_SwitchesCurrentViewModelAndUpdatesTitle()
    {
        // Arrange
        var vm = await CreateSutAsync();
        var expectedTitle = SettingsViewModel.Instance.Strings.TitleLibrary;

        // Act
        vm.NavigateToLibraryCommand.Execute(null);

        // Assert
        vm.IsLibraryActive.ShouldBeTrue();
        vm.CurrentTitle.ShouldBe(expectedTitle);
    }

    [Fact]
    public async Task NavigateToSettingsCommand_SwitchesCurrentViewModelAndUpdatesTitle()
    {
        // Arrange
        var vm = await CreateSutAsync();
        var expectedTitle = SettingsViewModel.Instance.Strings.TitleSettings;

        // Act
        vm.NavigateToSettingsCommand.Execute(null);

        // Assert
        vm.IsSettingsActive.ShouldBeTrue();
        vm.CurrentTitle.ShouldBe(expectedTitle);
    }

    [Fact]
    public async Task NavigateToHomeCommand_FromAnotherView_SwitchesBackToHome()
    {
        // Arrange
        var vm = await CreateSutAsync();
        vm.NavigateToSettingsCommand.Execute(null);

        // Act
        vm.NavigateToHomeCommand.Execute(null);

        // Assert
        vm.IsHomeActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrentViewModelChange_RaisesPropertyChangedForActiveFlags()
    {
        // Arrange
        var vm = await CreateSutAsync();
        using var recorder = new PropertyChangedRecorder(vm);

        // Act
        vm.NavigateToGamesCommand.Execute(null);

        // Assert
        recorder.WasRaised(nameof(MainViewModel.IsHomeActive)).ShouldBeTrue();
        recorder.WasRaised(nameof(MainViewModel.IsGamesActive)).ShouldBeTrue();
        recorder.WasRaised(nameof(MainViewModel.IsLibraryActive)).ShouldBeTrue();
        recorder.WasRaised(nameof(MainViewModel.IsSettingsActive)).ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshDataCommand_CanExecute_ReturnsFalseWhileGlobalIsDownloading()
    {
        // Arrange
        var global = new GlobalViewModel();
        var settings = new SettingsViewModel(_settingsService, _startupService, global, Substitute.For<IUpdateService>());
        var library = new LibraryViewModel(_contentService, _settingsService, _downloadService, global, _downloadOptions);
        await library.LibraryLoadedTask;
        var home = new HomeViewModel(_contentService, settings);
        await home.RefreshAsync();
        var games = new GamesViewModel(_contentService, library, global);
        await games.RefreshAsync();
        var faqs = new FaqsViewModel(settings);
        var vm = new MainViewModel(global, home, games, library, faqs, settings);

        // Act
        global.IsDownloading = true;

        // Assert
        vm.RefreshDataCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshDataCommand_CanExecute_ReturnsTrueWhenIdle()
    {
        // Arrange
        var vm = await CreateSutAsync();

        // Act & Assert
        vm.RefreshDataCommand.CanExecute(null).ShouldBeTrue();
    }
}

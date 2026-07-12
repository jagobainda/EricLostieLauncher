using LostieLauncher.Content;
using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;

namespace LostieLauncher.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class SettingsViewModelTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IWindowsStartupService _startupService = Substitute.For<IWindowsStartupService>();
    private readonly GlobalViewModel _globalViewModel = new();
    private readonly IUpdateService _updateService = Substitute.For<IUpdateService>();

    public SettingsViewModelTests(WpfApplicationFixture _) { /* fixture ensures Application.Current */ }

    private SettingsViewModel CreateSut(AppSettings? initial = null, bool startupEnabled = false)
    {
        var settings = initial ?? new AppSettings();
        _settingsService.Load().Returns(settings);
        _startupService.IsEnabled().Returns(startupEnabled);
        _startupService.Enable().Returns(true);
        _startupService.Disable().Returns(true);
        return new SettingsViewModel(_settingsService, _startupService, _globalViewModel, _updateService);
    }

    // -------------------- LoadSettings (constructor) --------------------

    [Fact]
    public void Constructor_LoadsValuesFromSettingsService()
    {
        // Arrange
        var stored = new AppSettings
        {
            Language = AppLanguage.Eng,
            Theme = AppTheme.Empoleon,
            StartMinimized = true,
            AutoUpdate = true,
            DownloadDirectory = @"C:\games"
        };

        // Act
        var vm = CreateSut(stored, startupEnabled: true);

        // Assert
        vm.Language.ShouldBe(AppLanguage.Eng);
        vm.Theme.ShouldBe(AppTheme.Empoleon);
        vm.StartMinimized.ShouldBeTrue();
        vm.AutoUpdate.ShouldBeTrue();
        vm.DownloadDirectory.ShouldBe(@"C:\games");
        vm.StartWithWindows.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_EnsuresGamesRootDirectoryExists()
    {
        // Arrange & Act
        _ = CreateSut();

        // Assert
        _settingsService.Received(1).EnsureGamesRootDirectoryExists();
    }

    [Fact]
    public void Constructor_DoesNotPersistDuringInitialLoad()
    {
        // Arrange & Act — populating the VM from disk should never trigger a Save round-trip.
        _ = CreateSut(new AppSettings { Language = AppLanguage.Cat });

        // Assert
        _settingsService.DidNotReceive().Save(Arg.Any<AppSettings>());
    }

    [Fact]
    public void Constructor_AssignsItselfToInstanceSingleton()
    {
        // Arrange & Act
        var vm = CreateSut();

        // Assert — production code (e.g. dialogs) reads SettingsViewModel.Instance.
        SettingsViewModel.Instance.ShouldBeSameAs(vm);
    }

    // -------------------- Instance fail-fast (BUG-066) --------------------

    [Fact]
    public void ResolveInstance_BeforeConstruction_ThrowsDiagnosticInsteadOfReturningNull()
    {
        // Arrange — the singleton has not been built yet (null backing field). The old `null!`
        // auto-property silently returned null here, deferring the failure to an obscure NRE far
        // from the cause. Act & Assert — now it fails fast with an actionable message (BUG-066).
        var ex = Should.Throw<InvalidOperationException>(() => SettingsViewModel.ResolveInstance(null));
        ex.Message.ShouldContain(nameof(SettingsViewModel.Instance));
    }

    [Fact]
    public void ResolveInstance_AfterConstruction_ReturnsTheSameInstance()
    {
        // Arrange
        var vm = CreateSut();

        // Act & Assert — once the DI container builds the singleton, resolution returns it as-is.
        SettingsViewModel.ResolveInstance(vm).ShouldBeSameAs(vm);
    }

    // -------------------- Language --------------------

    [Theory]
    [InlineData(AppLanguage.Eng, typeof(Eng))]
    [InlineData(AppLanguage.Cat, typeof(Cat))]
    [InlineData(AppLanguage.Eus, typeof(Eus))]
    [InlineData(AppLanguage.Gal, typeof(Gal))]
    [InlineData(AppLanguage.Por, typeof(Por))]
    [InlineData(AppLanguage.Val, typeof(Val))]
    [InlineData(AppLanguage.Fra, typeof(Fra))]
    [InlineData(AppLanguage.Esp, typeof(Esp))]
    public void Language_WhenChanged_ReplacesStringsWithMatchingResource(AppLanguage lang, Type expectedStringsType)
    {
        // Arrange
        var vm = CreateSut();

        // Act
        vm.Language = lang;

        // Assert
        vm.Strings.ShouldBeOfType(expectedStringsType);
    }

    [Fact]
    public void Language_WhenChanged_PersistsSettings()
    {
        // Arrange
        var vm = CreateSut();
        _settingsService.ClearReceivedCalls();

        // Act
        vm.Language = AppLanguage.Eng;

        // Assert
        _settingsService.Received(1).Save(Arg.Is<AppSettings>(s => s!.Language == AppLanguage.Eng));
    }

    [Fact]
    public void Language_WhenChanged_RaisesPropertyChangedForBothLanguageAndStrings()
    {
        // Arrange
        var vm = CreateSut();
        using var recorder = new PropertyChangedRecorder(vm);

        // Act
        vm.Language = AppLanguage.Eng;

        // Assert
        recorder.WasRaised(nameof(SettingsViewModel.Language)).ShouldBeTrue();
        recorder.WasRaised(nameof(SettingsViewModel.Strings)).ShouldBeTrue();
    }

    // -------------------- StartWithWindows --------------------

    [Fact]
    public void StartWithWindows_WhenSetToTrueAfterLoad_EnablesStartupService()
    {
        // Arrange
        var vm = CreateSut(startupEnabled: false);

        // Act
        vm.StartWithWindows = true;

        // Assert
        _startupService.Received(1).Enable();
    }

    [Fact]
    public void StartWithWindows_WhenSetToFalseAfterLoad_DisablesStartupService()
    {
        // Arrange
        var vm = CreateSut(startupEnabled: true);

        // Act
        vm.StartWithWindows = false;

        // Assert
        _startupService.Received(1).Disable();
    }

    [Fact]
    public void StartWithWindows_WhenEnableFails_RevertsToggleToRealState()
    {
        // Arrange — the registry write fails (ProcessPath unavailable / run key not writable).
        var vm = CreateSut(startupEnabled: false);
        _startupService.Enable().Returns(false);
        _startupService.IsEnabled().Returns(false);

        // Act
        vm.StartWithWindows = true;

        // Assert — the toggle must not lie: it reverts to the real (off) state (BUG-052).
        _startupService.Received(1).Enable();
        vm.StartWithWindows.ShouldBeFalse();
    }

    [Fact]
    public void StartWithWindows_WhenDisableFails_RevertsToggleToRealState()
    {
        // Arrange — startup is on; the user turns it off but the registry delete fails.
        var vm = CreateSut(startupEnabled: true);
        _startupService.Disable().Returns(false);
        _startupService.IsEnabled().Returns(true);

        // Act
        vm.StartWithWindows = false;

        // Assert — reverts back to the real (on) state instead of showing a false "off".
        _startupService.Received(1).Disable();
        vm.StartWithWindows.ShouldBeTrue();
    }

    [Fact]
    public void StartWithWindows_WhenEnableSucceeds_KeepsToggleOnAndPersists()
    {
        // Arrange
        var vm = CreateSut(startupEnabled: false);
        _startupService.Enable().Returns(true);
        _settingsService.ClearReceivedCalls();

        // Act
        vm.StartWithWindows = true;

        // Assert — successful write keeps the toggle on and persists settings (no revert).
        vm.StartWithWindows.ShouldBeTrue();
        _settingsService.Received(1).Save(Arg.Any<AppSettings>());
    }

    [Fact]
    public void StartWithWindows_DuringInitialLoad_DoesNotInvokeStartupService()
    {
        // Arrange & Act — the constructor reads IsEnabled() and assigns the property; that
        // assignment must NOT trigger Enable/Disable (that's the user's job).
        _ = CreateSut(startupEnabled: true);

        // Assert
        _startupService.DidNotReceive().Enable();
        _startupService.DidNotReceive().Disable();
    }

    // -------------------- Boolean toggles --------------------

    [Fact]
    public void StartMinimized_WhenChanged_PersistsSettings()
    {
        // Arrange
        var vm = CreateSut();
        _settingsService.ClearReceivedCalls();

        // Act
        vm.StartMinimized = true;

        // Assert
        _settingsService.Received(1).Save(Arg.Is<AppSettings>(s => s!.StartMinimized));
    }

    [Fact]
    public void AutoUpdate_WhenChanged_PersistsSettings()
    {
        // Arrange
        var vm = CreateSut();
        _settingsService.ClearReceivedCalls();

        // Act
        vm.AutoUpdate = true;

        // Assert
        _settingsService.Received(1).Save(Arg.Is<AppSettings>(s => s!.AutoUpdate));
    }

    // -------------------- DownloadDirectory --------------------

    [Fact]
    public void DownloadDirectory_WhenChanged_PersistsAndEnsuresDirectoryExists()
    {
        // Arrange
        var vm = CreateSut();
        _settingsService.ClearReceivedCalls();

        // Act
        vm.DownloadDirectory = @"D:\NewFolder";

        // Assert
        _settingsService.Received(1).Save(Arg.Is<AppSettings>(s => s!.DownloadDirectory == @"D:\NewFolder"));
        _settingsService.Received(1).EnsureGamesRootDirectoryExists();
    }

    // -------------------- MarkWelcomeSeen --------------------

    [Fact]
    public void MarkWelcomeSeen_PersistsHasSeenWelcomeAsTrue()
    {
        // Arrange — initial settings indicate user has NOT seen the welcome dialog yet.
        var vm = CreateSut(new AppSettings { HasSeenWelcome = false });
        _settingsService.ClearReceivedCalls();

        // Act
        vm.MarkWelcomeSeen();

        // Assert
        vm.HasSeenWelcome.ShouldBeTrue();
        _settingsService.Received(1).Save(Arg.Is<AppSettings>(s => s!.HasSeenWelcome));
    }

    [Fact]
    public void HasSeenWelcome_WhenInitialSettingsHadFlagSet_ReflectsStoredValue()
    {
        // Arrange
        var vm = CreateSut(new AppSettings { HasSeenWelcome = true });

        // Act
        var seen = vm.HasSeenWelcome;

        // Assert
        seen.ShouldBeTrue();
    }

    // -------------------- CheckForUpdates (BUG-022) --------------------

    [Fact]
    public async Task CheckForUpdates_WhenDownloadInProgress_NotifiesUserAndDoesNotCheck()
    {
        // Arrange — a download is active; restarting/checking now could corrupt it (BUG-022).
        var vm = CreateSut();
        _globalViewModel.IsDownloading = true;

        // Act
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        // Assert — the guard blocks the check entirely and tells the user why.
        _updateService.Received(1).NotifyDownloadInProgress();
        await _updateService.DidNotReceive().CheckForUpdatesAsync(Arg.Any<bool>());
    }

    [Fact]
    public async Task CheckForUpdates_WhenNotDownloading_DelegatesToUpdateServiceNotifyingWhenUpToDate()
    {
        // Arrange — no download in progress; the manual check should proceed.
        var vm = CreateSut();
        _globalViewModel.IsDownloading = false;

        // Act
        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        // Assert — runs the real Velopack check and asks to be told when already up to date.
        await _updateService.Received(1).CheckForUpdatesAsync(true);
        _updateService.DidNotReceive().NotifyDownloadInProgress();
    }

    // -------------------- FormatVersion (BUG-016) --------------------

    [Fact]
    public void FormatVersion_FourComponentAssemblyVersion_TrimsToThreeComponents()
    {
        // Arrange — the production assembly version is 4-component (e.g. 0.8.11.0).
        var version = new Version(0, 8, 11, 0);

        // Act
        var formatted = SettingsViewModel.FormatVersion(version);

        // Assert — aligns with the 3-component convention used elsewhere (VersionUtils).
        formatted.ShouldBe("v0.8.11");
    }

    [Fact]
    public void FormatVersion_ThreeComponentVersion_KeepsAllThree()
    {
        // Arrange
        var version = new Version(1, 2, 3);

        // Act
        var formatted = SettingsViewModel.FormatVersion(version);

        // Assert
        formatted.ShouldBe("v1.2.3");
    }

    [Fact]
    public void FormatVersion_TwoComponentVersion_DoesNotThrowAndKeepsAvailableFields()
    {
        // Arrange — a Version with only major.minor would make ToString(3) throw; the
        // clamp on available fields must keep it safe.
        var version = new Version(1, 5);

        // Act
        var formatted = SettingsViewModel.FormatVersion(version);

        // Assert
        formatted.ShouldBe("v1.5");
    }

    [Fact]
    public void FormatVersion_NullVersion_ReturnsUnknownFallback()
    {
        // Act — the regression of BUG-016: the fallback must actually be reachable.
        var formatted = SettingsViewModel.FormatVersion(null);

        // Assert
        formatted.ShouldBe("Unknown");
    }
}

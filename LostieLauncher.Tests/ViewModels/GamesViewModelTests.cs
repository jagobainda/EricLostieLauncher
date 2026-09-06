using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;
using System.Diagnostics;
using System.Reflection;

namespace LostieLauncher.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class GamesViewModelTests
{
    private readonly IContentService _contentService = Substitute.For<IContentService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDownloadService _downloadService = Substitute.For<IDownloadService>();
    private readonly DownloadOptions _downloadOptions = new(BaseUrl: "https://download.test");
    private readonly GlobalViewModel _globalViewModel = new();

    public GamesViewModelTests(WpfApplicationFixture _)
    {
        _contentService.GetGamesAsync().Returns([]);
        _contentService.GetLocalGamesAsync().Returns([]);
        _contentService.GetAllPlaytimesAsync().Returns(new Dictionary<Guid, int>());
        // GetGameDirectory is invoked for HasHelpSubfolder; return a path that does not exist
        // so that branch returns false without touching real disk.
        _contentService.GetGameDirectory(Arg.Any<string>()).Returns(ci => Path.Combine(Path.GetTempPath(), "LostieLauncherTests-nonexistent", ci.Arg<string>()!));
    }

    private LibraryViewModel CreateLibrary() => new(_contentService, _settingsService, _downloadService, _globalViewModel, _downloadOptions);

    private async Task<GamesViewModel> CreateSutAsync()
    {
        var library = CreateLibrary();
        await library.LibraryLoadedTask;
        var sut = new GamesViewModel(_contentService, library, _globalViewModel);
        // Wait for the constructor's fire-and-forget LoadInstalledGamesAsync to finish.
        await sut.RefreshAsync();
        return sut;
    }

    [Fact]
    public async Task Constructor_WithNoLocalGames_LeavesInstalledListEmpty()
    {
        // Arrange & Act
        var vm = await CreateSutAsync();

        // Assert
        vm.InstalledGames.ShouldBeEmpty();
        vm.IsLoading.ShouldBeFalse();
        vm.IsEmpty.ShouldBeTrue();
        vm.IsListVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadInstalledGames_PopulatesEntriesFromLocalRegistry()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Demo", version: "1.0.0", id: id)
        ]);
        _contentService.GetAllPlaytimesAsync().Returns(new Dictionary<Guid, int> { [id] = 15 });

        // Act
        var vm = await CreateSutAsync();

        // Assert
        var installed = vm.InstalledGames.Single();
        installed.Nombre.ShouldBe("Demo");
        installed.InstalledVersion.ShouldBe("1.0.0");
        installed.PlaytimeMinutes.ShouldBe(15);
    }

    [Fact]
    public async Task LoadInstalledGames_WhenOneGameHasInvalidName_StillLoadsTheOthers()
    {
        // Arrange — two installed games; GetGameDirectory throws for the malicious name (exactly as
        // the real service does for a traversal attempt) but succeeds for the valid one. Before
        // BUG-009 this single throw aborted the whole projection and left "My Games" empty.
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Good", version: "1.0.0", id: Guid.NewGuid()),
            TestData.LocalGame(name: "..\\evil", version: "1.0.0", id: Guid.NewGuid()),
        ]);
        _contentService.GetGameDirectory("..\\evil")
            .Returns(_ => throw new InvalidOperationException("escapes the games root"));

        // Act
        var vm = await CreateSutAsync();

        // Assert — both games are present and the invalid one simply has no help folder.
        vm.InstalledGames.Count.ShouldBe(2);
        vm.InstalledGames.Single(g => g.Nombre == "..\\evil").HasHelpFolder.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadInstalledGames_WhenRemoteVersionIsNewer_FlagsHasUpdate()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Demo", version: "2.0.0", id: id)
        ]);
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Demo", version: "1.0.0", id: id)
        ]);

        // Act
        var vm = await CreateSutAsync();

        // Assert
        var installed = vm.InstalledGames.Single();
        installed.HasUpdate.ShouldBeTrue();
        installed.UpdateVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task LoadInstalledGames_WhenRemoteAndLocalVersionsMatch_DoesNotFlagUpdate()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Demo", version: "1.0.0", id: id)
        ]);
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Demo", version: "1.0.0", id: id)
        ]);

        // Act
        var vm = await CreateSutAsync();

        // Assert
        var installed = vm.InstalledGames.Single();
        installed.HasUpdate.ShouldBeFalse();
        installed.UpdateVersion.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildInstalledGameInfo_PopulatesPlaytimeAndHelpFolder_ForAFreshlyInstalledCard()
    {
        // Arrange — a temp game dir WITH an "ayuda" subfolder. This is the exact path OnGameInstalled
        // now takes after install; before BUG-033 the post-install card was hand-built omitting both
        // PlaytimeMinutes (left at 0, wiping the value on updates) and HasHelpFolder (left false, so the
        // help button only appeared after a manual refresh).
        var id = Guid.NewGuid();
        var gameDir = Path.Combine(Path.GetTempPath(), "LostieLauncherTests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(gameDir, "ayuda"));
        try
        {
            _contentService.GetGameDirectory("Demo").Returns(gameDir);
            var vm = await CreateSutAsync();
            var local = TestData.LocalGame(name: "Demo", version: "1.0.0", id: id);
            var playtimes = new Dictionary<Guid, int> { [id] = 42 };

            // Act
            var info = vm.BuildInstalledGameInfo(local, playtimes);

            // Assert
            info.PlaytimeMinutes.ShouldBe(42);
            info.HasHelpFolder.ShouldBeTrue();
            info.Nombre.ShouldBe("Demo");
            info.InstalledVersion.ShouldBe("1.0.0");
        }
        finally
        {
            if (Directory.Exists(gameDir)) Directory.Delete(gameDir, recursive: true);
        }
    }

    [Fact]
    public async Task BuildInstalledGameInfo_WhenNoPlaytimeRecorded_LeavesPlaytimeAtZero()
    {
        // Arrange — empty playtime map and the fixture's non-existent game dir (no help folder).
        var vm = await CreateSutAsync();
        var local = TestData.LocalGame(name: "Demo", id: Guid.NewGuid());

        // Act
        var info = vm.BuildInstalledGameInfo(local, new Dictionary<Guid, int>());

        // Assert
        info.PlaytimeMinutes.ShouldBe(0);
        info.HasHelpFolder.ShouldBeFalse();
    }

    [Fact]
    public async Task BuildInstalledGameInfo_DerivesUpdateFlagFromRemoteCatalog()
    {
        // Arrange — remote catalog advertises a newer version than the installed one.
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([TestData.Game(name: "Demo", version: "2.0.0", id: id)]);
        var vm = await CreateSutAsync();
        var local = TestData.LocalGame(name: "Demo", version: "1.0.0", id: id);

        // Act — these are the other fields the old hand-built post-install card never computed.
        var info = vm.BuildInstalledGameInfo(local, new Dictionary<Guid, int>());

        // Assert
        info.HasUpdate.ShouldBeTrue();
        info.UpdateVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task NavigateToLibraryCommand_WhenExecuted_RaisesNavigateToLibraryRequested()
    {
        // Arrange
        var vm = await CreateSutAsync();
        var raised = false;
        vm.NavigateToLibraryRequested += () => raised = true;

        // Act
        vm.NavigateToLibraryCommand.Execute(null);

        // Assert
        raised.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_RecomputesInstalledList_FromContentService()
    {
        // Arrange
        var vm = await CreateSutAsync();
        _contentService.ClearReceivedCalls();

        // Act
        await vm.RefreshAsync();

        // Assert
        await _contentService.Received(1).GetLocalGamesAsync();
    }

    [Fact]
    public async Task RecordPlaySessionAsync_WhenMinutesPositive_PersistsPlaytimeForTheGame()
    {
        var vm = await CreateSutAsync();
        _contentService.ClearReceivedCalls();
        var id = Guid.NewGuid();

        await vm.RecordPlaySessionAsync(id, minutes: 25);

        await _contentService.Received(1).AddPlaytimeAsync(id, 25);
    }

    [Fact]
    public async Task RecordPlaySessionAsync_WhenMinutesIsZero_DoesNotPersistAnything()
    {
        var vm = await CreateSutAsync();
        _contentService.ClearReceivedCalls();

        await vm.RecordPlaySessionAsync(Guid.NewGuid(), minutes: 0);

        await _contentService.DidNotReceive().AddPlaytimeAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [Fact]
    public async Task RecordPlaySessionAsync_WhenGameIdIsEmpty_DoesNotPersistAnything()
    {
        var vm = await CreateSutAsync();
        _contentService.ClearReceivedCalls();

        await vm.RecordPlaySessionAsync(Guid.Empty, minutes: 30);

        await _contentService.DidNotReceive().AddPlaytimeAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromLibraryGameInstalled()
    {
        var library = CreateLibrary();
        await library.LibraryLoadedTask;
        var sut = new GamesViewModel(_contentService, library, _globalViewModel);
        await sut.RefreshAsync();
        GetGameInstalledSubscriberCount(library).ShouldBe(1);

        sut.Dispose();

        GetGameInstalledSubscriberCount(library).ShouldBe(0);
    }

    [Fact]
    public async Task OpenHelpFolder_WhenGameDirectoryDoesNotExist_DoesNotThrow()
    {
        // Arrange — GetGameDirectory already returns a non-existent path (fixture setup)
        var vm = await CreateSutAsync();
        _contentService.ClearReceivedCalls();

        // Act
        var act = () => vm.OpenHelpFolderCommand.Execute("MissingGame");

        // Assert — before the fix, Directory.EnumerateDirectories on a non-existent path threw
        // DirectoryNotFoundException; with the guard it returns without throwing.
        Should.NotThrow(act);
        _contentService.Received(1).GetGameDirectory("MissingGame");
    }

    [Fact]
    public async Task OpenHelpFolder_WhenGameDirectoryDoesNotExist_DoesNotEnumerateDirectories()
    {
        // Arrange — simulate a deleted directory by pointing GetGameDirectory to a
        // well-known temp path that definitely does not exist.
        var tempRoot = Path.Combine(Path.GetTempPath(), "LostieLauncherTests-DeletedGame-" + Guid.NewGuid());
        _contentService.GetGameDirectory("DeletedGame").Returns(tempRoot);
        var vm = await CreateSutAsync();

        // Sanity: the directory must NOT exist for the test to be meaningful.
        Directory.Exists(tempRoot).ShouldBeFalse();

        // Act
        vm.OpenHelpFolderCommand.Execute("DeletedGame");

        // Assert — GetGameDirectory was consulted (proves the command ran its path).
        _contentService.Received(1).GetGameDirectory("DeletedGame");
    }

    [Fact]
    public async Task OpenHelpFolder_WithValidDirectoryButNoHelpSubfolder_DoesNotThrow()
    {
        // Arrange — create a temp directory with NO "ayuda" subfolder.
        var tempRoot = Path.Combine(Path.GetTempPath(), "LostieLauncherTests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempRoot);
        try
        {
            _contentService.GetGameDirectory("NoHelp").Returns(tempRoot);
            var vm = await CreateSutAsync();

            // Act
            var act = () => vm.OpenHelpFolderCommand.Execute("NoHelp");

            // Assert — no "ayuda" subfolder → logs and returns, never reaches Process.Start.
            Should.NotThrow(act);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var sut = await CreateSutAsync();

        sut.Dispose();
        var act = sut.Dispose;

        Should.NotThrow(act);
    }

    private static int GetGameInstalledSubscriberCount(LibraryViewModel library)
    {
        var field = typeof(LibraryViewModel).GetField("GameInstalled", BindingFlags.Instance | BindingFlags.NonPublic);
        var subscribers = field?.GetValue(library) as Delegate;
        return subscribers?.GetInvocationList().Length ?? 0;
    }

    [Fact]
    public async Task TrackPlaySession_WhileTheGameIsRunning_MarksItAsRunning()
    {
        var vm = await CreateSutAsync();
        using var process = StartBlockingProcess();

        try
        {
            vm.TrackPlaySession(process, "Demo", Guid.Empty, DateTime.UtcNow);

            _globalViewModel.ActivePlaySessions.ShouldBe(1);
            _globalViewModel.IsGameRunning.ShouldBeTrue();
        }
        finally
        {
            process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public async Task TrackPlaySession_WhenTheGameProcessExits_ClosesTheSession()
    {
        var vm = await CreateSutAsync();
        using var process = StartBlockingProcess();
        vm.TrackPlaySession(process, "Demo", Guid.Empty, DateTime.UtcNow);
        _globalViewModel.IsGameRunning.ShouldBeTrue();

        process.Kill(entireProcessTree: true);

        await WaitUntilAsync(() => !_globalViewModel.IsGameRunning);
        _globalViewModel.ActivePlaySessions.ShouldBe(0);
    }

    [Fact]
    public async Task TrackPlaySession_WhenTheProcessHasAlreadyExited_DoesNotLeaveTheSessionOpen()
    {
        var vm = await CreateSutAsync();
        using var process = StartBlockingProcess();
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();

        vm.TrackPlaySession(process, "Demo", Guid.Empty, DateTime.UtcNow);

        await WaitUntilAsync(() => !_globalViewModel.IsGameRunning);
        _globalViewModel.ActivePlaySessions.ShouldBe(0);
    }

    private static Process StartBlockingProcess() => Process.Start(new ProcessStartInfo("cmd.exe", "/c pause")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardInput = true,
        RedirectStandardOutput = true
    })!;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(25);

        condition().ShouldBeTrue("Timed out waiting for the play session to close.");
    }

    // -------------------- Running-game detection --------------------

    /// <summary>
    /// Builds an installed game backed by a real folder on disk, optionally with the read-only
    /// subdirectory that destroyed the reported installation.
    /// </summary>
    private async Task<(GamesViewModel Vm, string GameDir)> CreateSutWithInstalledGameAsync(TempDirectoryFixture temp, bool readOnlyDirectory = false)
    {
        var gameDir = temp.Combine("Demo");
        Directory.CreateDirectory(Path.Combine(gameDir, "Data"));
        File.WriteAllText(Path.Combine(gameDir, "Game.exe"), "binary");
        File.WriteAllText(Path.Combine(gameDir, "Data", "save.dat"), "data");

        if (readOnlyDirectory)
        {
            var blocked = Path.Combine(gameDir, "Animations", "Beat_Up_hit_2");
            Directory.CreateDirectory(blocked);
            File.WriteAllText(Path.Combine(blocked, "frame.png"), "pixels");
            File.SetAttributes(blocked, File.GetAttributes(blocked) | FileAttributes.ReadOnly);
        }

        _contentService.GetGameDirectory("Demo").Returns(gameDir);
        _contentService.GetLocalGamesAsync().Returns([TestData.LocalGame(name: "Demo", version: "1.0.0", id: Guid.NewGuid())]);

        return (await CreateSutAsync(), gameDir);
    }

    [Fact]
    public async Task GetRunningSignal_WhenTheExecutableIsHeldOpen_ReportsTheWeakerSignal()
    {
        // Arrange — the game may have been started outside this launcher session, so there is no
        // tracked process and the lock on the executable is the only evidence left. It is reported
        // apart from TrackedProcess because Explorer and antivirus software hold it too.
        using var temp = new TempDirectoryFixture("uninstall-locked-exe");
        var (vm, gameDir) = await CreateSutWithInstalledGameAsync(temp);
        vm.GetRunningSignal("Demo").ShouldBe(GameRunningSignal.NotRunning);

        using var handle = new FileStream(Path.Combine(gameDir, "Game.exe"), FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act & Assert
        vm.GetRunningSignal("Demo").ShouldBe(GameRunningSignal.ExecutableLocked);
    }
}

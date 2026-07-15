using LostieLauncher.Content;
using LostieLauncher.Models;
using LostieLauncher.Services;
using LostieLauncher.ViewModels;
using System.Security.Cryptography;

namespace LostieLauncher.Tests.ViewModels;

[Collection(WpfCollection.Name)]
public class LibraryViewModelTests
{
    private readonly IContentService _contentService = Substitute.For<IContentService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDownloadService _downloadService = Substitute.For<IDownloadService>();
    private readonly DownloadOptions _downloadOptions = new(BaseUrl: "https://download.test");
    private readonly GlobalViewModel _globalViewModel = new();

    public LibraryViewModelTests(WpfApplicationFixture _)
    {
        // Default mocks: empty everything so initial LoadGamesAsync completes happily.
        _contentService.GetGamesAsync().Returns([]);
        _contentService.GetLocalGamesAsync().Returns([]);
        _contentService.GetAllPlaytimesAsync().Returns(new Dictionary<Guid, int>());
    }

    private LibraryViewModel CreateSut() => new(
        _contentService, _settingsService,
        _downloadService, _globalViewModel, _downloadOptions);

    [Fact]
    public async Task Constructor_TriggersInitialLoad_ResolvingLibraryLoadedTask()
    {
        // Arrange & Act
        var vm = CreateSut();
        await vm.LibraryLoadedTask;

        // Assert
        vm.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadGames_WhenRemoteHasGameAndLocalIsOlder_FlagsItAsUpdateAvailable()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Demo", version: "2.0.0", id: id)
        ]);
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Demo", version: "1.0.0", id: id)
        ]);
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert
        var game = vm.Games.Single();
        game.DownloadStatus.ShouldBe(GameDownloadStatus.UpdateAvailable);
    }

    [Fact]
    public async Task LoadGames_WhenLocalVersionMatchesRemote_FlagsItAsDownloaded()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Demo", version: "1.0.0", id: id)
        ]);
        _contentService.GetLocalGamesAsync().Returns([
            TestData.LocalGame(name: "Demo", version: "1.0.0", id: id)
        ]);
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert
        vm.Games.Single().DownloadStatus.ShouldBe(GameDownloadStatus.Downloaded);
    }

    [Fact]
    public async Task LoadGames_WhenGameNotInstalledLocally_LeavesStatusAsAvailable()
    {
        // Arrange
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Fresh", version: "1.0.0")
        ]);
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert — default status from the model is Available.
        vm.Games.Single().DownloadStatus.ShouldBe(GameDownloadStatus.Available);
    }

    [Fact]
    public async Task LoadGames_AppliesPlaytimesFromContentService()
    {
        // Arrange
        var id = Guid.NewGuid();
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Demo", id: id)
        ]);
        _contentService.GetAllPlaytimesAsync().Returns(new Dictionary<Guid, int> { [id] = 42 });
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert
        vm.Games.Single().PlaytimeMinutes.ShouldBe(42);
    }

    [Fact]
    public async Task IsEmpty_WhenServerReturnsNoGames_ReturnsTrueAfterLoad()
    {
        // Arrange — default mocks already return empty lists.
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert
        vm.IsEmpty.ShouldBeTrue();
        vm.IsListVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task IsListVisible_WhenServerReturnsGames_ReturnsTrueAndIsEmptyIsFalse()
    {
        // Arrange
        _contentService.GetGamesAsync().Returns([TestData.Game()]);
        var vm = CreateSut();

        // Act
        await vm.LibraryLoadedTask;

        // Assert
        vm.IsListVisible.ShouldBeTrue();
        vm.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_RequestsGamesAgain_FromContentService()
    {
        // Arrange
        var vm = CreateSut();
        await vm.LibraryLoadedTask;
        _contentService.ClearReceivedCalls();

        // Act
        await vm.RefreshAsync();

        // Assert
        await _contentService.Received(1).GetGamesAsync();
    }

    [Fact]
    public void StartDownloadCommand_WhenAlreadyDownloading_ReturnsImmediatelyAndDoesNothing()
    {
        // Arrange
        var vm = CreateSut();
        _globalViewModel.IsDownloading = true;
        var args = new GameDownloadArgs("any-game", "1.0.0", "/x");

        // Act — command should early-out without consulting the content service.
        vm.StartDownloadCommand.Execute(args);

        // Assert — IsServerActionBlockedAsync must NOT have been called.
        _contentService.DidNotReceive().IsServerActionBlockedAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PauseDownloadCommand_WhenNoActiveDownload_DoesNotThrow()
    {
        // Arrange
        var vm = CreateSut();

        // Act
        var act = () => vm.PauseDownloadCommand.Execute(null);

        // Assert
        Should.NotThrow(act);
    }


    [Fact]
    public async Task ResumingPausedGame_ContinuesItsOwnDownload_NotAnotherPausedGame()
    {
        // Arrange — two games that will both end up Paused. The download service records every
        // (url, destination) it is asked to fetch and reports "paused" so the games stay Paused.
        var calls = new List<(string Url, string Dest)>();
        _downloadService
            .DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgressInfo>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                calls.Add((ci.ArgAt<string>(0), ci.ArgAt<string>(1)));
                return Task.FromResult(DownloadResult.Cancelled());
            });
        _settingsService.GetGamesRootDirectory().Returns(Path.Combine(Path.GetTempPath(), "LostieLauncherTests-root"));
        _contentService.GetGameDirectory(Arg.Any<string>())
            .Returns(ci => Path.Combine(Path.GetTempPath(), "LostieLauncherTests-extract", ci.Arg<string>()!));
        _contentService.GetGamesAsync().Returns([
            TestData.Game(name: "Alpha", version: "1.0.0"),
            TestData.Game(name: "Bravo", version: "1.0.0"),
        ]);

        var vm = CreateSut();
        await vm.LibraryLoadedTask;

        var argsA = new GameDownloadArgs("alpha", "1.0.0", "/a/alpha.zip");
        var argsB = new GameDownloadArgs("bravo", "1.0.0", "/b/bravo.zip");

        // Act — start+pause A, then start+pause B (the update path shows no modal dialog), resume A.
        await vm.StartUpdateCommand.ExecuteAsync(argsA);
        await vm.StartUpdateCommand.ExecuteAsync(argsB);

        vm.Games.Single(g => g.GameId == "alpha").DownloadStatus.ShouldBe(GameDownloadStatus.Paused);
        vm.Games.Single(g => g.GameId == "bravo").DownloadStatus.ShouldBe(GameDownloadStatus.Paused);

        calls.Clear();
        await vm.StartDownloadCommand.ExecuteAsync(argsA);   // Resume A

        // Assert — the resume fetched A's own URL/destination, never B's (the BUG-003 regression
        // reused the last global args, which were B's).
        calls.ShouldHaveSingleItem();
        calls[0].Url.ShouldBe("https://download.test/a/alpha.zip");
        calls[0].Url.ShouldNotContain("bravo");
        calls[0].Dest.ShouldContain("alpha.");
    }

    [Fact]
    public async Task PauseDownloadCommand_PausesActiveDownload_FreesTheGlobalGuard_AndKeepsItResumable()
    {
        // Arrange — the first fetch blocks until its token is cancelled (a real pause); any later
        // fetch (the resume) returns immediately so the test does not hang.
        var fetches = 0;
        _downloadService
            .DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgressInfo>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                fetches++;
                if (fetches == 1)
                {
                    var ct = ci.ArgAt<CancellationToken>(3);
                    try { await Task.Delay(Timeout.Infinite, ct); }
                    catch (OperationCanceledException) { }
                }
                return DownloadResult.Cancelled();
            });
        _settingsService.GetGamesRootDirectory().Returns(Path.Combine(Path.GetTempPath(), "LostieLauncherTests-root"));
        _contentService.GetGameDirectory(Arg.Any<string>())
            .Returns(ci => Path.Combine(Path.GetTempPath(), "LostieLauncherTests-extract", ci.Arg<string>()!));
        _contentService.GetGamesAsync().Returns([TestData.Game(name: "Alpha", version: "1.0.0")]);

        var vm = CreateSut();
        await vm.LibraryLoadedTask;
        var args = new GameDownloadArgs("alpha", "1.0.0", "/a/alpha.zip");

        // Act — kick off the (blocking) download, then pause it by GameId.
        var downloadTask = vm.StartUpdateCommand.ExecuteAsync(args);
        var game = vm.Games.Single();
        game.DownloadStatus.ShouldBe(GameDownloadStatus.Downloading);   // sanity: in flight
        _globalViewModel.IsDownloading.ShouldBeTrue();

        vm.PauseDownloadCommand.Execute("alpha");
        await downloadTask;

        // Assert — paused, the single-download guard is released, and the session survives so a
        // subsequent resume actually launches another fetch.
        game.DownloadStatus.ShouldBe(GameDownloadStatus.Paused);
        _globalViewModel.IsDownloading.ShouldBeFalse();

        await vm.StartDownloadCommand.ExecuteAsync(args);   // Resume
        fetches.ShouldBe(2);
    }

    [Fact]
    public async Task DownloadFinishing_ReleasesTheGlobalGuard_SoFurtherDownloadsArePossible()
    {
        // Arrange — the fetch reports Success. The per-game session refactor must still release the
        // single-download guard on a terminal outcome (not only on pause): otherwise the first
        // completed download would leave IsDownloading stuck true and block every later download.
        var fetches = 0;
        _downloadService
            .DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgressInfo>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fetches++;
                return Task.FromResult(DownloadResult.Succeeded());
            });
        _settingsService.GetGamesRootDirectory().Returns(Path.Combine(Path.GetTempPath(), "LostieLauncherTests-root"));
        _contentService.GetGameDirectory(Arg.Any<string>())
            .Returns(ci => Path.Combine(Path.GetTempPath(), "LostieLauncherTests-extract", ci.Arg<string>()!));
        _contentService.GetGamesAsync().Returns([TestData.Game(name: "Alpha", version: "1.0.0")]);

        var vm = CreateSut();
        await vm.LibraryLoadedTask;
        var args = new GameDownloadArgs("alpha", "1.0.0", "/a/alpha.zip");

        // Act — the success handler runs (extraction fails harmlessly because no .zip exists on
        // disk, exercising the terminal RemoveSession path either way).
        await vm.StartUpdateCommand.ExecuteAsync(args);

        // Assert — guard released, and a second download is actually allowed through (not early-out).
        _globalViewModel.IsDownloading.ShouldBeFalse();
        await vm.StartUpdateCommand.ExecuteAsync(args);
        fetches.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteDownload_WhenSomethingThrowsMidFlight_StillReleasesTheGlobalGuard()
    {
        // Arrange — the download service throws between IsDownloading=true and its reset (the BUG-010
        // vector: any throw in the body — binding setter, modal, cleanup — leaves the guard stuck true
        // without a try/finally). DownloadAsync is the only injectable seam in that body, so it stands
        // in for "anything in the body throws"; the realistic production sources (CustomMessageBox in
        // HandleDownloadFailed, binding setters) are modal/dispatcher-bound and not unit-testable.
        var attempts = 0;
        _downloadService
            .DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgressInfo>>(), Arg.Any<CancellationToken>())
            .Returns<Task<DownloadResult>>(_ => { attempts++; throw new IOException("boom"); });
        _settingsService.GetGamesRootDirectory().Returns(Path.Combine(Path.GetTempPath(), "LostieLauncherTests-root"));
        _contentService.GetGameDirectory(Arg.Any<string>())
            .Returns(ci => Path.Combine(Path.GetTempPath(), "LostieLauncherTests-extract", ci.Arg<string>()!));
        _contentService.GetGamesAsync().Returns([TestData.Game(name: "Alpha", version: "1.0.0")]);

        var vm = CreateSut();
        await vm.LibraryLoadedTask;
        var args = new GameDownloadArgs("alpha", "1.0.0", "/a/alpha.zip");

        // Act — the throw propagates (BUG-014 silences it in production); the guard must still clear.
        await Should.ThrowAsync<IOException>(() => vm.StartUpdateCommand.ExecuteAsync(args));

        // Assert — guard released despite the exception...
        _globalViewModel.IsDownloading.ShouldBeFalse();

        // ...and a second download is genuinely allowed through (the body ran again, not an early-out),
        // proving the guard did not stay stuck true.
        await Should.ThrowAsync<IOException>(() => vm.StartUpdateCommand.ExecuteAsync(args));
        attempts.ShouldBe(2);
    }

    [Fact]
    public void AtomicSwapDirectories_FreshInstall_MovesSourceToTargetAndCleansUp()
    {
        // Arrange — no existing target directory (fresh install)
        using var root = new TempDirectoryFixture("atomicswap-fresh");
        var source = root.Combine("source");
        var backup = root.Combine("backup");
        var target = root.Combine("target");

        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "game.exe"), "v1.0");

        // Act
        LibraryViewModel.AtomicSwapDirectories(source, backup, target);

        // Assert — source is gone, target exists with the file, backup does not exist
        Directory.Exists(source).ShouldBeFalse();
        Directory.Exists(target).ShouldBeTrue();
        File.Exists(Path.Combine(target, "game.exe")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(target, "game.exe")).ShouldBe("v1.0");
        Directory.Exists(backup).ShouldBeFalse();
    }

    [Fact]
    public void AtomicSwapDirectories_Update_SwapsTargetToBackupSourceToTargetAndDeletesBackup()
    {
        // Arrange — existing target directory (update scenario)
        using var root = new TempDirectoryFixture("atomicswap-update");
        var source = root.Combine("source");
        var backup = root.Combine("backup");
        var target = root.Combine("target");

        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "game.exe"), "v2.0");
        File.WriteAllText(Path.Combine(source, "new.dll"), "new");

        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "game.exe"), "v1.0");
        File.WriteAllText(Path.Combine(target, "old.dll"), "orphan");

        // Act
        LibraryViewModel.AtomicSwapDirectories(source, backup, target);

        // Assert — source gone, target has only v2.0 files (BUG-029: no orphan old.dll), backup gone
        Directory.Exists(source).ShouldBeFalse();
        Directory.Exists(target).ShouldBeTrue();
        File.Exists(Path.Combine(target, "game.exe")).ShouldBeTrue();
        File.ReadAllText(Path.Combine(target, "game.exe")).ShouldBe("v2.0");
        File.Exists(Path.Combine(target, "new.dll")).ShouldBeTrue();
        File.Exists(Path.Combine(target, "old.dll")).ShouldBeFalse();
        Directory.Exists(backup).ShouldBeFalse();
    }

    [Fact]
    public void AtomicSwapDirectories_LeftoverBackup_IsCleanedBeforeSwap()
    {
        // Arrange — a leftover .old directory from a previous crashed swap
        using var root = new TempDirectoryFixture("atomicswap-leftover");
        var source = root.Combine("source");
        var backup = root.Combine("backup");
        var target = root.Combine("target");

        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "game.exe"), "fresh");

        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "game.exe"), "old");

        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(backup, "stale.txt"), "leftover-from-crash");

        // Act
        LibraryViewModel.AtomicSwapDirectories(source, backup, target);

        // Assert — stale backup was cleaned, swap succeeded normally
        Directory.Exists(source).ShouldBeFalse();
        Directory.Exists(target).ShouldBeTrue();
        File.ReadAllText(Path.Combine(target, "game.exe")).ShouldBe("fresh");
        Directory.Exists(backup).ShouldBeFalse();
    }

    // ---- BUG-025: integrity verification is mandatory and fail-closed ----------------------

    [Fact]
    public async Task VerifyIntegrity_WhenFileMatchesExpectedHash_ReturnsTrue()
    {
        // Arrange
        using var root = new TempDirectoryFixture("verify-match");
        var zip = root.Combine("game.zip");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(zip, bytes);
        var expected = Convert.ToHexString(SHA256.HashData(bytes));

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), zip, expected);

        // Assert
        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyIntegrity_IsCaseInsensitive_AcceptsLowercaseHash()
    {
        // Arrange — catalog hashes may be lowercase; comparison must be case-insensitive.
        using var root = new TempDirectoryFixture("verify-case");
        var zip = root.Combine("game.zip");
        var bytes = new byte[] { 9, 8, 7 };
        await File.WriteAllBytesAsync(zip, bytes);
        var expected = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), zip, expected);

        // Assert
        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashDiffers_ReturnsFalse()
    {
        // Arrange — well-formed hash that is not the file's actual digest (corruption / tampering).
        using var root = new TempDirectoryFixture("verify-mismatch");
        var zip = root.Combine("game.zip");
        await File.WriteAllBytesAsync(zip, new byte[] { 1, 2, 3 });
        var wrong = new string('a', 64);

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), zip, wrong);

        // Assert
        ok.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashIsEmpty_FailsClosed_WithoutInstalling()
    {
        // Arrange — BUG-025: an absent hash must NOT skip verification; it fails closed instead of
        // installing unchecked. The .zip does not even exist, proving the file is never opened when
        // there is nothing to verify against.
        using var root = new TempDirectoryFixture("verify-nohash");
        var missingZip = root.Combine("does-not-exist.zip");

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), missingZip, string.Empty);

        // Assert
        ok.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashIsNull_FailsClosed_WithoutInstalling()
    {
        // Arrange — System.Text.Json binds an explicit "sha256": null over the string.Empty default,
        // so a null reaches the gate despite the non-nullable declaration. It must fail closed (and not
        // throw), exactly like the empty-hash case, instead of degrading to a silent crash.
        using var root = new TempDirectoryFixture("verify-nullhash");
        var missingZip = root.Combine("does-not-exist.zip");

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), missingZip, null);

        // Assert
        ok.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashIsMalformed_FailsClosed()
    {
        // Arrange — a non-empty but not-64-hex string is not a usable validator.
        using var root = new TempDirectoryFixture("verify-malformed");
        var zip = root.Combine("game.zip");
        await File.WriteAllBytesAsync(zip, new byte[] { 1 });

        // Act
        var ok = await LibraryViewModel.VerifyIntegrityAsync(TestData.Game(), zip, "not-a-sha256");

        // Assert
        ok.ShouldBeFalse();
    }

    [Fact]
    public void IsValidSpecialVersionConfig_WithValidArchivoHashAndVersion_ReturnsTrue()
    {
        // Arrange
        var config = new SpecialVersionConfig
        {
            Archivo = "game.zip",
            Sha256 = new string('A', 64),
            Version = "1.0.0",
        };

        // Act & Assert
        LibraryViewModel.IsValidSpecialVersionConfig(config).ShouldBeTrue();
    }

    [Fact]
    public void IsValidSpecialVersionConfig_WithEmptySha256_ReturnsFalse()
    {
        // Arrange — BUG-025: special versions may no longer ship without a hash; an empty Sha256 is
        // rejected before the (potentially multi-GB) download starts.
        var config = new SpecialVersionConfig
        {
            Archivo = "game.zip",
            Sha256 = string.Empty,
            Version = "1.0.0",
        };

        // Act & Assert
        LibraryViewModel.IsValidSpecialVersionConfig(config).ShouldBeFalse();
    }

    [Fact]
    public void IsValidSpecialVersionConfig_WithMalformedSha256_ReturnsFalse()
    {
        // Arrange — a present but not-64-hex hash is not a usable validator.
        var config = new SpecialVersionConfig
        {
            Archivo = "game.zip",
            Sha256 = "abc",
            Version = "1.0.0",
        };

        // Act & Assert
        LibraryViewModel.IsValidSpecialVersionConfig(config).ShouldBeFalse();
    }

    // -------------------- GetSpecialVersionConfigErrorMessage (BUG-026) --------------------

    [Fact]
    public void GetSpecialVersionConfigErrorMessage_WhenNotFound_ReturnsKeyNotFoundMessage()
    {
        // Arrange — a real 404: the key genuinely does not exist.
        var strings = new Eng();

        // Act
        var error = LibraryViewModel.GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome.NotFound, strings);

        // Assert
        error.ShouldNotBeNull();
        error!.Value.Title.ShouldBe(strings.DownloadKeyNotFoundTitle);
        error.Value.Message.ShouldBe(strings.DownloadKeyNotFoundMessage);
    }

    [Fact]
    public void GetSpecialVersionConfigErrorMessage_WhenNetworkError_ReturnsDownloadErrorNotKeyNotFound()
    {
        // Arrange — the exact regression of BUG-026: a network failure must not be reported as a bad key.
        var strings = new Eng();

        // Act
        var error = LibraryViewModel.GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome.NetworkError, strings);

        // Assert
        error.ShouldNotBeNull();
        error!.Value.Title.ShouldBe(strings.DownloadErrorTitle);
        error.Value.Message.ShouldBe(strings.DownloadErrorMessage);
        error.Value.Title.ShouldNotBe(strings.DownloadKeyNotFoundTitle);
    }

    [Fact]
    public void GetSpecialVersionConfigErrorMessage_WhenInvalidResponse_ReturnsDownloadError()
    {
        // Arrange — server answered with garbage; transient/server problem, not a wrong key.
        var strings = new Eng();

        // Act
        var error = LibraryViewModel.GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome.InvalidResponse, strings);

        // Assert
        error.ShouldNotBeNull();
        error!.Value.Title.ShouldBe(strings.DownloadErrorTitle);
        error.Value.Message.ShouldBe(strings.DownloadErrorMessage);
    }

    [Fact]
    public void GetSpecialVersionConfigErrorMessage_WhenCancelled_ReturnsNull()
    {
        // Arrange — the user deliberately cancelled; no dialog should appear.
        var strings = new Eng();

        // Act & Assert
        LibraryViewModel.GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome.Cancelled, strings).ShouldBeNull();
    }

    [Fact]
    public void GetSpecialVersionConfigErrorMessage_WhenSuccess_ReturnsNull()
    {
        // Arrange — there is no error to show on success.
        var strings = new Eng();

        // Act & Assert
        LibraryViewModel.GetSpecialVersionConfigErrorMessage(SpecialVersionConfigOutcome.Success, strings).ShouldBeNull();
    }
}

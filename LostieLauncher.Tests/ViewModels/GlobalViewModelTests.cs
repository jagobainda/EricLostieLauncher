using LostieLauncher.ViewModels;

namespace LostieLauncher.Tests.ViewModels;

public class GlobalViewModelTests
{
    [Fact]
    public void IsBusy_DefaultsToFalse_WhenNothingIsRunning()
    {
        // Arrange
        var vm = new GlobalViewModel();

        // Act
        var busy = vm.IsBusy;

        // Assert
        busy.ShouldBeFalse();
    }

    [Fact]
    public void IsBusy_WhenIsDownloadingTrue_BecomesTrue()
    {
        // Arrange
        var vm = new GlobalViewModel();

        // Act
        vm.IsDownloading = true;

        // Assert
        vm.IsBusy.ShouldBeTrue();
    }

    [Fact]
    public void IsBusy_WhenIsRefreshingTrue_BecomesTrue()
    {
        // Arrange
        var vm = new GlobalViewModel();

        // Act
        vm.IsRefreshing = true;

        // Assert
        vm.IsBusy.ShouldBeTrue();
    }

    [Fact]
    public void IsDownloadingChange_RaisesPropertyChangedForIsBusy()
    {
        // Arrange
        var vm = new GlobalViewModel();
        using var recorder = new PropertyChangedRecorder(vm);

        // Act
        vm.IsDownloading = true;

        // Assert
        recorder.WasRaised(nameof(GlobalViewModel.IsBusy)).ShouldBeTrue();
    }

    [Fact]
    public void IsRefreshingChange_RaisesPropertyChangedForIsBusy()
    {
        // Arrange
        var vm = new GlobalViewModel();
        using var recorder = new PropertyChangedRecorder(vm);

        // Act
        vm.IsRefreshing = true;

        // Assert
        recorder.WasRaised(nameof(GlobalViewModel.IsBusy)).ShouldBeTrue();
    }

    [Fact]
    public void IsGameRunning_DefaultsToFalse_WhenNoGameWasLaunched()
    {
        var vm = new GlobalViewModel();

        vm.ActivePlaySessions.ShouldBe(0);
        vm.IsGameRunning.ShouldBeFalse();
    }

    [Fact]
    public void BeginPlaySession_MarksAGameAsRunning()
    {
        var vm = new GlobalViewModel();

        vm.BeginPlaySession();

        vm.ActivePlaySessions.ShouldBe(1);
        vm.IsGameRunning.ShouldBeTrue();
    }

    [Fact]
    public void EndPlaySession_AfterTheLastGameExits_ClearsIsGameRunning()
    {
        var vm = new GlobalViewModel();
        vm.BeginPlaySession();

        vm.EndPlaySession();

        vm.ActivePlaySessions.ShouldBe(0);
        vm.IsGameRunning.ShouldBeFalse();
    }

    [Fact]
    public void EndPlaySession_WithSeveralGamesOpen_KeepsIsGameRunningUntilTheLastOne()
    {
        var vm = new GlobalViewModel();
        vm.BeginPlaySession();
        vm.BeginPlaySession();

        vm.EndPlaySession();

        vm.ActivePlaySessions.ShouldBe(1);
        vm.IsGameRunning.ShouldBeTrue();
    }

    [Fact]
    public void PlaySessionChange_RaisesPropertyChangedForIsGameRunning()
    {
        var vm = new GlobalViewModel();
        using var recorder = new PropertyChangedRecorder(vm);

        vm.BeginPlaySession();

        recorder.WasRaised(nameof(GlobalViewModel.IsGameRunning)).ShouldBeTrue();
    }
}

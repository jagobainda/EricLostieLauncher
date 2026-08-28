using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class ShutdownWarningPolicyTests
{
    [Fact]
    public void Decide_WhenIdle_WarnsAboutNothing()
    {
        var warning = ShutdownWarningPolicy.Decide(isDownloading: false, isGameRunning: false);

        warning.ShouldBe(ShutdownWarning.None);
    }

    [Fact]
    public void Decide_WhenDownloading_WarnsAboutTheDownload()
    {
        var warning = ShutdownWarningPolicy.Decide(isDownloading: true, isGameRunning: false);

        warning.ShouldBe(ShutdownWarning.Download);
    }

    [Fact]
    public void Decide_WhenAGameIsRunning_WarnsAboutTheGame()
    {
        var warning = ShutdownWarningPolicy.Decide(isDownloading: false, isGameRunning: true);

        warning.ShouldBe(ShutdownWarning.Game);
    }

    [Fact]
    public void Decide_WhenDownloadingAndPlaying_WarnsAboutBoth()
    {
        var warning = ShutdownWarningPolicy.Decide(isDownloading: true, isGameRunning: true);

        warning.ShouldBe(ShutdownWarning.Both);
    }
}

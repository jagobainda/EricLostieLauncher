using LostieLauncher.Models;

namespace LostieLauncher.Tests.Models;

public class DownloadResultTests
{
    [Fact]
    public void Succeeded_BuildsResultWithSuccessOutcomeAndNoError()
    {
        // Arrange & Act
        var result = DownloadResult.Succeeded();

        // Assert
        result.Outcome.ShouldBe(DownloadOutcome.Success);
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Cancelled_BuildsResultWithCancelledOutcomeAndNoError()
    {
        // Arrange & Act
        var result = DownloadResult.Cancelled();

        // Assert
        result.Outcome.ShouldBe(DownloadOutcome.Cancelled);
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Failed_BuildsResultWithFailedOutcomeAndProvidedError()
    {
        // Arrange
        const string message = "boom";

        // Act
        var result = DownloadResult.Failed(message);

        // Assert
        result.Outcome.ShouldBe(DownloadOutcome.Failed);
        result.ErrorMessage.ShouldBe(message);
    }

    [Fact]
    public void PermissionDenied_BuildsResultWithPermissionDeniedOutcomeAndNoError()
    {
        // Arrange & Act
        var result = DownloadResult.PermissionDenied();

        // Assert — a permissions failure is distinct from a generic failure so the UI can
        // point the user at the download-path setting instead of showing the generic error.
        result.Outcome.ShouldBe(DownloadOutcome.PermissionDenied);
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void DownloadProgressInfo_WhenConstructedWithoutOptionalFields_AppliesDocumentedDefaults()
    {
        // Arrange & Act
        var info = new DownloadProgressInfo(50, 1000);

        // Assert
        info.Percent.ShouldBe(50);
        info.BytesPerSecond.ShouldBe(1000);
        info.TotalBytes.ShouldBe(-1);
        info.DownloadedBytes.ShouldBe(0);
    }
}

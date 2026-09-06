using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class FileMoveDiagnosticsTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new("file-move-diagnostics");

    public void Dispose() => _temp.Dispose();

    private string CreatePartFile()
    {
        var path = _temp.Combine("game.zip.part");
        File.WriteAllText(path, "payload");
        return path;
    }

    private static Exception CaptureMoveFailure(string source, string destination)
    {
        var error = Record.Exception(() => File.Move(source, destination, overwrite: true));
        error.ShouldNotBeNull();
        return error;
    }

    [Fact]
    public void Describe_WhenTheDestinationIsADirectory_NamesBothPathsAndTheDirectory()
    {
        var part = CreatePartFile();
        var destination = _temp.Combine("game.zip");
        Directory.CreateDirectory(destination);

        var description = FileMoveDiagnostics.Describe(part, destination, CaptureMoveFailure(part, destination));

        description.ShouldContain(part);
        description.ShouldContain(destination);
        description.ShouldContain("destination='" + destination + "' [directory");
        description.ShouldContain("win32=5 (ERROR_ACCESS_DENIED)");
    }

    [Fact]
    public void Describe_WhenTheDestinationIsReadOnly_ReportsTheReadOnlyAttribute()
    {
        var part = CreatePartFile();
        var destination = _temp.Combine("game.zip");
        File.WriteAllText(destination, "previous");
        File.SetAttributes(destination, FileAttributes.ReadOnly);

        try
        {
            var description = FileMoveDiagnostics.Describe(part, destination, CaptureMoveFailure(part, destination));

            description.ShouldContain("destination='" + destination + "' [file");
            description.ShouldContain("ReadOnly");
        }
        finally
        {
            File.SetAttributes(destination, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Describe_WhenTheDestinationIsHeldOpen_ReportsTheLock()
    {
        var part = CreatePartFile();
        var destination = _temp.Combine("game.zip");
        File.WriteAllText(destination, "previous");
        using var handle = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);

        var description = FileMoveDiagnostics.Describe(part, destination, CaptureMoveFailure(part, destination));

        description.ShouldContain("lockedByAnotherProcess=True");
    }

    [Fact]
    public void Describe_WhenTheDestinationDoesNotExist_ReportsItAsMissing()
    {
        var part = CreatePartFile();
        var destination = _temp.Combine("game.zip");

        var description = FileMoveDiagnostics.Describe(part, destination, new UnauthorizedAccessException("Access to the path is denied."));

        description.ShouldContain("destination='" + destination + "' [missing]");
        description.ShouldContain("destinationDirectory='" + _temp.Path + "' [exists=True]");
    }

    [Fact]
    public void Describe_RecordsTheSizeOfThePartFile()
    {
        var part = CreatePartFile();
        var expectedSize = new FileInfo(part).Length;

        var description = FileMoveDiagnostics.Describe(part, _temp.Combine("game.zip"), null);

        description.ShouldContain("size=" + expectedSize);
        description.ShouldContain("win32=n/a");
        description.ShouldContain("error=n/a");
    }

    [Fact]
    public void Describe_WithNoPaths_DoesNotThrow()
    {
        var description = FileMoveDiagnostics.Describe(null, null, null);

        description.ShouldContain("source=<null>");
        description.ShouldContain("destination=<null>");
    }

    [Fact]
    public void TryGetWin32Error_WithAnExceptionThatIsNotAWin32Failure_ReturnsFalse()
    {
        FileMoveDiagnostics.TryGetWin32Error(new InvalidOperationException(), out _).ShouldBeFalse();
        FileMoveDiagnostics.TryGetWin32Error(null, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(5, "ERROR_ACCESS_DENIED")]
    [InlineData(32, "ERROR_SHARING_VIOLATION")]
    [InlineData(112, "ERROR_DISK_FULL")]
    [InlineData(4321, "unknown")]
    public void DescribeWin32Error_MapsTheCodesThatMatterForFinalization(int code, string expected) => FileMoveDiagnostics.DescribeWin32Error(code).ShouldBe(expected);
}

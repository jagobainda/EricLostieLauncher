using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class FileLockProbeTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new("file-lock-probe");

    public void Dispose() => _temp.Dispose();

    private string CreateFile(string name = "file.bin")
    {
        var path = _temp.Combine(name);
        File.WriteAllText(path, "content");
        return path;
    }

    [Fact]
    public void IsLockedByAnotherProcess_WhenTheFileIsHeldOpen_ReturnsTrue()
    {
        // Arrange
        var path = CreateFile();
        using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Act & Assert
        FileLockProbe.IsLockedByAnotherProcess(path).ShouldBeTrue();
    }

    [Fact]
    public void IsLockedByAnotherProcess_WhenNobodyHoldsTheFile_ReturnsFalse() => FileLockProbe.IsLockedByAnotherProcess(CreateFile()).ShouldBeFalse();

    [Fact]
    public void IsLockedByAnotherProcess_WhenTheFileIsReadOnly_ReturnsFalse()
    {
        // Arrange — a read-only attribute is a permission matter, not a lock. Reporting it as "in
        // use" would refuse an uninstall that is perfectly safe to run.
        var path = CreateFile();
        File.SetAttributes(path, FileAttributes.ReadOnly);

        try
        {
            // Act & Assert
            FileLockProbe.IsLockedByAnotherProcess(path).ShouldBeFalse();
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void IsLockedByAnotherProcess_WhenTheFileDoesNotExist_ReturnsFalse() => FileLockProbe.IsLockedByAnotherProcess(_temp.Combine("missing.bin")).ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsLockedByAnotherProcess_WithNoUsablePath_ReturnsFalse(string? path) => FileLockProbe.IsLockedByAnotherProcess(path).ShouldBeFalse();
}

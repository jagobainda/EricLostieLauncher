using LostieLauncher.Models;
using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class DirectoryRemoverTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new("directory-remover");

    public void Dispose() => _temp.Dispose();

    private static DirectoryDeletionResult DeleteOnce(string path) => DirectoryRemover.Delete(path, maxAttempts: 1, retryDelay: TimeSpan.Zero);

    /// <summary>
    /// Recreates the tree from the user log: a game root with an executable, data folders, and one
    /// read-only subdirectory under Animations.
    /// </summary>
    private string ArrangeGameFolder(bool readOnlyDirectory = false, bool readOnlyFile = false)
    {
        var root = _temp.Combine("Game");
        Directory.CreateDirectory(Path.Combine(root, "Audio"));
        Directory.CreateDirectory(Path.Combine(root, "Graphics"));
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        var blocked = Path.Combine(root, "Animations", "Beat_Up_hit_2");
        Directory.CreateDirectory(blocked);

        File.WriteAllText(Path.Combine(root, "Game.exe"), "binary");
        File.WriteAllText(Path.Combine(root, "Data", "save.dat"), "data");
        var animation = Path.Combine(blocked, "frame.png");
        File.WriteAllText(animation, "pixels");

        if (readOnlyFile) File.SetAttributes(animation, FileAttributes.ReadOnly);
        if (readOnlyDirectory) File.SetAttributes(blocked, File.GetAttributes(blocked) | FileAttributes.ReadOnly);

        return root;
    }

    [Fact]
    public void Delete_WithAReadOnlyDirectoryInTheTree_RemovesEverything()
    {
        // Arrange — the exact shape of the reported failure: Directory.Delete(recursive: true) wipes
        // every file and then throws IOException on the read-only directory, leaving the game
        // destroyed but still registered.
        var root = ArrangeGameFolder(readOnlyDirectory: true);

        // Act
        var result = DeleteOnce(root);

        // Assert
        result.Deleted.ShouldBeTrue();
        result.BlockingPath.ShouldBeNull();
        result.Error.ShouldBeNull();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WithReadOnlyFiles_RemovesEverything()
    {
        // Arrange
        var root = ArrangeGameFolder(readOnlyFile: true);

        // Act
        var result = DeleteOnce(root);

        // Assert
        result.Deleted.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WithAReadOnlyRootDirectory_RemovesEverything()
    {
        // Arrange — the read-only attribute on the topmost directory is the last one to be hit.
        var root = ArrangeGameFolder();
        File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.ReadOnly);

        // Act
        var result = DeleteOnce(root);

        // Assert
        result.Deleted.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WhenThePathDoesNotExist_ReportsSuccess()
    {
        // Arrange & Act
        var result = DeleteOnce(_temp.Combine("missing"));

        // Assert
        result.Deleted.ShouldBeTrue();
    }

    [Fact]
    public void Delete_WhenAFileIsHeldOpen_ReportsTheBlockingPath()
    {
        // Arrange — a genuine lock, the case the read-only handling cannot fix.
        var root = ArrangeGameFolder();
        var locked = Path.Combine(root, "Data", "save.dat");
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var result = DeleteOnce(root);

        // Assert — the caller can now name the offending path to the user instead of a generic
        // "some files may be in use".
        result.Deleted.ShouldBeFalse();
        result.BlockingPath.ShouldBe(locked);
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Delete_WhenTheBlockerPersists_RetriesUpToTheAttemptLimit()
    {
        // Arrange
        var root = ArrangeGameFolder();
        using var handle = new FileStream(Path.Combine(root, "Data", "save.dat"), FileMode.Open, FileAccess.Read, FileShare.None);

        // Act
        var result = DirectoryRemover.Delete(root, maxAttempts: 3, retryDelay: TimeSpan.Zero);

        // Assert — transient locks (an antivirus scanning the folder) get more than one chance.
        result.Deleted.ShouldBeFalse();
        result.Attempts.ShouldBe(3);
    }

    [Fact]
    public void Delete_WhenTheBlockerGoesAway_SucceedsOnALaterAttempt()
    {
        // Arrange
        var root = ArrangeGameFolder();
        var locked = Path.Combine(root, "Data", "save.dat");
        var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            // Release the lock well after the first attempt has failed. The first attempt only has
            // to walk a handful of entries, so the margin here is what keeps the test off the clock:
            // it would take a 300 ms first attempt to make this pass without ever retrying.
            using var releaser = new Timer(_ => handle.Dispose(), null, TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);

            // Act
            var result = DirectoryRemover.Delete(root, maxAttempts: 10, retryDelay: TimeSpan.FromMilliseconds(100));

            // Assert
            result.Deleted.ShouldBeTrue();
            result.Attempts.ShouldBeGreaterThan(1);
            Directory.Exists(root).ShouldBeFalse();
        }
        finally
        {
            handle.Dispose();
        }
    }

    [Fact]
    public void Delete_WithAJunctionInTheTree_UnlinksItWithoutDeletingTheTarget()
    {
        // Arrange — a junction pointing outside the game folder. Walking into it would delete the
        // user's real files somewhere else on disk.
        var outside = _temp.Combine("outside");
        Directory.CreateDirectory(outside);
        var preserved = Path.Combine(outside, "important.txt");
        File.WriteAllText(preserved, "keep me");

        var root = ArrangeGameFolder();
        var junction = Path.Combine(root, "link");
        Assert.SkipUnless(TryCreateJunction(junction, outside), "Junctions cannot be created in this environment.");

        // Act
        var result = DeleteOnce(root);

        // Assert
        result.Deleted.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
        File.Exists(preserved).ShouldBeTrue();
    }

    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null) return false;

            process.WaitForExit(10_000);
            return process.HasExited && process.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

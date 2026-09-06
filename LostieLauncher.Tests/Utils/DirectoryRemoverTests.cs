using LostieLauncher.Models;
using LostieLauncher.Utils;

namespace LostieLauncher.Tests.Utils;

public class DirectoryRemoverTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new("directory-remover");

    public void Dispose() => _temp.Dispose();

    private static DirectoryDeletionResult DeleteOnce(string path) => DirectoryRemover.Delete(path, maxAttempts: 1, retryDelay: TimeSpan.Zero);

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
        var root = ArrangeGameFolder(readOnlyDirectory: true);

        var result = DeleteOnce(root);

        result.Deleted.ShouldBeTrue();
        result.BlockingPath.ShouldBeNull();
        result.Error.ShouldBeNull();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WithReadOnlyFiles_RemovesEverything()
    {
        var root = ArrangeGameFolder(readOnlyFile: true);

        var result = DeleteOnce(root);

        result.Deleted.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WithAReadOnlyRootDirectory_RemovesEverything()
    {
        var root = ArrangeGameFolder();
        File.SetAttributes(root, File.GetAttributes(root) | FileAttributes.ReadOnly);

        var result = DeleteOnce(root);

        result.Deleted.ShouldBeTrue();
        Directory.Exists(root).ShouldBeFalse();
    }

    [Fact]
    public void Delete_WhenThePathDoesNotExist_ReportsSuccess()
    {
        var result = DeleteOnce(_temp.Combine("missing"));

        result.Deleted.ShouldBeTrue();
    }

    [Fact]
    public void Delete_WhenAFileIsHeldOpen_ReportsTheBlockingPath()
    {
        var root = ArrangeGameFolder();
        var locked = Path.Combine(root, "Data", "save.dat");
        using var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = DeleteOnce(root);

        result.Deleted.ShouldBeFalse();
        result.BlockingPath.ShouldBe(locked);
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public void Delete_WhenTheBlockerPersists_RetriesUpToTheAttemptLimit()
    {
        var root = ArrangeGameFolder();
        using var handle = new FileStream(Path.Combine(root, "Data", "save.dat"), FileMode.Open, FileAccess.Read, FileShare.None);

        var result = DirectoryRemover.Delete(root, maxAttempts: 3, retryDelay: TimeSpan.Zero);

        result.Deleted.ShouldBeFalse();
        result.Attempts.ShouldBe(3);
    }

    [Fact]
    public void Delete_WhenTheBlockerGoesAway_SucceedsOnALaterAttempt()
    {
        var root = ArrangeGameFolder();
        var locked = Path.Combine(root, "Data", "save.dat");
        var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        try
        {
            using var releaser = new Timer(_ => handle.Dispose(), null, TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);

            var result = DirectoryRemover.Delete(root, maxAttempts: 10, retryDelay: TimeSpan.FromMilliseconds(100));

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
        var outside = _temp.Combine("outside");
        Directory.CreateDirectory(outside);
        var preserved = Path.Combine(outside, "important.txt");
        File.WriteAllText(preserved, "keep me");

        var root = ArrangeGameFolder();
        var junction = Path.Combine(root, "link");
        Assert.SkipUnless(TryCreateJunction(junction, outside), "Junctions cannot be created in this environment.");

        var result = DeleteOnce(root);

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

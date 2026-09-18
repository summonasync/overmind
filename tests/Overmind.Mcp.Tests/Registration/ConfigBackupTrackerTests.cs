using System.Globalization;
using Overmind.Mcp.Registration;
using Shouldly;
using Xunit;

namespace Overmind.Mcp.Tests.Registration;

public class ConfigBackupTrackerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"overmind-backup-{Guid.NewGuid():N}");

    public ConfigBackupTrackerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    private string TargetPath => Path.Combine(_dir, "config.toml");

    private string[] BackupFiles()
    {
        return Directory.GetFiles(_dir, "*.bak");
    }

    [Fact]
    public void EnsureBackup_ExistingFile_CreatesTimestampedCopy()
    {
        File.WriteAllText(TargetPath, "model = \"gpt-5-codex\"\n");

        var backupPath = new ConfigBackupTracker().EnsureBackup(TargetPath, CancellationToken.None);

        backupPath.ShouldNotBeNull();
        var fileName = Path.GetFileName(backupPath!);
        var stamp = fileName["config.toml.".Length..^".bak".Length];
        DateTime.TryParseExact(stamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _).ShouldBeTrue();
        File.ReadAllText(backupPath).ShouldBe(File.ReadAllText(TargetPath));
    }

    [Fact]
    public void EnsureBackup_SameFileTwiceInOneRun_SecondCallReturnsNull()
    {
        File.WriteAllText(TargetPath, "model = \"gpt-5-codex\"\n");
        var tracker = new ConfigBackupTracker();

        var first = tracker.EnsureBackup(TargetPath, CancellationToken.None);
        var second = tracker.EnsureBackup(TargetPath, CancellationToken.None);

        first.ShouldNotBeNull();
        second.ShouldBeNull();
        BackupFiles().Length.ShouldBe(1);
    }

    [Fact]
    public void EnsureBackup_MissingFile_ReturnsNullAndCreatesNothing()
    {
        var backupPath = new ConfigBackupTracker().EnsureBackup(TargetPath, CancellationToken.None);

        backupPath.ShouldBeNull();
        BackupFiles().Length.ShouldBe(0);
    }

    [Fact]
    public void EnsureBackup_NewRunAfterMutation_CreatesFreshBackup()
    {
        const string original = "model = \"gpt-5-codex\"\n";
        File.WriteAllText(TargetPath, original);
        var firstBackup = new ConfigBackupTracker().EnsureBackup(TargetPath, CancellationToken.None);

        const string mutated = "model = \"gpt-5-codex\"\nprofile = \"chat\"\n";
        File.WriteAllText(TargetPath, mutated);
        var secondBackup = new ConfigBackupTracker().EnsureBackup(TargetPath, CancellationToken.None);

        firstBackup.ShouldNotBeNull();
        secondBackup.ShouldNotBeNull();
        secondBackup.ShouldNotBe(firstBackup);
        BackupFiles().Length.ShouldBe(2);
        File.ReadAllText(firstBackup!).ShouldBe(original);
        File.ReadAllText(secondBackup!).ShouldBe(mutated);
    }
}

using System.Text.Json;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class WakeDropWriterTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wake-{Guid.NewGuid():N}");
    private readonly WakeDropWriter _writer;

    public WakeDropWriterTests()
    {
        _writer = new WakeDropWriter(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task Write_SerializesSpecShapeAndCompositeCursor()
    {
        var at = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var refId = Guid.NewGuid();
        var drop = new WakeDrop(
            "billy",
            at,
            $"{at:O}|{refId}",
            [new AgentWakeSignal("notification-reply", refId, null, "human", at, "approved")]);

        await _writer.WriteAsync(drop);

        var parsed = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(Path.Combine(_root, "billy.json")), JsonOptions);
        parsed.ShouldNotBeNull();
        parsed.Alias.ShouldBe("billy");
        parsed.UpdatedAt.ShouldBe(at);
        parsed.Cursor.ShouldBe($"{at:O}|{refId}");
        var signal = parsed.Signals.Single();
        signal.Kind.ShouldBe("notification-reply");
        signal.RefId.ShouldBe(refId);
        signal.RoomId.ShouldBeNull();
        signal.From.ShouldBe("human");
        signal.CreatedAt.ShouldBe(at);
        signal.Excerpt.ShouldBe("approved");
    }

    [Fact]
    public void For_CursorComesFromTheNewestSignal()
    {
        var olderAt = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);
        var newerAt = olderAt.AddHours(1);
        var page = new AgentWakePage(
        [
            new AgentWakeSignal("notification-reply", Guid.NewGuid(), null, "human", olderAt, "one"),
            new AgentWakeSignal("wave-mention", Guid.NewGuid(), "all", "dan", newerAt, "two")
        ], newerAt, Guid.NewGuid());

        var drop = WakeDrop.For("dan", newerAt, page);

        drop.Cursor.ShouldBe($"{newerAt:O}|{page.CursorRefId}");
    }

    [Fact]
    public async Task Write_ConcurrentWriters_NeverPublishATornFile()
    {
        var writes = Enumerable.Range(0, 24)
            .Select(i => _writer.WriteAsync(Drop(i)))
            .ToList();

        await Task.WhenAll(writes);

        var parsed = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(Path.Combine(_root, "billy.json")), JsonOptions);
        parsed.ShouldNotBeNull();
        parsed.Signals.Count.ShouldBe(1);
        Directory.EnumerateFiles(_root, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public async Task Write_OverExistingFile_ReplacesContent()
    {
        await _writer.WriteAsync(Drop(0));

        await _writer.WriteAsync(Drop(1));

        var parsed = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(Path.Combine(_root, "billy.json")), JsonOptions);
        parsed.ShouldNotBeNull();
        parsed.Signals.Single().Excerpt.ShouldBe("ping 1");
    }

    [Fact]
    public async Task Write_PinsOwnerOnlyPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await _writer.WriteAsync(Drop(0));

        File.GetUnixFileMode(Path.Combine(_root, "billy.json"))
            .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.GetUnixFileMode(_root)
            .ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public void Sweep_RemovesStaleFilesAndKeepsFreshOnes()
    {
        Directory.CreateDirectory(_root);
        var stale = Path.Combine(_root, "stale.json");
        var fresh = Path.Combine(_root, "fresh.json");
        File.WriteAllText(stale, "{}");
        File.WriteAllText(fresh, "{}");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow - WakeDropWriter.StaleAfter - TimeSpan.FromMinutes(1));
        File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow - TimeSpan.FromMinutes(1));

        var removed = _writer.SweepStale();

        removed.ShouldBe(1);
        File.Exists(stale).ShouldBeFalse();
        File.Exists(fresh).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_AfterRestart_UpdatesTheExistingFileInPlace()
    {
        await _writer.WriteAsync(Drop(0));
        var path = Path.Combine(_root, "billy.json");
        var before = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(path), JsonOptions);
        before.ShouldNotBeNull();

        var restarted = new WakeDropWriter(_root);
        File.Exists(path).ShouldBeTrue();
        var readable = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(path), JsonOptions);
        readable.ShouldNotBeNull();
        readable.UpdatedAt.ShouldBe(before.UpdatedAt);

        await restarted.WriteAsync(Drop(1));

        Directory.EnumerateFiles(_root, "billy.json").ShouldHaveSingleItem();
        var updated = JsonSerializer.Deserialize<WakeDrop>(
            await File.ReadAllTextAsync(path), JsonOptions);
        updated.ShouldNotBeNull();
        updated.Signals.Single().Excerpt.ShouldBe("ping 1");
        updated.Cursor.ShouldNotBe(before.Cursor);
    }

    private static WakeDrop Drop(int i)
    {
        var at = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc).AddMinutes(i);
        return new WakeDrop(
            "billy",
            at,
            $"{at:O}|{Guid.NewGuid()}",
            [new AgentWakeSignal("wave-mention", Guid.NewGuid(), "all", "dan", at, $"ping {i}")]);
    }
}

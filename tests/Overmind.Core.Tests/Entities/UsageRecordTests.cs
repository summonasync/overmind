using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public class UsageRecordTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    [Fact]
    public void Create_SetsFieldsAndTrimsIdentity()
    {
        var ts = DateTime.UtcNow.AddMinutes(-1);

        var record = UsageRecord.Create(
            ProjectId, "  Dan  ", SessionId, ts, " glm-5 ", 11, 22, 33, 44);

        record.Id.ShouldNotBe(Guid.Empty);
        record.ProjectId.ShouldBe(ProjectId);
        record.Alias.ShouldBe("Dan");
        record.SessionId.ShouldBe(SessionId);
        record.Ts.ShouldBe(ts);
        record.Model.ShouldBe("glm-5");
        record.InputTokens.ShouldBe(11L);
        record.OutputTokens.ShouldBe(22L);
        record.CacheReadTokens.ShouldBe(33L);
        record.CacheWriteTokens.ShouldBe(44L);
    }

    [Fact]
    public void Create_WithoutProjectAndCacheCounts_LeavesThemEmpty()
    {
        var record = UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, "glm-5", 1, 2);

        record.ProjectId.ShouldBeNull();
        record.CacheReadTokens.ShouldBe(0L);
        record.CacheWriteTokens.ShouldBe(0L);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyAlias_Throws(string? alias)
    {
        Should.Throw<ArgumentException>(
            () => UsageRecord.Create(null, alias!, SessionId, DateTime.UtcNow, "glm-5", 1, 2));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyModel_Throws(string? model)
    {
        Should.Throw<ArgumentException>(
            () => UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, model!, 1, 2));
    }

    [Fact]
    public void Create_NegativeTokenCounts_Throw()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, "glm-5", -1, 2));
        Should.Throw<ArgumentOutOfRangeException>(
            () => UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, "glm-5", 1, -2));
        Should.Throw<ArgumentOutOfRangeException>(
            () => UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, "glm-5", 1, 2, -3));
        Should.Throw<ArgumentOutOfRangeException>(
            () => UsageRecord.Create(null, "Dan", SessionId, DateTime.UtcNow, "glm-5", 1, 2, 3, -4));
    }
}

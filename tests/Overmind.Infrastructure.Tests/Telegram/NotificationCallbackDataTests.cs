using Overmind.Infrastructure.Telegram;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Telegram;

public sealed class NotificationCallbackDataTests
{
    private static readonly Guid NotificationId = new("1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111");

    [Theory]
    [InlineData("approve", NotificationDecision.Approve)]
    [InlineData("decline", NotificationDecision.Decline)]
    public void TryParse_ValidShape_ParsesNotificationIdAndDecision(string action, NotificationDecision expected)
    {
        var parsed = NotificationCallbackData.TryParse($"notif:{NotificationId:D}:{action}", out var data);

        parsed.ShouldBeTrue();
        data.NotificationId.ShouldBe(NotificationId);
        data.Decision.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notif")]
    [InlineData("notif:")]
    [InlineData("notif:1f0aa0e0:approve")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111")]
    [InlineData("notify:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:cancel")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:Approve")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:1")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve:extra")]
    [InlineData(" notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve")]
    [InlineData("notif: 1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve")]
    [InlineData("notif:1f0aa0e0-8ba6-4c0d-9d0f-2f8ea8b1c111:approve ")]
    public void TryParse_GarbageShapes_ReturnsFalse(string? data)
    {
        NotificationCallbackData.TryParse(data, out _).ShouldBeFalse();
    }
}

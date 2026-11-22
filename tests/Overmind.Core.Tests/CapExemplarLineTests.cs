using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

// 266/T3 — the pinned positive exemplar. The crew's own survey named this line as the model every
// softened error should reach: field, actual value, limit, and the delta to cut, all in one line and
// all true. It is a DO-NOT-SOFTEN pin, not a behaviour test: the doors around it changed in T1 (the
// band accepts now, the ceiling rejects), the wording of the rejection may not drift.
public class CapExemplarLineTests
{
    [Fact]
    public void WaveTextTooLong_IsTheExemplarByteForByte()
    {
        PlanningException.WaveTextTooLong(571, 500).Message
            .ShouldBe("text is 571 chars; cap is 500; cut 71");
    }

    [Theory]
    [InlineData(611, 610, "text is 611 chars; cap is 610; cut 1")]
    [InlineData(2_441, 2_000, "text is 2441 chars; cap is 2000; cut 441")]
    public void WaveTextTooLong_CarriesField_Actual_Limit_AndDeltaInLine(int length, int cap, string expected)
    {
        var ex = PlanningException.WaveTextTooLong(length, cap);

        ex.Message.ShouldBe(expected);
        ex.Code.ShouldBe(PlanningFailureCode.WaveTextTooLong);
        ex.Fix.ShouldBeNull();
    }
}

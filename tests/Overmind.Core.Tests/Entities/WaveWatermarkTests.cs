using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests.Entities;

public sealed class WaveWatermarkTests
{
    [Fact]
    public void Create_LowersAlias_AndCarriesStamp()
    {
        var at = DateTime.UtcNow;
        var watermark = WaveWatermark.Create(Guid.NewGuid(), " Dan ", at);

        watermark.Alias.ShouldBe("dan");
        watermark.LastReadAt.ShouldBe(at);
    }

    [Fact]
    public void Create_RejectsBlank_AndOverCapAlias()
    {
        Should.Throw<ArgumentException>(() => WaveWatermark.Create(Guid.NewGuid(), "  ", DateTime.UtcNow));
        Should.Throw<ArgumentException>(() =>
            WaveWatermark.Create(Guid.NewGuid(), new string('x', WaveWatermark.MaxIdentityLength + 1), DateTime.UtcNow));
    }

    [Fact]
    public void Advance_MovesForwardOnly()
    {
        var watermark = WaveWatermark.Create(Guid.NewGuid(), "dan", DateTime.UtcNow);
        var earlier = watermark.LastReadAt.AddMinutes(-5);
        var later = watermark.LastReadAt.AddMinutes(5);

        watermark.Advance(earlier);
        watermark.LastReadAt.ShouldNotBe(earlier);

        watermark.Advance(later);
        watermark.LastReadAt.ShouldBe(later);
    }
}

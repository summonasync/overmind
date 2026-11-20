using Overmind.Infrastructure.Kits;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class MarkerBlockWriterTests
{
    [Fact]
    public void KitToken_normalizes_non_alphanumerics()
    {
        MarkerBlockWriter.KitToken("sociality").ShouldBe("SOCIALITY");
        MarkerBlockWriter.KitToken("overmind-ceremony").ShouldBe("OVERMIND_CEREMONY");
    }

    [Fact]
    public void Upsert_into_empty_text_inserts_the_block()
    {
        var (text, changed, hash) = MarkerBlockWriter.Upsert("", "sociality", "# Tone rules.\n");

        changed.ShouldBeTrue();
        text.ShouldContain("===OVERMIND SOCIALITY KIT START===\n# Tone rules.\n===OVERMIND SOCIALITY KIT END===\n");
        hash.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Upsert_appends_after_existing_content_with_one_blank_line()
    {
        var (text, changed, _) = MarkerBlockWriter.Upsert("my own notes\n", "sociality", "tone\n");

        changed.ShouldBeTrue();
        text.ShouldBe("my own notes\n\n===OVERMIND SOCIALITY KIT START===\ntone\n===OVERMIND SOCIALITY KIT END===\n");
    }

    [Fact]
    public void Upsert_without_trailing_newline_adds_one_first()
    {
        var (text, _, _) = MarkerBlockWriter.Upsert("no newline", "sociality", "tone\n");

        text.ShouldStartWith("no newline\n\n===OVERMIND SOCIALITY");
    }

    [Fact]
    public void Upsert_replaces_only_the_kit_block()
    {
        var text = "header\n\n===OVERMIND SOCIALITY KIT START===\nold tone\n===OVERMIND SOCIALITY KIT END===\n";
        var (updated, changed, hash) = MarkerBlockWriter.Upsert(text, "sociality", "new tone\n");

        changed.ShouldBeTrue();
        updated.ShouldContain("new tone");
        updated.ShouldNotContain("old tone");
        updated.ShouldStartWith("header\n");
        hash.ShouldBe(MarkerBlockWriter.ContentSha256("===OVERMIND SOCIALITY KIT START===\nnew tone\n===OVERMIND SOCIALITY KIT END==="));
    }

    [Fact]
    public void Upsert_of_one_kit_leaves_another_kits_block_untouched()
    {
        var text = "===OVERMIND OTHER KIT START===\nother\n===OVERMIND OTHER KIT END===\n";
        var (updated, _, _) = MarkerBlockWriter.Upsert(text, "sociality", "social\n");

        updated.ShouldContain("===OVERMIND OTHER KIT START===\nother\n===OVERMIND OTHER KIT END===");
        updated.ShouldContain("===OVERMIND SOCIALITY KIT START===");
    }

    [Fact]
    public void Upsert_with_unchanged_content_reports_no_change()
    {
        var (text, changed, hash) = MarkerBlockWriter.Upsert("", "sociality", "tone\n");
        var (again, changedAgain, hashAgain) = MarkerBlockWriter.Upsert(text, "sociality", "tone\n");

        changed.ShouldBeTrue();
        changedAgain.ShouldBeFalse();
        again.ShouldBe(text);
        hashAgain.ShouldBe(hash);
    }

    [Fact]
    public void Upsert_refuses_a_start_marker_without_an_end()
    {
        var broken = "===OVERMIND SOCIALITY KIT START===\nno end here\n";

        Should.Throw<InvalidOperationException>(() => MarkerBlockWriter.Upsert(broken, "sociality", "tone\n"))
            .Message.ShouldContain("without");
    }

    [Fact]
    public void Cut_removes_the_block_and_following_blank_line()
    {
        var text = "keep me\n\n===OVERMIND SOCIALITY KIT START===\ntone\n===OVERMIND SOCIALITY KIT END===\n";
        var (cut, removed) = MarkerBlockWriter.Cut(text, "sociality");

        removed.ShouldBeTrue();
        cut.ShouldBe("keep me\n");
    }

    [Fact]
    public void Cut_without_a_block_is_a_noop()
    {
        var (cut, removed) = MarkerBlockWriter.Cut("plain notes\n", "sociality");

        removed.ShouldBeFalse();
        cut.ShouldBe("plain notes\n");
    }

    [Fact]
    public void Cut_leaves_another_kits_block_in_place()
    {
        var text = MarkerBlockWriter.Upsert(
            MarkerBlockWriter.Upsert("", "other", "other\n").Text, "sociality", "tone\n").Text;
        var (cut, removed) = MarkerBlockWriter.Cut(text, "sociality");

        removed.ShouldBeTrue();
        cut.ShouldBe(MarkerBlockWriter.Upsert("", "other", "other\n").Text);
    }
}

using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Core.Tests;

public class PlanningSectionsTests
{
    private const string Task037Body = """
<goal>
Mutation MCP tools stop echoing full artifact/entry bodies: adapter-level compact projections (metadata + bodyChars, log tail for set_status) cut thousands of wasted tokens per ceremony session.
</goal>

<context>
Confirmed burners this session: planning_upsert_artifact and planning_set_status return the full body + log the caller just wrote (audits of tonight's traffic: ~30 mutations x multi-KB). Spec artifact d74d5d4a has the decisions. The adapter is src/Overmind.Mcp — tool handlers in OvermindPlanningTools.cs / OvermindTools.cs call the host API via OvermindApiClient and pass responses through; the projection mappers go next to the handlers (private, no shared abstraction yet).
</context>

<scope>
- Audit the mutation tools' current responses (run the adapter's own tests / read the passthrough code): planning_upsert_artifact, planning_set_status, planning_append_log, planning_set_feature_status, set_current, update_entry, write_entry. Record in the task log which echo bodies and which are already compact.
- For each body-echoing mutation tool: project the host response to a compact DTO — { id, kind/entity, status, version, order where applicable, frozenAt, updatedAt, bodyChars }; planning_set_status additionally includes the LAST log line (appended provenance) instead of the full log. Drop the body (and the full log arrays).
- Read tools (planning_read_feature, planning_list_features, entries list/search, etc.) untouched.
- Tests (Overmind.Mcp.Tests): for each slimmed tool — output does NOT contain the submitted body marker content; carries id/status/version/bodyChars; set_status carries the log tail line; one read-tool regression pin (unchanged full body). Adapt existing canned-JSON tests that assert full bodies in mutation outputs.
- Gates: dotnet build + full suite green (baseline 989).
</scope>

<anti-scope>
- No host/API changes, no web, no includeBody flag, no truncation modes.
- Read tools untouched.
</anti-scope>

<constraints>
- Repo conventions: no comments, English strings, no new packages.
- No git commits — report changed files.
</constraints>

<acceptance-criteria>
- A multi-KB upsert returns a small JSON (no body) with bodyChars matching the sent size.
- set_status confirms with the last log line.
- Read tools still return full bodies.
- Suite green.
</acceptance-criteria>

<refs>
- Spec artifact d74d5d4a.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Spec gated (user approve). Starting execution: dotnet-coder (sonnet) on BACKLOG/037-mcp-echo-slimming/tasks/01-task-01.md.
- 2026-09-20 — Executed by dotnet-coder. Slimmed (body echo dropped, compact projection): planning_upsert_artifact, planning_create_feature, write_entry, update_entry. planning_set_status changed from plain string to {id, status, lastLogLine}. Already compact, left alone: planning_append_log, planning_set_feature_status, planning_set_current, planning_validate. Obstacle: the host returns an EMPTY 200 on artifact /status and /log POSTs, so set_status cannot project version/bodyChars or a host-dated log tail from the response — the confirmation carries the accepted logNote verbatim instead; the agent door exposes no artifact-by-id read and the human door is off-posture for the adapter (agent-door-only rule, CONTEXT.md). Gates: build clean, suite 990 green (Core 108 / Infra 336 / Mcp 238 / Api 308).
- 2026-09-20 — Review gate passed: PASS, 0 BLOCKER, 1 WARNING + 2 SUGGESTIONS (audits/review-037-01.md). 990/990. Audit: 5 tools slimmed (upsert_artifact, create_feature — worst echoer, set_status, write/update_entry), 3 already compact, read tools untouched with regression pin. lastLogLine honesty verified end-to-end (PlanningLog.Append stores verbatim; 200 only after SaveChanges). WARNING-1 (overmind_notify echo) routed to microbatch.
</log>
""";

    private const string Task00206Body = """
# 002/06 — KitImporter: re-import (three-way update)

<goal>
`KitImporter.ReimportAsync(kitId)` re-scans the kit's `SourcePath` and updates the kit in place, returning a full report: added / updated (new `EntryVersion`) / kept-local / conflicted (refused) / removed. Conflicts never overwrite local work and keep the old `ImportedContentHash` so the conflict re-surfaces next time. Missing source path → typed failure.
</goal>

<context>
Imported kits remember their source (task 02). Re-import is the manual refresh story (git auto-update is out of scope). The diff is three-way per entry, keyed on `ImportedContentHash` (H = hash at last import): file hash == H ⇒ no upstream change → keep local edits (kept-local); entry unchanged since import (current content hash == H) and file changed ⇒ take file, snapshot new `EntryVersion` (updated); both changed ⇒ conflict — refuse that entry, keep local, keep old hash, report; new file ⇒ new entry (added); file gone since last import ⇒ entry removed from kit and deleted (kit-owned), reported (removed). Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Infrastructure/Kits/KitImporter.cs` — add `ReimportAsync(kitId)` + report model (per-entry outcome + counts)
- Re-scan via `KitScanner` (task 03); match file entries to kit entries by relative path within the candidate
- **Review carry-over from 002/04 (`audits/review-002-04.md`, IMPORTANT-2 — root cause is scanner-side):** `KitScanner` marks empty `gates/*.md`/`guards/*.md` files as handled without an `EmptyFile` issue, so the preview shows them as importable while the importer silently drops them. Fix in the scanner (one condition: empty gate/guard file → `EmptyFile` issue, not a gate/guard entry), then remove the importer's silent-skip workaround and add regression tests on both sides
- **Review carry-over from 002/05 (`audits/architect-review-2026-09-14-002-05.md`, NIT-2 test part):** pin the deliberate `DuplicateTarget` behavior with a test — a hand-mixed kit (agnostic + explicit `SkillFile` with the same `RelativePath` in one skill, constructible via `EntriesApi`) throws `DuplicateTarget` at materialization, no silent overwrite
- `EntryVersion` snapshot on update follows the existing `ContextEntry.UpdateContent` behavior — reuse it, don't hand-roll versioning
- `ImportedAtUtc` and touched `ImportedContentHash`es update; conflicted entries keep the old hash
- Re-import on a manual kit (no `SourcePath`) or a missing path → `KitImportException` with distinct kinds (no-source vs source-missing)
- `ImportedContentHash` on removed entries' rows go away with the entry; removals are reported
- Tests: `tests/Overmind.Infrastructure.Tests/` — the full conflict matrix (file-only changed, entry-only changed, both changed, unchanged, added, removed), hash bookkeeping on conflicts, typed failures, `EntryVersion` created on updates; PLUS two review carry-overs from 002/02 (`audits/architect-review-2026-09-14-002-02.md`, NIT-1/NIT-2): a Core.Tests case pinning that `MarkImported` on an already-imported kit re-stamps silently (load-bearing for re-import), and an Infrastructure roundtrip test pinning the INTEGER enum persistence convention for `KitSourceKind`
</scope>

<anti-scope>
- No materializer changes (task 05); re-import does NOT trigger materialization sync (that's an activate/drift concern, unchanged)
- No API endpoints (task 07), no web UI (task 08)
- No auto-merge or conflict-resolution UI hooks — refuse + report only
- No changes to `EntryVersion` semantics or the version-numbering convention (known deferred item)
</anti-scope>

<constraints>
- No new NuGet packages
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Infrastructure.Tests` green
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [ ] Full conflict matrix covered by tests with correct outcomes per cell
- [ ] Updated entries gain an `EntryVersion`; conflicted entries keep local content and old `ImportedContentHash` (conflict re-reports on next re-import)
- [ ] Removed files remove their kit-owned entries; added files create entries with hashes
- [ ] Re-import on manual kit and on missing path raise distinct typed failures
- [ ] `ImportedAtUtc` bumps on every re-import run
- [ ] Build + Infrastructure.Tests green
</acceptance-criteria>

<refs>
- @../spec.md — D8, R8–R10 (reviewer reads, not executor)
- @../plan.md — task 06 row
- tasks/02-origin-model.md — origin fields + `ImportedContentHash`
- tasks/03-kit-scanner.md — re-scan source
- tasks/04-kit-importer.md — same service, commit flow
</refs>

<obstacles>
- EF Core discovery quirk (cost a debug round): entities added to a nav collection of a tracked non-Added principal are assumed to already exist in the DB when the key is ValueGeneratedOnAdd (`SkillFile.Id`) — tracked as Modified → UPDATE against a missing row → DbUpdateConcurrencyException. Fix: `ResyncSkillFiles` adds each new `SkillFile` via `_db.SkillFiles.Add(file)` BEFORE attaching it to the entry nav. (`EntryVersion.Id`/`SkillKitEntry` composite key are `ValueGeneratedNever` and track as Added via nav — no special handling needed, pinned by tests.)
- Kit links are snapshotted once (`importedLinks`) before the diff passes: links created by adds during the run have a null `Entry` nav and must not be re-matched or re-removed by later passes.
- Gate/guard identity is title-based (the entry model stores no source path for rules). An upstream H1 rename surfaces as removed+added; duplicate titles within one kind match 1:1 in deterministic order (by EntryId / relative path).
- Removed entries: the ContextEntry row is deleted only when no OTHER kit links it (`KitsApi.AddEntry` allows cross-kit links; a manual kit referencing an imported entry must not lose it). Otherwise only this kit's link is removed. Pinned by `Reimport_RemovedEntryLinkedElsewhere_RemovesLinkKeepsEntryRow`.
- Empty/whitespace upstream skill is refused as Conflicted (local content + old hash kept, re-surfaces next run) instead of crashing the run on `UpdateContent`'s whitespace guard; a brand-new empty skill is likewise refused rather than added. Whitespace gates/guards can no longer reach the importer at all (scanner fix), the in-importer check is belt-and-braces.
- Skill match key on the kit side is reconstructed as `{skillsDir}/{title}` from `SkillFiles[].Platform` via `PlatformDirectoryMap` (agnostic ⇒ bare title), compared separator-normalized and case-insensitively — disambiguates same-name platform variants (`.claude/skills/deploy` vs `.gemini/skills/deploy`) that title matching would merge.
- `KitImporter` ctor gained `PlatformDirectoryMap` (skill-key reconstruction); importer is not in Host DI yet (task 07), only `KitImporterTests`/`KitReimportTests` construct it — both updated.
</obstacles>

<log>
- 2026-09-14 — done. `ReimportAsync` three-way diff (added/updated/kept-local/conflicted/removed + counts) with per-entry report (`KitReimportReport`/`KitReimportEntryResult`/`KitReimportOutcome`); conflicts keep local content + old `ImportedContentHash` and re-report; updates snapshot via `ContextEntry.UpdateContent` + resync `SkillFiles`; removals delete kit-owned entry rows (guarded when linked elsewhere); `MarkImported` re-stamp bumps `ImportedAtUtc`; `NoSource`/`SourceMissing` failure kinds. Scanner IMPORTANT-2 fix: empty/whitespace gates+guards → `EmptyFile` issue, no gate entry; importer silent-skip removed; regression tests both sides (scanner test new, `Commit_EmptyGateFile_SkipsThatGate` still green end-to-end). 002/05 NIT-2: `DuplicateTarget` pinning test. 002/02 NIT-1/NIT-2: `MarkImported` re-stamp Core test + `KitSourceKind` INTEGER roundtrip (`KitOriginPersistenceTests`, raw `SqlQuery` value + `typeof` = integer). 21 new tests (15 reimport matrix + scanner + materialization + 2 persistence + Core re-stamp); suite 462/462 green (Core 94 / Infra 174 / Api 135 / Mcp 59); scoped `dotnet format whitespace --verify-no-changes` clean. Files: `src/Overmind.Core/Abstractions/KitImport{FailureKind,Exception}.cs`, `src/Overmind.Core/Entities/SkillKitEntry.cs` (hash writer), `src/Overmind.Infrastructure/Kits/Kit{Importer,Scanner}.cs` + 3 new report models, `tests/Overmind.Infrastructure.Tests/KitReimportTests.cs` (new), `KitScannerTests.cs`, `KitImporterTests.cs`, `MaterializationTests.cs`, `KitOriginPersistenceTests.cs` (new), `tests/Overmind.Core.Tests/Entities/EntityFactoryTests.cs`.
</log>
""";

    [Fact]
    public void Find_SingleSection_ReturnsRawContent()
    {
        var body = "# Plan\n\n<goal>\nShip the parser.\n</goal>\n\n<scope>\n- one\n</scope>\n";

        var section = PlanningSections.Find("goal", body);

        section.ShouldNotBeNull();
        section!.Name.ShouldBe("goal");
        section.Content.ShouldBe("Ship the parser.");
    }

    [Fact]
    public void Find_MissingSection_ReturnsNull()
    {
        PlanningSections.Find("goal", "# Title\n\nno anchored tags").ShouldBeNull();
    }

    [Fact]
    public void TryFind_MissingSection_ReturnsFalseAndNull()
    {
        var found = PlanningSections.TryFind("refs", "<goal>\ncontent\n</goal>", out var section);

        found.ShouldBeFalse();
        section.ShouldBeNull();
    }

    [Fact]
    public void TryFind_ExistingSection_ReturnsTrueAndSection()
    {
        var found = PlanningSections.TryFind("goal", "<goal>\ncontent\n</goal>", out var section);

        found.ShouldBeTrue();
        section!.Content.ShouldBe("content");
    }

    [Fact]
    public void Find_DuplicateNames_ReturnsLastAnchoredPair()
    {
        var body = "<goal>\nfirst pair\n</goal>\n\nmiddle\n\n<goal>\nsecond pair\n</goal>\n";

        var section = PlanningSections.Find("goal", body);

        section!.Content.ShouldBe("second pair");
    }

    [Fact]
    public void Find_OpenTagWithoutClose_IsIgnored()
    {
        var body = "<goal>\nno close here\n\n<scope>\nreal\n</scope>\n";

        PlanningSections.Find("goal", body).ShouldBeNull();
        PlanningSections.Find("scope", body)!.Content.ShouldBe("real");
    }

    [Fact]
    public void Find_ContentWithBlankLines_PreservesThem()
    {
        var body = "<context>\nfirst paragraph\n\nsecond paragraph\n</context>\n";

        var section = PlanningSections.Find("context", body);

        section!.Content.ShouldBe("first paragraph\n\nsecond paragraph");
    }

    [Fact]
    public void Find_CrlfBody_ReturnsContentWithoutOuterNewlines()
    {
        var body = "# Plan\r\n\r\n<goal>\r\nfirst\r\n\r\nsecond\r\n</goal>\r\n";

        var section = PlanningSections.Find("goal", body);

        section!.Content.ShouldBe("first\r\n\r\nsecond");
    }

    [Fact]
    public void Find_IndentedTagLines_MatchesWithSpacesAndTabs()
    {
        var body = "intro\n\n  <goal>\n\tinner text\n\t</goal>\n\ntail";

        var section = PlanningSections.Find("goal", body);

        section.ShouldNotBeNull();
        section!.Content.ShouldBe("\tinner text");
    }

    [Fact]
    public void Find_ProseMentionInline_ReturnsNull()
    {
        var body = "# Plan\n\nUse `<goal>` tags inline, never on their own line.";

        PlanningSections.Find("goal", body).ShouldBeNull();
    }

    [Fact]
    public void Replace_SingleSection_SwapsContentKeepsTagsAndSurroundings()
    {
        var body = "before\n\n<goal>\nold text\n</goal>\n\nafter";

        var result = PlanningSections.Replace("goal", body, "new text");

        result.ShouldBe("before\n\n<goal>\nnew text\n</goal>\n\nafter");
    }

    [Fact]
    public void Replace_MissingSection_ThrowsInsteadOfCreating()
    {
        var body = "# Title\n\nno anchored tags";

        Should.Throw<InvalidOperationException>(() => PlanningSections.Replace("goal", body, "text"))
            .Message.ShouldContain("goal");
        body.ShouldNotContain("<goal>");
    }

    [Fact]
    public void Replace_DuplicateNames_ReplacesLastAnchoredPairOnly()
    {
        var body = "<goal>\nfirst pair\n</goal>\n\nmiddle\n\n<goal>\nsecond pair\n</goal>\n";

        var result = PlanningSections.Replace("goal", body, "patched");

        result.ShouldBe("<goal>\nfirst pair\n</goal>\n\nmiddle\n\n<goal>\npatched\n</goal>\n");
    }

    [Fact]
    public void Replace_RoundTripsFoundContent()
    {
        var body = "a\n\n<goal>\nline one\n\nline two\n</goal>\n\nb";

        var roundTripped = PlanningSections.Replace("goal", body, PlanningSections.Find("goal", body)!.Content);

        roundTripped.ShouldBe(body);
    }

    [Fact]
    public void Replace_EmptySection_RoundTripsWithoutBlankLine()
    {
        var body = "a\n<goal>\n</goal>\nb";

        var roundTripped = PlanningSections.Replace("goal", body, PlanningSections.Find("goal", body)!.Content);

        roundTripped.ShouldBe(body);
    }

    [Fact]
    public void Replace_CrlfBody_PreservesLineEndings()
    {
        var body = "# Plan\r\n\r\n<goal>\r\nfirst\r\n</goal>\r\n";

        var result = PlanningSections.Replace("goal", body, "next");

        result.ShouldBe("# Plan\r\n\r\n<goal>\r\nnext\r\n</goal>\r\n");
    }

    [Fact]
    public void Replace_IndentedTagLines_PreservesIndentation()
    {
        var body = "intro\n\n  <goal>\n\tinner text\n\t</goal>\n\ntail";

        var result = PlanningSections.Replace("goal", body, "replaced");

        result.ShouldBe("intro\n\n  <goal>\nreplaced\n\t</goal>\n\ntail");
    }

    [Fact]
    public void Remove_WholeSection_CollapsesIntoTheMarkerLine()
    {
        var body = "before\n\n<goal>\nold text\n</goal>\n\nafter";

        var result = PlanningSections.Remove("goal", body, "<removed 2026-11-10 — superseded>");

        result.ShouldBe("before\n\n<removed 2026-11-10 — superseded>\n\nafter");
    }

    [Fact]
    public void Remove_MissingSection_ThrowsInsteadOfCreating()
    {
        var body = "# Title\n\nno anchored tags";

        Should.Throw<InvalidOperationException>(() => PlanningSections.Remove("goal", body, "<removed — x>"))
            .Message.ShouldContain("goal");
    }

    [Fact]
    public void Remove_SectionAtBodyEnd_TrailingNewlinePreserved()
    {
        var body = "a\n<goal>\nx\n</goal>\n";

        var result = PlanningSections.Remove("goal", body, "<removed 2026-11-10 — gone>");

        result.ShouldBe("a\n<removed 2026-11-10 — gone>\n");
    }

    [Fact]
    public void Remove_CrlfBody_KeepsLineEndings()
    {
        var body = "# Plan\r\n\r\n<goal>\r\nfirst\r\n</goal>\r\n";

        var result = PlanningSections.Remove("goal", body, "<removed 2026-11-10 — rework>");

        result.ShouldBe("# Plan\r\n\r\n<removed 2026-11-10 — rework>\r\n");
    }

    [Fact]
    public void Remove_TombstonedSection_StopsResolvingWhileTraceStays()
    {
        var body = "intro\n\n<goal>\nold\n</goal>\n\ntail";

        var result = PlanningSections.Remove("goal", body, "<removed 2026-11-10 — dead>");

        PlanningSections.Find("goal", result).ShouldBeNull();
        result.ShouldContain("<removed 2026-11-10 — dead>");
    }

    [Fact]
    public void Remove_DuplicateNames_RemovesTheLastAnchoredPairOnly()
    {
        var body = "<goal>\nfirst pair\n</goal>\n\nmiddle\n\n<goal>\nsecond pair\n</goal>\n";

        var result = PlanningSections.Remove("goal", body, "<removed 2026-11-10 — dup>");

        result.ShouldBe("<goal>\nfirst pair\n</goal>\n\nmiddle\n\n<removed 2026-11-10 — dup>\n");
    }

    [Fact]
    public void List_ReturnsNamesInDocumentOrder()
    {
        var body = "<goal>\nx\n</goal>\n\n<context>\ny\n</context>\n\n<log>\n- a\n</log>\n";

        var names = PlanningSections.List(body);

        names.ShouldBe(new[] { "goal", "context", "log" });
    }

    [Fact]
    public void List_DuplicatePairs_ListsEachPair()
    {
        var body = "<goal>\na\n</goal>\nx\n<goal>\nb\n</goal>\n";

        var names = PlanningSections.List(body);

        names.ShouldBe(new[] { "goal", "goal" });
    }

    [Fact]
    public void List_NoAnchoredPairs_ReturnsEmpty()
    {
        PlanningSections.List("# Title\n\nUse `<goal>` inline only").ShouldBeEmpty();
    }

    [Fact]
    public void List_EmptyBody_ReturnsEmpty()
    {
        PlanningSections.List("").ShouldBeEmpty();
    }

    [Fact]
    public void List_UnclosedTag_IsNotListed()
    {
        var body = "<goal>\nno close\n\n<scope>\nreal\n</scope>\n";

        var names = PlanningSections.List(body);

        names.ShouldBe(new[] { "scope" });
    }

    [Fact]
    public void FilteredRead_AnchoredSections_ReturnInBodyOrderRegardlessOfRequestOrder()
    {
        var body = "# Task\n\n<goal>\nModel it.\n</goal>\n\n<obstacles>\nA hard one.\n</obstacles>\n";

        var read = PlanningSections.FilteredRead(body, ["obstacles", "goal"]);

        read.Body.ShouldBe("<goal>\nModel it.\n</goal>\n<obstacles>\nA hard one.\n</obstacles>");
        read.MissingNames.ShouldBe([]);
    }

    [Fact]
    public void FilteredRead_OneLineWrapper_IsFoundAndExpandedNotMissing()
    {
        var body = "# Task\n\n<goal>Model it.</goal>\n\n<obstacles>\nA hard one.\n</obstacles>\n";

        var read = PlanningSections.FilteredRead(body, ["goal"]);

        read.Body.ShouldBe("<goal>\nModel it.\n</goal>");
        read.MissingNames.ShouldBe([]);
    }

    [Fact]
    public void FilteredRead_AbsentName_IsMissing()
    {
        var read = PlanningSections.FilteredRead("<goal>\nModel it.\n</goal>\n", ["goal", "banana"]);

        read.Body.ShouldBe("<goal>\nModel it.\n</goal>");
        read.MissingNames.ShouldBe(["banana"]);
    }

    [Fact]
    public void FilteredRead_FencedExample_StaysMissing()
    {
        var body = "Prose:\n\n```xml\n<goal>Example only.</goal>\n```\n";

        var read = PlanningSections.FilteredRead(body, ["goal"]);

        read.Body.ShouldBe("");
        read.MissingNames.ShouldBe(["goal"]);
    }

    [Fact]
    public void FilteredRead_DuplicateRequest_RendersOnce()
    {
        var read = PlanningSections.FilteredRead("<goal>\nOne.\n</goal>\n", ["goal", "goal"]);

        read.Body.ShouldBe("<goal>\nOne.\n</goal>");
        read.MissingNames.ShouldBe([]);
    }

    [Fact]
    public void ParseAcceptanceCriteria_MixedMarkers_ExposesIndexStateAndRawText()
    {
        var body = "<acceptance-criteria>\n- [ ] first todo\n- [x] done one\n- plain bullet\nintro text\n- [X] upper x\n</acceptance-criteria>\n";

        var items = PlanningSections.ParseAcceptanceCriteria(body);

        items.Count.ShouldBe(4);
        items[0].ShouldBe(new PlanningAcItem(1, false, "[ ] first todo"));
        items[1].ShouldBe(new PlanningAcItem(2, true, "[x] done one"));
        items[2].ShouldBe(new PlanningAcItem(3, null, "plain bullet"));
        items[3].ShouldBe(new PlanningAcItem(4, true, "[X] upper x"));
    }

    [Fact]
    public void ParseAcceptanceCriteria_MissingSection_ReturnsEmpty()
    {
        PlanningSections.ParseAcceptanceCriteria("# Spec\n\nno AC section").ShouldBeEmpty();
    }

    [Fact]
    public void ParseAcceptanceCriteria_DuplicateSections_ReadsLastAnchoredPair()
    {
        var body = "<acceptance-criteria>\n- [ ] old\n</acceptance-criteria>\n\n<acceptance-criteria>\n- [x] current\n</acceptance-criteria>\n";

        var items = PlanningSections.ParseAcceptanceCriteria(body);

        items.Count.ShouldBe(1);
        items[0].Checked.ShouldBe(true);
        items[0].RawText.ShouldBe("[x] current");
    }

    [Fact]
    public void ParseAcceptanceCriteria_CrlfBody_ReadsItems()
    {
        var body = "<acceptance-criteria>\r\n- [ ] first\r\n- [x] second\r\n</acceptance-criteria>\r\n";

        var items = PlanningSections.ParseAcceptanceCriteria(body);

        items.Count.ShouldBe(2);
        items[0].RawText.ShouldBe("[ ] first");
        items[1].Checked.ShouldBe(true);
    }

    [Fact]
    public void List_RealTaskBody_037_ReturnsSectionNamesInOrder()
    {
        var names = PlanningSections.List(Task037Body);

        names.ShouldBe(new[]
        {
            "goal", "context", "scope", "anti-scope", "constraints",
            "acceptance-criteria", "refs", "obstacles", "log"
        });
    }

    [Fact]
    public void Find_RealTaskBody_037_LogCarriesEveryEntry()
    {
        var section = PlanningSections.Find("log", Task037Body);

        section.ShouldNotBeNull();
        section!.Content.ShouldContain("2026-09-20 — Executed by dotnet-coder");
        section.Content.ShouldContain("2026-09-20 — Review gate passed");

        var lines = PlanningLog.Parse(Task037Body);
        lines.Count.ShouldBe(3);
        lines.ShouldAllBe(l => l.Date == new DateOnly(2026, 9, 20));
        lines[2].Text.ShouldStartWith("Review gate passed");
    }

    [Fact]
    public void Replace_RealTaskBody_037_RoundTripsMultiLineSection()
    {
        var roundTripped = PlanningSections.Replace(
            "scope", Task037Body, PlanningSections.Find("scope", Task037Body)!.Content);

        roundTripped.ShouldBe(Task037Body);
    }

    [Fact]
    public void ParseAcceptanceCriteria_RealTaskBody_00206_ReadsCheckboxItems()
    {
        var items = PlanningSections.ParseAcceptanceCriteria(Task00206Body);

        items.Count.ShouldBe(6);
        items.ShouldAllBe(i => i.Checked == false);
        items[0].RawText.ShouldBe("[ ] Full conflict matrix covered by tests with correct outcomes per cell");
        items[3].RawText.ShouldBe("[ ] Re-import on manual kit and on missing path raise distinct typed failures");
    }

    [Fact]
    public void Replace_ContentCarryingWholeLineWrappers_StripsThemBeforeSplicing()
    {
        var body = "<obstacles>\n- (empty — fill during work)\n</obstacles>";

        var result = PlanningSections.Replace(
            "obstacles", body, "<obstacles>\n- patched bullet <round-1 B1>\n</obstacles>");

        result.ShouldBe("<obstacles>\n- patched bullet <round-1 B1>\n</obstacles>");
    }

    [Fact]
    public void Replace_ContentCarryingOnlyCloseTag_StripsItBeforeSplicing()
    {
        var body = "<goal>\nold\n</goal>";

        var result = PlanningSections.Replace("goal", body, "next\n</goal>");

        result.ShouldBe("<goal>\nnext\n</goal>");
    }

    [Fact]
    public void Replace_ContentCarryingOnlyOpenTag_RendersEmptySection()
    {
        var body = "<goal>\nold\n</goal>";

        var result = PlanningSections.Replace("goal", body, "<goal>");

        result.ShouldBe("<goal>\n</goal>");
    }

    [Fact]
    public void Replace_InlineTagMentionsInContent_ArePreserved()
    {
        var body = "<obstacles>\nold\n</obstacles>";

        var result = PlanningSections.Replace(
            "obstacles", body, "- note <round-1 B1, CLOSED> stays\n- tail mentions <obstacles> inline");

        result.ShouldBe("<obstacles>\n- note <round-1 B1, CLOSED> stays\n- tail mentions <obstacles> inline\n</obstacles>");
    }

    [Fact]
    public void Normalize_DuplicateOpeningTags_CollapsesToSingleWrapper()
    {
        var normalized = PlanningSections.Normalize("<obstacles>\n<obstacles>\n- a\n</obstacles>");

        normalized.ShouldBe("<obstacles>\n- a\n</obstacles>");
    }

    [Fact]
    public void Normalize_DuplicateClosingTags_CollapsesToSingleWrapper()
    {
        var normalized = PlanningSections.Normalize("<goal>\nx\n</goal>\n</goal>\n");

        normalized.ShouldBe("<goal>\nx\n</goal>\n");
    }

    [Fact]
    public void Normalize_TrappedBulletOutsideCanonicalPair_DoesNotSurvive()
    {
        var corrupted =
            "<obstacles>\n" +
            "<obstacles>\n" +
            "- kept bullet <round-1 B1>\n" +
            "</obstacles>\n" +
            "- evicted addendum bullet\n" +
            "</obstacles>\n\n" +
            "<log>\n- 2026-10-04 [agent] seeded\n</log>\n";

        var normalized = PlanningSections.Normalize(corrupted);

        normalized.ShouldBe(
            "<obstacles>\n" +
            "- kept bullet <round-1 B1>\n" +
            "</obstacles>\n\n" +
            "<log>\n- 2026-10-04 [agent] seeded\n</log>\n");
        PlanningSections.Find("obstacles", normalized)!.Content.ShouldContain("kept bullet");
        normalized.ShouldNotContain("evicted addendum bullet");
    }

    [Fact]
    public void Normalize_StrayCloseAfterForeignSection_LogSurvivesAndStrayLeaves()
    {
        var corrupted =
            "<obstacles>\n- only\n</obstacles>\n\n<log>\n- entry\n</log>\n</obstacles>\n";

        var normalized = PlanningSections.Normalize(corrupted);

        normalized.ShouldBe("<obstacles>\n- only\n</obstacles>\n\n<log>\n- entry\n</log>\n");
    }

    [Fact]
    public void Normalize_DuplicatePairs_KeepsCanonicalLastPairContent()
    {
        var normalized = PlanningSections.Normalize(
            "<goal>\nfirst\n</goal>\n\nmiddle prose\n\n<goal>\nsecond\n</goal>");

        normalized.ShouldBe("<goal>\nsecond\n</goal>");
        normalized.ShouldNotContain("first");
        normalized.ShouldNotContain("middle prose");
    }

    [Fact]
    public void Normalize_CleanBody_ReturnsIdenticalText()
    {
        var body = "# Task\n\n<goal>\nDo it.\n</goal>\n\n<obstacles>\n- a <tag> inline\n</obstacles>\n\n<log>\n- 2026-10-04 — note\n</log>\n";

        PlanningSections.Normalize(body).ShouldBe(body);
    }

    [Fact]
    public void Normalize_UnpairedTagLines_AreLeftAlone()
    {
        var body = "<goal>\nno close here\n\n<scope>\nreal\n</scope>\n";

        PlanningSections.Normalize(body).ShouldBe(body);
    }

    [Fact]
    public void Normalize_ProseMentionsInline_AreUntouched()
    {
        var body = "# Plan\n\nUse `<goal>` tags inline, never on their own line.";

        PlanningSections.Normalize(body).ShouldBe(body);
    }

    [Fact]
    public void Find_RealTaskBody_00206_MultiParagraphObstaclesStayIntact()
    {
        var section = PlanningSections.Find("obstacles", Task00206Body);

        section.ShouldNotBeNull();
        section!.Content.ShouldStartWith("- EF Core discovery quirk");
        section.Content.ShouldContain("`KitImporter` ctor gained `PlatformDirectoryMap`");

        var logLines = PlanningLog.Parse(Task00206Body);
        logLines.Count.ShouldBe(1);
        logLines[0].Date.ShouldBe(new DateOnly(2026, 9, 14));
        logLines[0].Text.ShouldStartWith("done. `ReimportAsync` three-way diff");
    }

    [Fact]
    public void Insert_AppendsSectionWithContent_AfterExistingBody()
    {
        var body = PlanningSections.Insert("obstacles", "<goal>\nShip.\n</goal>\n", "- none yet");

        body.ShouldBe("<goal>\nShip.\n</goal>\n<obstacles>\n- none yet\n</obstacles>\n");
    }

    [Fact]
    public void Insert_BodyWithoutTrailingNewline_AddsTheSeparator()
    {
        var body = PlanningSections.Insert("refs", "<goal>\nShip.\n</goal>", "docs/plans/x.md");

        body.ShouldBe("<goal>\nShip.\n</goal>\n<refs>\ndocs/plans/x.md\n</refs>\n");
    }

    [Fact]
    public void Insert_EmptyBody_RendersTheSectionAlone()
    {
        PlanningSections.Insert("scope", string.Empty, "Everything.").ShouldBe("<scope>\nEverything.\n</scope>\n");
    }

    [Fact]
    public void Insert_WithoutContent_RendersTheDeclaredEmptyMarker()
    {
        var body = PlanningSections.Insert("obstacles", "<goal>\nShip.\n</goal>\n", null);

        body.ShouldBe("<goal>\nShip.\n</goal>\n<obstacles>\n(none)\n</obstacles>\n");
        PlanningSections.Find("obstacles", body)!.Content.ShouldBe("(none)");
    }

    [Fact]
    public void Insert_ContentThatStripsToEmpty_FallsBackToTheMarker()
    {
        var body = PlanningSections.Insert("refs", string.Empty, "<refs>\n</refs>");

        body.ShouldBe("<refs>\n(none)\n</refs>\n");
    }

    [Fact]
    public void Insert_WhitespaceContent_RendersTheDeclaredEmptyMarker()
    {
        var body = PlanningSections.Insert("scope", string.Empty, "   \n  ");

        body.ShouldBe("<scope>\n(none)\n</scope>\n");
    }

    [Fact]
    public void Insert_PreservesEveryExistingByte()
    {
        const string body = "# Task\r\n\r\n<goal>\nShip.\n</goal>\n";

        PlanningSections.Insert("log", body, null).ShouldStartWith(body);
    }

    [Fact]
    public void Find_DuplicateAnchoredPairs_PrefersTheLastNonEmptyCopy()
    {
        var poisoned = "<goal>\nreal goal\n</goal>\n\n<goal>\n(none)\n</goal>\n";

        PlanningSections.Find("goal", poisoned)!.Content.ShouldBe("real goal");
    }

    [Fact]
    public void Find_AllAnchoredCopiesEmpty_LastPairWins()
    {
        var body = "<goal>\n(none)\n</goal>\n\n<goal>\n(none)\n</goal>\n";
        var secondOpen = body.IndexOf("<goal>", body.IndexOf("<goal>") + 1);

        var section = PlanningSections.Find("goal", body)!;
        section.Content.ShouldBe("(none)");
        section.OpenIndex.ShouldBe(secondOpen);
    }

    [Fact]
    public void ParseAcceptanceCriteria_PoisonedBody_SeesTheRealChecklist()
    {
        var poisoned = "<acceptance-criteria>\n- [ ] build green\n- [x] tests green</acceptance-criteria>\n<acceptance-criteria>\n(none)\n</acceptance-criteria>\n";

        var items = PlanningSections.ParseAcceptanceCriteria(poisoned);

        items.Count.ShouldBe(2);
        items[0].Checked.ShouldBe(false);
        items[1].Checked.ShouldBe(true);
    }

    [Theory]
    [InlineData("goal", true)]
    [InlineData("anti-scope", true)]
    [InlineData("my-tag", true)]
    [InlineData("a0", true)]
    [InlineData("Goal", false)]
    [InlineData("2fa", false)]
    [InlineData("a_b", false)]
    [InlineData("a.b", false)]
    [InlineData("", false)]
    public void IsNameParseable_MatchesTheFindGrammar(string name, bool expected)
    {
        PlanningSections.IsNameParseable(name).ShouldBe(expected);
    }
}

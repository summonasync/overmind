import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { createJiti } from "jiti";
import {
  caretAnchor,
  entryMarker,
  featureMarker,
  findWikiTrigger,
  hardLinesBefore,
  insertMarker,
  projectMarker,
  type FieldMetrics,
  type Rect,
} from "../src/features/journal/lib/note-links.ts";
import {
  DEFAULT_LIMITS,
  EMPTY_CANDIDATES,
  buildLinkSuggestions,
  isBareTitleLinkable,
  type CandidateSet,
} from "../src/features/journal/lib/link-targets.ts";
import {
  authorLine,
  creatableTitle,
  deadEndDetail,
  deadEndKind,
  deadEndLabel,
  groupOutgoing,
  linkedEntryIds,
  noteHref,
  outgoingHref,
  relatedQuery,
  relatedRows,
  streamHrefFrom,
} from "../src/features/journal/lib/note-rails.ts";
import {
  addTag,
  draftFromNote,
  isDraftDirty,
  saveBlocker,
  toFormValues,
} from "../src/features/journal/lib/note-form.ts";
import type { JournalNote } from "../src/features/journal/types.ts";

const sliceFile = (path: string) =>
  readFileSync(new URL(`../src/features/journal/${path}`, import.meta.url), "utf8");

// The same render door the markdown round-trip test uses: the shared viewer mounted through
// jiti, because it carries no React import of its own.
const jiti = createJiti(import.meta.url, {
  jsx: { runtime: "automatic", importSource: "react" },
  alias: { "@": new URL("../src/", import.meta.url).href },
});
const { MarkdownViewer } = await jiti.import("../src/shared/ui/markdown-viewer.tsx");
const { BacklinksRail } = await jiti.import(
  "../src/features/journal/components/backlinks-rail.tsx",
);
const { UnresolvedList } = await jiti.import(
  "../src/features/journal/components/unresolved-list.tsx",
);
const { WikiLinkAutocomplete } = await jiti.import(
  "../src/features/journal/components/wikilink-autocomplete.tsx",
);
const render = (content: string) =>
  renderToStaticMarkup(React.createElement(MarkdownViewer, { content }));

const noop = () => {};

// The files this task shipped, named once so every guard below says what it measured. Kept as a
// literal list on purpose: a glob over the directory would silently widen the hex and day-key
// asserts the next time anyone adds a file to the feature.
const T8_SLICE = [
  "components/note-view.tsx",
  "components/note-editor.tsx",
  "components/wikilink-autocomplete.tsx",
  "components/backlinks-rail.tsx",
  "components/outgoing-rail.tsx",
  "components/unresolved-list.tsx",
  "components/related-rail.tsx",
  "components/tag-editor.tsx",
  "lib/note-links.ts",
  "lib/link-targets.ts",
  "lib/note-rails.ts",
  "lib/note-form.ts",
  "lib/use-note-links.ts",
] as const;

const NOTE: JournalNote = {
  id: "11111111-1111-4111-8111-111111111111",
  projectId: "journal",
  type: "journal",
  title: "Standing note",
  content: "body",
  currentVersion: 3,
  createdAt: "2026-11-10T09:00:00.0000000Z",
  updatedAt: "2026-11-16T11:40:00.0000000Z",
  tags: ["overmind"],
  authorAlias: null,
  state: "active",
  outgoing: [],
  incoming: [],
  unresolved: [],
};

test("the marker spellings are the three the R3 grammar resolves", () => {
  assert.equal(entryMarker("  Deep work  "), "[[Deep work]]");
  assert.equal(projectMarker("Overmind"), "[[project:Overmind]]");
  assert.equal(featureMarker("184-obsidian-like-functionality"), "[[feature:184-obsidian-like-functionality]]");
});

test("the trigger reads a half-typed marker and refuses a finished one", () => {
  const text = "link [[Over";
  assert.deepEqual(findWikiTrigger(text, text.length), { start: 5, end: 11, partial: "Over" });

  const closed = "see [[Done]] and keep typing";
  assert.equal(findWikiTrigger(closed, 15), null);

  // A marker does not span a paragraph: the walk back stops at the newline, so a stray `[[`
  // three lines up cannot hijack the current line.
  const acrossLine = "starts [[here\nand now typing";
  assert.equal(findWikiTrigger(acrossLine, acrossLine.length), null);

  assert.equal(findWikiTrigger("nothing typed", 13), null);
  assert.deepEqual(findWikiTrigger("[[", 2), { start: 0, end: 2, partial: "" });
});

test("inserting a suggestion leaves the caret after the marker, not at the end", () => {
  const value = "link [[Over";
  const trigger = findWikiTrigger(value, value.length);
  assert.notEqual(trigger, null);
  const marker = entryMarker("Overmind");
  const at = trigger!;
  const inserted = insertMarker(value, at, value.length, marker);

  assert.equal(inserted.value, `link ${marker} `);
  assert.equal(inserted.caret, `link ${marker} `.length);

  // The replace eats the trigger and what was typed into it, and nothing past the caret: text
  // the owner has not reached yet stays where it is.
  const mid = "a [[Ox b";
  const t = findWikiTrigger(mid, 5);
  assert.notEqual(t, null);
  const out = insertMarker(mid, t!, 5, "[[Oxide]]");
  assert.equal(out.value, "a [[Oxide]] x b");
  assert.equal(out.caret, "a [[Oxide]] ".length);
});

test("the line counter reads hard newlines up to the caret", () => {
  assert.equal(hardLinesBefore("single line", 11), 0);
  assert.equal(hardLinesBefore("one\ntwo\nthree", 0), 0);
  assert.equal(hardLinesBefore("one\ntwo\nthree", 4), 1);
  assert.equal(hardLinesBefore("one\ntwo\nthree", 15), 2);
  // A clamped caret past the end must not over-count, and a negative one must not under-count.
  assert.equal(hardLinesBefore("one\ntwo", 99), 1);
  assert.equal(hardLinesBefore("one\ntwo", -3), 0);
});

test("the suggestion list hangs off the caret's own line, clamped into the field", () => {
  const field: Rect = { top: 100, left: 40, width: 700, height: 260 };
  // The positioned wrapper sits 24px above the field (its header row lives in between).
  const shell: Rect = { top: 76, left: 40, width: 700, height: 300 };
  const metrics: FieldMetrics = { paddingTop: 8, paddingLeft: 12, lineHeight: 20, scrollTop: 0 };

  // First line: 24 (field inside the wrapper) + 8 (padding) + 20 (one line) + 2 (the gap).
  const first = caretAnchor(field, shell, metrics, 0);
  assert.deepEqual(first, { left: 12, top: 54, width: 22 * 16 });

  const third = caretAnchor(field, shell, metrics, 2);
  assert.equal(third.top - first.top, 40, "two hard lines move the list by two line heights");

  // Scrolled past the line, the list stays at the top of the field instead of vanishing above it.
  const scrolled = caretAnchor(field, shell, { ...metrics, scrollTop: 500 }, 0);
  assert.equal(scrolled.top, 22, "clamped to the wrapper origin plus one line");

  // And it never lands below the field's last line.
  const beyond = caretAnchor(field, shell, { ...metrics, lineHeight: 200 }, 3);
  assert.equal(beyond.top, 286, "clamped to the field's bottom line");
});

test("the note arm offers only titles a bare marker can land on", () => {
  const candidates: CandidateSet = {
    ...EMPTY_CANDIDATES,
    notes: [
      { id: "a", title: "Overmind roadmap" },
      { id: "b", title: "feature:184" },
      { id: NOTE.id, title: "Standing note" },
      { id: "c", title: "  " },
    ],
  };

  const rows = buildLinkSuggestions(candidates, { query: "overmind", currentNoteId: NOTE.id });
  // The one existing title that matches is offered as a link, and the exact words typed are
  // offered as a marked creation — no note is titled just "overmind", so a silent link there
  // would be an edge that never exists.
  assert.deepEqual(
    rows.map((row) => [row.kind, row.marker, row.createsNote]),
    [
      ["note", "[[Overmind roadmap]]", false],
      ["create", "[[overmind]]", true],
    ],
  );

  // A note titled like a reserved prefix is unreachable as a bare marker, and the note itself is
  // never offered — spec R3 drops a self-link, so proposing one would put an edge the Host throws
  // away on the owner's screen.
  assert.equal(isBareTitleLinkable("feature:184"), false);
  assert.equal(isBareTitleLinkable("project:Overmind"), false);
  assert.equal(isBareTitleLinkable("   "), false);
  assert.equal(isBareTitleLinkable("Plain title"), true);
});

test("ranking puts the exact title first, then the prefix, then the substring", () => {
  const candidates: CandidateSet = {
    ...EMPTY_CANDIDATES,
    notes: [
      { id: "1", title: "Rewriting deep work today" },
      { id: "2", title: "Deep work" },
      { id: "3", title: "Deep work sessions" },
    ],
  };
  const rows = buildLinkSuggestions(candidates, { query: "deep work", currentNoteId: "x" });
  assert.deepEqual(rows.map((row) => row.label), [
    "Deep work",
    "Deep work sessions",
    "Rewriting deep work today",
  ]);
});

test("the project arm keeps the stored casing, the resolver's collision rule depends on it", () => {
  const candidates: CandidateSet = {
    ...EMPTY_CANDIDATES,
    projects: [
      { id: "p1", name: "Overmind" },
      { id: "p2", name: "overmind-archive" },
    ],
  };
  const rows = buildLinkSuggestions(candidates, { query: "over", currentNoteId: NOTE.id });
  assert.deepEqual(rows.filter((row) => row.kind === "project").map((row) => row.marker), [
    "[[project:Overmind]]",
    "[[project:overmind-archive]]",
  ]);
});

test("a feature slug borrowed by two boards is offered by neither", () => {
  const candidates: CandidateSet = {
    ...EMPTY_CANDIDATES,
    features: [
      { projectId: "p1", projectName: "One", slug: "184-shared", title: "Journal" },
      { projectId: "p2", projectName: "Two", slug: "184-shared", title: "Journal twin" },
      { projectId: "p1", projectName: "One", slug: "200-unique", title: "Unique work" },
    ],
  };
  const rows = buildLinkSuggestions(candidates, { query: "", currentNoteId: NOTE.id });
  const features = rows.filter((row) => row.kind === "feature");
  assert.deepEqual(features.map((row) => row.marker), ["[[feature:200-unique]]"]);
});

test("the create row is marked as a creation and never invented over an existing title", () => {
  const candidates: CandidateSet = { ...EMPTY_CANDIDATES, notes: [{ id: "n1", title: "Existing note" }] };

  const fresh = buildLinkSuggestions(candidates, { query: "Brand new", currentNoteId: NOTE.id });
  assert.deepEqual(fresh.map((row) => [row.kind, row.createsNote, row.detail]), [
    ["create", true, "creates a new note"],
  ]);
  assert.equal(fresh[0].marker, "[[Brand new]]");

  const existing = buildLinkSuggestions(candidates, { query: "Existing note", currentNoteId: NOTE.id });
  assert.deepEqual(existing.map((row) => row.kind), ["note"], "a note that exists is never offered as a creation");

  // A query the resolver would send to the feature/project arm has no honest note to create, and
  // brackets would tear the marker apart on insert.
  for (const query of ["feature:184", "project:Overmind", "a[b]c"]) {
    assert.deepEqual(buildLinkSuggestions(candidates, { query, currentNoteId: NOTE.id }), []);
  }
});

test("the arms are capped, so the list stays a picker and not a dump", () => {
  const candidates: CandidateSet = {
    notes: Array.from({ length: 30 }, (_, i) => ({ id: `n${i}`, title: `Note ${i}` })),
    projects: Array.from({ length: 10 }, (_, i) => ({ id: `p${i}`, name: `Proj ${i}` })),
    features: Array.from({ length: 10 }, (_, i) => ({
      projectId: `p${i}`,
      projectName: `Proj ${i}`,
      slug: `0${i}-feature`,
      title: `Feature ${i}`,
    })),
  };
  const rows = buildLinkSuggestions(candidates, { query: "", currentNoteId: NOTE.id });
  const counted = {
    note: rows.filter((row) => row.kind === "note").length,
    project: rows.filter((row) => row.kind === "project").length,
    feature: rows.filter((row) => row.kind === "feature").length,
  };
  assert.deepEqual(counted, {
    note: DEFAULT_LIMITS.notes,
    project: DEFAULT_LIMITS.projects,
    feature: DEFAULT_LIMITS.features,
  });
});

test("no suggestion can masquerade as an edge the Host would not create", () => {
  // The closure property, stated over the generator's own vocabulary: every row that is not
  // labelled as a creation carries a marker the R3 grammar resolves — a reserved prefix with a
  // non-empty key, or a bare title the parser will actually treat as a title.
  const candidates: CandidateSet = {
    notes: [
      { id: "n1", title: "Plain" },
      { id: "n2", title: "feature:sneaky" },
      { id: "n3", title: "" },
      { id: "n4", title: "  padded  " },
    ],
    projects: [{ id: "p1", name: "Overmind" }],
    features: [{ projectId: "p1", projectName: "Overmind", slug: "184-x", title: "X" }],
  };
  for (const query of ["", "f", "feat", "plain", "pad", "no-such", "project:", "184"]) {
    for (const row of buildLinkSuggestions(candidates, { query, currentNoteId: "n1" })) {
      const inner = row.marker.slice(2, -2);
      assert.match(row.marker, /^\[\[.+\]\]$/, `empty marker for query ${query}`);
      if (row.kind === "feature") {
        assert.match(inner, /^feature:[a-z0-9][a-z0-9-]*$/, inner);
      } else if (row.kind === "project") {
        assert.match(inner, /^project:\S+$/, inner);
      } else {
        assert.ok(isBareTitleLinkable(inner), `bare marker cannot resolve: ${inner}`);
        if (row.kind === "note") {
          assert.equal(row.createsNote, false);
        }
      }
    }
  }
});

test("the rails read the detail payload and never re-parse the body", () => {
  const note: JournalNote = {
    ...NOTE,
    outgoing: [
      { kind: "entry", id: "e1", label: "Other note" },
      { kind: "project", id: "p1", label: "Overmind" },
      { kind: "feature", id: "f1", label: "Journal" },
    ],
    incoming: [{ entryId: "e2", title: "Source", snippet: "…links here" }],
    unresolved: ["feature:999-missing", "Project:Overmind ", "Not Written Yet"],
  };

  const groups = groupOutgoing(note.outgoing);
  assert.deepEqual(Object.keys(groups).map((k) => `${k}:${groups[k as keyof typeof groups].length}`), [
    "notes:1",
    "projects:1",
    "features:1",
  ]);

  assert.equal(outgoingHref(note.outgoing[0]), `/journal?note=${encodeURIComponent("e1")}`);
  assert.equal(outgoingHref(note.outgoing[1]), `/dashboard?project=${encodeURIComponent("p1")}`);
  // The feature edge carries a guid and a title only, and every feature URL in this app is
  // `project=<id>&feature=<slug>`. No slug, no link — a guess would land on the wrong board.
  assert.equal(outgoingHref(note.outgoing[2]), null);

  assert.deepEqual([...linkedEntryIds(note.incoming, note.outgoing)].sort(), ["e1", "e2"]);

  assert.equal(deadEndKind("feature:999-missing"), "feature");
  assert.equal(deadEndKind("Project:Overmind "), "project");
  assert.equal(deadEndKind("Not Written Yet"), "note");
  assert.equal(deadEndLabel("Project:Overmind "), "Overmind");
  assert.ok(deadEndDetail("project").length > 0);

  // Creating a note only clears a dead end a bare title can land on.
  assert.equal(creatableTitle("Not Written Yet"), "Not Written Yet");
  assert.equal(creatableTitle("feature:999-missing"), null);
  assert.equal(creatableTitle("Project:Overmind"), null);
});

test("the author line is the boundary ruling made visible", () => {
  assert.deepEqual(authorLine(null), { label: "you", agent: false });
  assert.deepEqual(authorLine("   "), { label: "you", agent: false });
  assert.deepEqual(authorLine("diary-sensei"), { label: "diary-sensei", agent: true });
});

test("related rows drop the note itself, its neighbours and duplicates", () => {
  const hits = [
    { entryId: NOTE.id, title: "self", snippet: "", type: "journal" },
    { entryId: "e1", title: "already linked", snippet: "", type: "journal" },
    { entryId: "e2", title: "first", snippet: "s", type: "journal" },
    { entryId: "e2", title: "again", snippet: "s", type: "journal" },
    { entryId: "e3", title: "third", snippet: "s", type: "journal" },
  ];
  const rows = relatedRows(hits, { selfId: NOTE.id, linkedIds: new Set(["e1"]) });
  assert.deepEqual(rows.map((row) => row.title), ["first", "third"]);

  const capped = relatedRows(
    Array.from({ length: 9 }, (_, i) => ({ entryId: `x${i}`, title: `t${i}`, snippet: "", type: "journal" })),
    { selfId: NOTE.id },
  );
  assert.equal(capped.length, 5);
});

test("the embedding query is clamped in code points, not UTF-16 units", () => {
  const long = relatedQuery("Title", "\u{1D569}".repeat(900));
  assert.equal(Array.from(long).length, 500);
  // Cutting on a unit would ship half a surrogate pair; counting code points means the astral
  // character either survives whole or not at all.
  assert.equal(/[\uD800-\uDBFF](?![\uDC00-\uDFFF])/.test(long), false, "a lone high surrogate");
});

test("the back link carries the filters the owner arrived on and drops the note", () => {
  assert.equal(streamHrefFrom("?day=2026-11-16&note=abc&tag=x"), "/journal?day=2026-11-16&tag=x");
  assert.equal(streamHrefFrom("note=abc"), "/journal");
  assert.equal(noteHref("a b"), "/journal?note=a%20b");
});

test("the draft compares tags as a set and refuses what the validator refuses", () => {
  const saved = draftFromNote({ ...NOTE, tags: ["one", "two"] });
  assert.equal(isDraftDirty({ ...saved, tags: ["two", "one"] }, saved), false);
  assert.equal(isDraftDirty({ ...saved, tags: ["two", "ONE "] }, saved), false);
  assert.equal(isDraftDirty({ ...saved, tags: ["two"] }, saved), true);

  const added = addTag(saved, " three ");
  assert.deepEqual(added.draft.tags, ["one", "two", "three"]);
  assert.equal(added.error, null);
  assert.match(addTag(saved, "a,b").error ?? "", /comma/);
  assert.match(addTag(saved, "ONE").error ?? "", /already/);
  assert.match(addTag(saved, "   ").error ?? "", /Nothing/);
  assert.equal(toFormValues(saved)?.title, "Standing note");

  // The Host pins Title and Content NotEmpty; the button and its explanation answer with the one
  // blocker the save path would hit.
  assert.match(saveBlocker({ title: "", content: "   ", tags: [] }) ?? "", /body/);
  assert.equal(saveBlocker({ title: "", content: "first line", tags: [] }), null);
  assert.equal(toFormValues({ title: "", content: "first line", tags: [] })?.title, "first line");
});

test("the slice owns one day key — the stream's localDayOf — and no second one", () => {
  const view = sliceFile("components/note-view.tsx");
  assert.match(view, /formatJournalStamp/, "the note view stopped stamping through the shared key");
  const rail = sliceFile("components/backlinks-rail.tsx");
  assert.match(rail, /localDayOf\(updatedAt\)/, "the day row stopped using the one day key");
  assert.match(rail, /from "@\/features\/journal\/lib\/stream-filters"/, "the day row grew its own key");

  for (const file of T8_SLICE) {
    const source = sliceFile(file);
    assert.equal(/toISOString\(/.test(source), false, `${file} slices a UTC day out of a timestamp`);
    assert.equal(/toLocaleDate/.test(source), false, `${file} formats a date through the locale`);
    assert.equal(/new Date\([^)]*createdAt/.test(source), false, `${file} keyed a day off createdAt`);
  }
});

test("rails are kept honest by invalidation, never by writing rows into the cache", () => {
  const view = sliceFile("components/note-view.tsx");
  assert.match(
    view,
    /invalidateQueries\(\{ queryKey: journalKeys\.detailsRoot\(\) \}\)/,
    "the note view stopped staling the detail shelf it shares with other notes",
  );
  for (const file of [...T8_SLICE, "lib/use-journal.ts", "queries.ts"]) {
    assert.equal(/\.setQueryData\s*\(/.test(sliceFile(file)), false, `${file} synthesised a cache entry`);
  }
  assert.match(
    sliceFile("queries.ts"),
    /detailsRoot: \(\) => \[\.\.\.journalKeys\.all, "detail"\] as const/,
    "the detail prefix the rails stale is no longer spelled by the key factory",
  );
});

test("the slice adds no competing window listener and does not loosen the dialog guard", () => {
  // shared/ui/modal.tsx binds Escape on window, and task 6's capture hook bails when a dialog is
  // already open. Both facts survive this task: the only keydown listener in the feature is the
  // one task 6 shipped, and the guard is still there.
  for (const file of T8_SLICE) {
    const source = sliceFile(file);
    assert.equal(
      /window\.addEventListener\(/.test(source),
      false,
      `${file} added a second window-level listener to a surface where dialogs stack`,
    );
  }
  const hotkeys = sliceFile("lib/use-capture-hotkeys.ts");
  assert.match(hotkeys, /querySelector\('\[role="dialog"\]'\)/, "the stacked-dialog guard was loosened");
  const editor = sliceFile("components/note-editor.tsx");
  assert.match(editor, /event\.stopPropagation\(\)/, "Escape on an open list would close the dialog under it");
});

test("the note view mounts the shared back link as its first row", () => {
  const view = sliceFile("components/note-view.tsx");
  assert.match(view, /from "@\/shared\/ui\/page-back-link"/);
  const backAt = view.indexOf("<PageBackLink");
  const titleAt = view.indexOf('aria-label="Note title"');
  assert.ok(backAt > 0, "the note view lost the shared back link");
  assert.ok(titleAt > 0 && backAt < titleAt, "the back link is no longer above the title");
});

test("the autocomplete is a combobox the textarea points at, not a second focus stop", () => {
  const editor = sliceFile("components/note-editor.tsx");
  const list = sliceFile("components/wikilink-autocomplete.tsx");

  for (const needle of [
    'role: "combobox"',
    '"aria-expanded"',
    '"aria-autocomplete": "list"',
    '"aria-controls"',
    "aria-activedescendant",
    "ArrowDown",
    "ArrowUp",
    '"Tab"',
  ]) {
    assert.ok(editor.includes(needle), `the editor lost ${needle}`);
  }
  for (const needle of ['role="listbox"', 'role="option"', "aria-selected", "onMouseDown"]) {
    assert.ok(list.includes(needle), `the listbox lost ${needle}`);
  }
  // The list must not steal focus on mousedown or the caret the insert is written around is gone.
  assert.match(list, /onMouseDown=\{\(event\) => event\.preventDefault\(\)\}/);
});

test("the note view edits through the shared markdown pair, not a private one", () => {
  const editor = sliceFile("components/note-editor.tsx");
  assert.match(editor, /from "@\/shared\/ui\/markdown-editor"/);
  assert.match(editor, /<MarkdownEditor/);
  assert.equal(/from "@\/shared\/ui\/textarea"/.test(editor), false, "a second write/preview pair snuck in");
  assert.equal(/write-preview-toggle/.test(editor), false, "the toggle escaped the shared editor");
});

test("zero hex literals in the slice", () => {
  const offenders: string[] = [];
  for (const file of T8_SLICE) {
    sliceFile(file)
      .split("\n")
      .forEach((line, i) => {
        if (/#[0-9a-fA-F]{3,8}\b/.test(line)) {
          offenders.push(`${file}:${i + 1}`);
        }
      });
  }
  assert.deepEqual(offenders, []);
});

test("the slice reaches siblings through their barrels only", () => {
  // The whole-tree wall lives in journal-stream.test.mts; this one is the local ledger, so a
  // deep import added here fails with the file named instead of in a tree-wide list.
  const offenders: string[] = [];
  let scanned = 0;
  for (const file of T8_SLICE) {
    const own = "journal";
    sliceFile(file)
      .split("\n")
      .forEach((line) => {
        for (const match of line.matchAll(/\b(?:from|import|require)\s*\(?\s*"([^"]+)"\s*\)?/g)) {
          const spec = match[1];
          if (!spec.startsWith("@/features/")) continue;
          scanned += 1;
          const target = spec.slice("@/features/".length);
          const parts = target.split("/");
          if (parts.length >= 2 && parts[0] !== own) {
            offenders.push(`${file} -> ${spec}`);
          }
        }
      });
  }
  assert.deepEqual(offenders, []);
  // The denominator, asserted so a green here cannot come from the scan having seen nothing.
  assert.ok(scanned >= 3, `the barrel scan only looked at ${scanned} sibling imports`);
});

test("no second wikilink parser lives in this slice", () => {
  // R7: the read path derives the rails with the one grammar, on the Host. The editor may read
  // the trigger off the caret — that is keystroke math, not grammar — so the trigger vocabulary is
  // allowed in exactly two files: the module that defines it and the module that drives it.
  const owners = ["lib/note-links.ts", "components/note-editor.tsx"];
  for (const file of T8_SLICE) {
    const mentions = /WIKI_TRIGGER|findWikiTrigger|insertMarker/.test(sliceFile(file));
    assert.equal(
      mentions,
      owners.includes(file),
      `${file} reached for the marker grammar${owners.includes(file) ? "" : " — rails must come off the detail payload"}`,
    );
  }
});

test("a wikilink stays text in the preview, inside a fence and outside one", () => {
  const html = render(
    [
      "A plain [[Target]] in prose.",
      "",
      "```",
      "[[InFence]]",
      "```",
      "",
      "Inline [[InCode]] too.",
    ].join("\n"),
  );

  // Positive control first: the markers really did render, so the assert below measures a body
  // carrying wikilinks rather than an empty render.
  assert.match(html, /\[\[Target\]\]/);
  assert.match(html, /<pre[\s>][\s\S]*\[\[InFence\]\]/, "the fenced block stopped rendering");
  // The criterion itself: the preview turns no marker into a link anywhere — fenced, inline or
  // bare. Navigation over edges is the rail's job, and the rail reads the Host's resolver
  // instead of a second client-side matcher.
  assert.equal(/<a\b/.test(html), false, "the preview linked a wikilink marker");
});

test("a linked note renders its backlinks, an unlinked one says so out loud", () => {
  const rail = (backlinks: readonly { entryId: string; title: string; snippet: string }[]) =>
    renderToStaticMarkup(
      React.createElement(BacklinksRail, {
        backlinks,
        askRelated: noop,
        askingRelated: false,
        onNavigate: noop,
      }),
    );

  const empty = rail([]);
  assert.match(empty, /Backlinks[\s\S]{0,16}\(0\)/);
  assert.match(empty, /Nobody links here yet\./, "the empty state lost its explicit line");
  assert.match(empty, /Suggest related/, "the empty state lost the embeddings door");

  const filled = rail([
    { entryId: "e1", title: "Source note", snippet: "…and it points here" },
    { entryId: "e2", title: "   ", snippet: "" },
  ]);
  assert.match(filled, /Backlinks[\s\S]{0,16}\(2\)/);
  assert.match(filled, /href="\/journal\?note=e1"/);
  assert.match(filled, /Source note/);
  assert.match(filled, /…and it points here/, "the Host's context line never reached the row");
  // An untitled source still routes somewhere the owner can read, and never renders blank.
  assert.match(filled, /Untitled note/);
});

test("the dead-end list shows the target verbatim and offers a create only where one helps", () => {
  const html = renderToStaticMarkup(
    React.createElement(UnresolvedList, {
      targets: ["Not Written Yet", "feature:999-missing"],
      creating: null,
      onCreate: noop,
    }),
  );
  assert.match(html, /Unresolved links/);
  assert.match(html, /Not Written Yet/);
  assert.match(html, /feature:999-missing/);
  assert.match(html, /Create “Not Written Yet”/, "the note dead end lost its create affordance");
  // A feature marker cannot be cleared by writing a note, so no button lies about it.
  assert.equal(/Create “feature:999-missing”/.test(html), false);

  const none = renderToStaticMarkup(
    React.createElement(UnresolvedList, { targets: [], creating: null, onCreate: noop }),
  );
  assert.equal(none, "", "an empty unresolved list renders a section nobody asked for");
});

test("a proposal that creates a note is labelled as a creation on screen", () => {
  const rows = buildLinkSuggestions(
    { ...EMPTY_CANDIDATES, notes: [{ id: "n1", title: "Existing note" }] },
    { query: "Brand new", currentNoteId: NOTE.id },
  );
  const html = renderToStaticMarkup(
    React.createElement(WikiLinkAutocomplete, {
      anchor: { left: 0, top: 0, width: 200 },
      rows,
      activeIndex: 0,
      onPick: noop,
      onHover: noop,
    }),
  );
  assert.match(html, /Brand new/);
  assert.match(html, /creates new note/, "the creation row carried no visible mark");
  assert.match(html, /role="option"/);
  assert.match(html, /aria-selected="true"/, "the active row is not announced as active");
});

test("the slice file names are the ones the guards above measure", () => {
  // Every guard reads the list above, so a stale name there is a silent shrink of the hex,
  // day-key and listener asserts. The list is checked against disk, and its size is pinned so a
  // dropped entry cannot pass unnoticed.
  for (const file of T8_SLICE) {
    assert.ok(sliceFile(file).length > 200, `${file} is missing, empty or wrong`);
  }
  assert.equal(T8_SLICE.length, 13);
});

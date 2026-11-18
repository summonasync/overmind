import { strict as assert } from "node:assert";
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { test } from "node:test";
import {
  EMPTY_JOURNAL_FILTERS,
  formatJournalStamp,
  journalServerFilters,
  localDayOf,
  parseJournalFilters,
  selectJournalDay,
  sortByActivityDesc,
  writeJournalFilters,
} from "../src/features/journal/lib/stream-filters.ts";
import { journalStreamParams } from "../src/features/journal/lib/stream-query.ts";

const qs = (search: string) => new URLSearchParams(search);

test("all four inbox filters are read off the URL", () => {
  const filters = parseJournalFilters(
    qs("?day=2026-11-17&tag=overmind&link=unlinked&project=ff313f29-8144-488c-94e6-5ea5dde57bfd"),
  );
  assert.deepEqual(filters, {
    day: "2026-11-17",
    tag: "overmind",
    link: "unlinked",
    project: "ff313f29-8144-488c-94e6-5ea5dde57bfd",
  });
});

test("a bare /journal reads as no filters at all", () => {
  assert.deepEqual(parseJournalFilters(null), EMPTY_JOURNAL_FILTERS);
  assert.deepEqual(parseJournalFilters(qs("")), EMPTY_JOURNAL_FILTERS);
});

// The server would answer 400 UnknownLinkFilter for a bogus cohort; the URL is hand-
// editable, so the page degrades to "no cohort filter" instead of an error screen.
test("a link value outside the server vocabulary drops, casing does not", () => {
  assert.equal(parseJournalFilters(qs("?link=everything")).link, "");
  assert.equal(parseJournalFilters(qs("?link=LINKED")).link, "linked");
  assert.equal(parseJournalFilters(qs("?link= unlinked ")).link, "unlinked");
  assert.equal(parseJournalFilters(qs("?day=2026-02-31")).day, "");
});

test("filters round-trip through the URL and survive a reload", () => {
  const filters = parseJournalFilters(qs("?day=2026-11-16&tag=estimates&link=linked&project=p1"));
  const params = writeJournalFilters(qs(""), filters);
  assert.deepEqual(parseJournalFilters(params), filters);
});

test("writing filters leaves params this view does not own alone", () => {
  const target = qs("?note=6c221af1&day=2026-11-17");
  writeJournalFilters(target, { ...EMPTY_JOURNAL_FILTERS, day: "2026-11-18" });
  assert.equal(target.get("note"), "6c221af1");
  assert.equal(target.get("day"), "2026-11-18");
});

test("an emptied filter deletes its key instead of writing an empty one", () => {
  const target = qs("?tag=overmind&day=2026-11-17");
  writeJournalFilters(target, EMPTY_JOURNAL_FILTERS);
  assert.equal(target.has("tag"), false);
  assert.equal(target.has("day"), false);
  assert.equal(target.toString(), "");
});

test("the day never reaches the server query", () => {
  const filters = journalServerFilters(parseJournalFilters(qs("?day=2026-11-17&tag=overmind")));
  assert.deepEqual(filters, { tag: "overmind", link: "", project: "" });
  const params = journalStreamParams(filters);
  assert.equal(params.get("day"), null);
  assert.equal(params.get("since"), null);
  assert.equal(params.get("until"), null);
});

// The load-bearing one: link and project are sent to the Host, they are not a cache-key
// echo. EntryStreamFilter.ValidateLink accepts exactly these two spellings.
test("link and project ride the request the stream makes", () => {
  const query = journalStreamParams({
    tag: "overmind",
    link: "linked",
    project: "ff313f29-8144-488c-94e6-5ea5dde57bfd",
  }).toString();
  assert.equal(
    query,
    "type=journal&state=active&tag=overmind&link=linked&project=ff313f29-8144-488c-94e6-5ea5dde57bfd",
  );
});

test("an unset filter is absent from the request, not sent empty", () => {
  const query = journalStreamParams({ tag: "", link: "", project: "" }).toString();
  assert.equal(query, "type=journal&state=active");
});

// The card accent must be the same predicate the chip filters on, so it is the same
// request with the cohort forced — not a browser-side reading of the rails.
test("the accent cohort is the page request with link forced and nothing else changed", () => {
  const page = journalServerFilters(parseJournalFilters(qs("?tag=overmind&day=2026-11-17&project=p9")));
  const cohort = journalStreamParams({ ...page, link: "linked" });
  assert.equal(cohort.get("link"), "linked");
  assert.equal(cohort.get("tag"), page.tag);
  assert.equal(cohort.get("project"), page.project);
  assert.equal(cohort.get("day"), null);
  assert.equal(
    cohort.toString(),
    journalStreamParams({ tag: "overmind", link: "linked", project: "p9" }).toString(),
  );
});

function localNoonOf(offsetDays: number): Date {
  const at = new Date();
  at.setDate(at.getDate() + offsetDays);
  at.setHours(12, 0, 0, 0);
  return at;
}

function naiveUtcStamp(at: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${at.getUTCFullYear()}-${pad(at.getUTCMonth() + 1)}-${pad(at.getUTCDate())}T${pad(
    at.getUTCHours(),
  )}:${pad(at.getUTCMinutes())}:${pad(at.getUTCSeconds())}`;
}

const dayKey = (at: Date) => {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}`;
};

test("local day flips at local midnight, not at the UTC one", () => {
  const midnight = new Date();
  midnight.setHours(0, 0, 0, 0);
  const today = dayKey(midnight);
  const yesterday = new Date(midnight.getTime() - 1);
  assert.equal(localDayOf(naiveUtcStamp(midnight)), today);
  assert.equal(localDayOf(naiveUtcStamp(new Date(midnight.getTime() + 1000))), today);
  assert.equal(localDayOf(naiveUtcStamp(yesterday)), dayKey(yesterday));
  assert.notEqual(dayKey(yesterday), today);
});

test("the stamp is the local clock the owner reads", () => {
  const at = localNoonOf(0);
  assert.equal(formatJournalStamp(naiveUtcStamp(at)), `${dayKey(at)} 12:00`);
});

// AC: a note written at 23:58 belongs to that day; the same note edited past midnight
// moves to the next one. Both instants are built from the machine's own local midnight so
// the assertion holds in any zone and fails the moment the boundary goes string-compare.
test("a note follows its last touch across local midnight", () => {
  const midnight = new Date();
  midnight.setHours(0, 0, 0, 0);
  const writtenAt = new Date(midnight.getTime() + (23 * 60 + 58) * 60_000);
  const editedAt = new Date(midnight.getTime() + (24 * 60 + 5) * 60_000);
  const today = dayKey(midnight);
  const tomorrow = dayKey(editedAt);
  assert.notEqual(today, tomorrow);

  const note = { id: "n1", updatedAt: naiveUtcStamp(writtenAt) };
  assert.deepEqual(selectJournalDay([note], today).map((row) => row.id), ["n1"]);
  assert.deepEqual(selectJournalDay([note], tomorrow), []);

  const edited = { id: "n1", updatedAt: naiveUtcStamp(editedAt) };
  assert.deepEqual(selectJournalDay([edited], tomorrow).map((row) => row.id), ["n1"]);
  assert.deepEqual(selectJournalDay([edited], today), []);
});

// The teeth of the AC above depend on the machine's zone, and on this box (+03) the 23:58
// leg is blind: local 23:58 and the UTC date of the same instant agree there, so a
// `toISOString().slice(0, 10)` implementation would pass it. The boundary is replayed in
// two pinned zones on opposite sides of UTC — +14 puts the hours after midnight on the
// previous UTC day, -11 puts the hours before midnight on the next one — and each leg
// first proves that the naive string compare really disagrees in that zone.
test("the local day beats a UTC string compare in both zone directions", () => {
  const inherited = process.env.TZ;
  try {
    process.env.TZ = "Pacific/Kiritimati";
    const afterMidnight = naiveUtcStamp(new Date(2026, 10, 19, 0, 5, 0));
    assert.equal(localDayOf(afterMidnight), "2026-11-19");
    assert.notEqual(localDayOf(afterMidnight), afterMidnight.slice(0, 10));
    assert.deepEqual(
      selectJournalDay([{ id: "n1", updatedAt: afterMidnight }], "2026-11-19").map((row) => row.id),
      ["n1"],
    );
    assert.deepEqual(selectJournalDay([{ id: "n1", updatedAt: afterMidnight }], "2026-11-18"), []);

    process.env.TZ = "Pacific/Midway";
    const beforeMidnight = naiveUtcStamp(new Date(2026, 10, 18, 23, 58, 0));
    assert.equal(localDayOf(beforeMidnight), "2026-11-18");
    assert.notEqual(localDayOf(beforeMidnight), beforeMidnight.slice(0, 10));
    assert.deepEqual(
      selectJournalDay([{ id: "n1", updatedAt: beforeMidnight }], "2026-11-18").map((row) => row.id),
      ["n1"],
    );
    assert.deepEqual(selectJournalDay([{ id: "n1", updatedAt: beforeMidnight }], "2026-11-19"), []);
  } finally {
    if (inherited === undefined) {
      delete process.env.TZ;
    } else {
      process.env.TZ = inherited;
    }
  }
});

test("a malformed timestamp is dropped from the day shelf instead of landing on today", () => {
  assert.equal(localDayOf("not-a-stamp"), null);
  assert.deepEqual(selectJournalDay([{ id: "n1", updatedAt: "not-a-stamp" }], dayKey(new Date())), []);
});

test("the stream is newest activity first", () => {
  const older = naiveUtcStamp(localNoonOf(-1));
  const newer = naiveUtcStamp(localNoonOf(0));
  const sorted = sortByActivityDesc([
    { id: "a", updatedAt: older },
    { id: "b", updatedAt: newer },
  ]);
  assert.deepEqual(sorted.map((row) => row.id), ["b", "a"]);
  assert.deepEqual(selectJournalDay(sorted, "").map((row) => row.id), ["b", "a"]);
});

// Drift pin: the stream sends names, the Host binds names. If a parameter is renamed on
// either side this goes red, instead of the filter silently turning into a no-op over
// unfiltered rows — the exact failure that made the chip a blocker here.
test("the Host binds every parameter the stream sends", () => {
  const api = readFileSync(join("..", "src", "Overmind.Host", "Api", "EntriesApi.cs"), "utf8");
  const handler = /private static async Task<IResult> ListByProject\(([\s\S]*?)\)\s/.exec(api);
  assert.ok(handler, "ListByProject signature not found in EntriesApi.cs");
  const bound = handler[1];
  for (const name of ["type", "tag", "state", "link", "project"]) {
    assert.match(bound, new RegExp(`string\\? ${name},`), `${name} is not a bound parameter`);
  }
  assert.match(bound, /EntryLinkTriage triage,/);
});

// The accent classes are only safe because the generated stylesheet emits them after the
// card's plain `border` and `border-border`: web's cn() concatenates, so a tie would be
// decided by the compiler. This measures the compiler instead of assuming it. It also
// catches the accent token disappearing from the build, which is how a renamed utility
// silently turns a colour-coded inbox back into a flat list.
test("generated css orders the accent utilities after the plain border", () => {
  const chunksDir = join("out", "_next", "static", "chunks");
  if (!existsSync(chunksDir)) {
    assert.fail("run npm run build first: out/_next/static/chunks is absent");
  }
  const sheets = readdirSync(chunksDir).filter((name) => name.endsWith(".css"));
  assert.ok(sheets.length > 0, "run npm run build first: no *.css under out/_next/static/chunks");
  const css = sheets.map((name) => readFileSync(join(chunksDir, name), "utf8")).join("\n");
  const offset = (token: string) => css.lastIndexOf(`.${token}{`);
  const rule = (token: string) => {
    const at = offset(token);
    return at < 0 ? "" : css.slice(at, css.indexOf("}", at) + 1);
  };

  assert.match(rule("border"), /border-width:1px/);
  assert.match(rule("border-l-2"), /border-left-width:2px/);
  // The accent must paint the left edge only: the all-sides spelling would frame the card.
  for (const token of ["border-l-info", "border-l-success"]) {
    assert.match(rule(token), /border-left-color:/);
    assert.ok(offset(token) >= 0, `${token} is not in the generated css at all`);
    assert.ok(
      offset(token) > offset("border-border"),
      `${token} at ${offset(token)} precedes border-border at ${offset("border-border")}`,
    );
  }
  assert.ok(offset("border-l-2") > offset("border"));
});

function sourceOf(path: string): string {
  return readFileSync(join("src", path), "utf8");
}

// Shape asserts, not render captures: there is no browser in this lane and the running
// Host predates the journal container, so these pin the mechanism the keyboard path and
// the four states rely on. They go red the moment the mechanism is edited away; they are
// not a substitute for the visual pass, which is written up as an open leg.
test("card actions are real focusable buttons revealed by focus as well as hover", () => {
  const card = sourceOf("features/journal/components/journal-card.tsx");
  assert.match(card, /onClick=\{\(\) => onOpen\(note\)\}/);
  assert.match(card, /aria-label=\{`Delete \$\{label\}`\}/);
  assert.match(card, /tabIndex=\{0\}/);
  // Passport §7 puts the card in the tab order, so the stop has to do something: Enter on
  // the card opens the note. The target check is the safety half — Enter on the Open or
  // Delete button bubbles up from that button, and an unguarded card handler would open the
  // note on top of the action the control was asked to take.
  assert.match(card, /onKeyDown=\{\(event\) => \{/);
  assert.match(card, /event\.target !== event\.currentTarget/);
  assert.match(card, /event\.key !== "Enter"/);
  // Opacity, not conditional rendering: a node mounted only while the pointer is over it
  // is unreachable by Tab, and the passport asks for focus-within alongside hover.
  assert.match(card, /opacity-0 group-hover:opacity-100 group-focus-within:opacity-100/);
  assert.equal(/\{(?:hovered|isHovered|hovering) &&/.test(card), false, "actions render only on hover");
  assert.equal(/<div[^>]*\bonClick=/.test(card), false, "a div stands in for a button");
});

test("the stream owns the four states and borrows the shell's one degraded banner", () => {
  const stream = sourceOf("features/journal/components/journal-stream.tsx");
  assert.match(stream, /animate-pulse/);
  assert.equal(/loading-state/.test(stream), false, "the stream fell back to the spinner state");
  assert.match(stream, /<EmptyState\b/);
  assert.match(stream, /<ErrorText error=\{error\} \/>/);
  assert.match(stream, /onClick=\{onRetry\}/);
  // Passport §7 sizes come from the shared slot table, so a size change is one edit.
  assert.match(sourceOf("shared/ui/empty-state.tsx"), /pose="confused" size=\{ZERLING_SIZES\.display\}/);
  assert.match(sourceOf("shared/ui/error-text.tsx"), /pose="yelling" size=\{ZERLING_SIZES\.banner\}/);

  // One degraded banner for the whole app, mounted by the shell that wraps this route:
  // a second copy on the journal page would double the same warning.
  const page = sourceOf("pages/journal-page/ui/journal-page.tsx");
  assert.equal(/DegradedBanner/.test(page), false);
  assert.match(sourceOf("app/layout.page.tsx"), /<AppShell>\{children\}<\/AppShell>/);
  const banner = sourceOf("widgets/degraded-banner/ui/degraded-banner.tsx");
  assert.match(banner, /pose="sleeping" size=\{ZERLING_SIZES\.banner\}/);
  assert.match(banner, /aria-label="Dismiss degraded mode notice"/);
});

// STRUCTURE.md:96 — a feature reaches a sibling through its barrel, never through its
// internals. The journal stream shipped the first two deep imports this tree had, so the
// rule is measured here rather than left to the next pair of eyes: every module specifier
// that crosses a feature edge is resolved, and one landing inside a sibling's subtree goes
// red with the file:line that did it. A shared-layer relative import is not an offender —
// shared sits below features, not beside them.
//
// Task 8 widened the matcher. The original `from "…"` form could not see a side-effect
// `import "…"` or a dynamic `import("…")`, and the task-7 review planted both and measured them
// green (review-184-17 n5). The next test is the proof that this matcher sees them now.
const SPECIFIER = /\b(?:from|import|require)\s*\(?\s*"([^"]+)"\s*\)?/g;

function barrelViolation(spec: string, own: string, file: string): boolean {
  const featuresDir = join("src", "features");
  let target: string;
  if (spec.startsWith("@/features/")) {
    target = spec.slice("@/features/".length);
  } else if (spec.startsWith(".")) {
    const hop = relative(featuresDir, resolve(dirname(file), spec));
    if (hop.startsWith("..")) return false;
    target = hop.split(sep).join("/");
  } else return false;
  const parts = target.split("/");
  return parts.length >= 2 && parts[0] !== own && parts[1] !== "index";
}

test("no feature imports another feature's internals", () => {
  const featuresDir = join("src", "features");
  const slash = (path: string) => path.split(sep).join("/");
  const offenders: string[] = [];
  for (const rel of readdirSync(featuresDir, { recursive: true })) {
    const at = join(featuresDir, rel);
    if (!/\.tsx?$/.test(rel) || !statSync(at).isFile()) continue;
    const own = slash(rel).split("/")[0];
    readFileSync(at, "utf8")
      .split("\n")
      .forEach((line, i) => {
        for (const match of line.matchAll(SPECIFIER)) {
          if (barrelViolation(match[1], own, at)) {
            offenders.push(`${rel}:${i + 1} -> ${match[1]}`);
          }
        }
      });
  }
  assert.deepEqual(offenders, []);
});

// The guard proves itself against the exact shapes that used to slip: assert the matcher, not
// just the empty offender list — a scan over a tree with nothing in it is a green that
// measured nothing.
test("the barrel wall reads side-effect and dynamic imports too", () => {
  const file = join("src", "features", "journal", "components", "probe.tsx");
  const read = (line: string) =>
    [...line.matchAll(SPECIFIER)].filter((m) => barrelViolation(m[1], "journal", file)).length;

  assert.equal(read('import "@/features/planning/lib/board-order";'), 1, "side-effect import passed unnoticed");
  assert.equal(read('const load = () => import("@/features/planning/lib/board-order");'), 1, "dynamic import passed unnoticed");
  assert.equal(read('const m = require("@/features/planning/lib/board-order");'), 1, "require passed unnoticed");
  assert.equal(read('export { Board } from "@/features/planning/lib/feature-board";'), 1, "re-export still an offender");

  assert.equal(read('import { Board } from "@/features/planning";'), 0, "the barrel is a legal door");
  assert.equal(read('import "@/shared/ui/modal";'), 0, "shared sits below features, not beside them");
  assert.equal(read('const load = () => import("@/shared/ui/modal");'), 0, "dynamic shared import is legal");
  assert.equal(read('import "./note-form";'), 0, "a feature reaches into its own internals freely");
  assert.equal(read('import { z } from "node:assert";'), 0, "a package specifier is not a feature edge");
});

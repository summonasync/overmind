import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createJiti } from "jiti";
import { FILTER_DETAIL_FRAMING, activeFilterNames } from "../src/features/journal/lib/graph-copy.ts";
import { EMPTY_GRAPH_FILTERS } from "../src/features/journal/lib/graph-filters.ts";
import {
  backLinkLabelFrom,
  noteHrefFromGraph,
  streamHrefFrom,
} from "../src/features/journal/lib/note-rails.ts";
import type {
  GraphNode,
  NotePromotionResult,
  PromotionForm,
} from "../src/features/journal/types.ts";

// `note-promotion.ts` reaches the board's cache keys through the planning barrel — the only legal
// door per `journal-stream.test.mts` — and the barrel is spelled with the `@/` alias, so the whole
// module comes in through jiti like every other aliased module in this suite.
const jiti = createJiti(import.meta.url, {
  jsx: { runtime: "automatic", importSource: "react" },
  alias: {
    "@/shared/assets/zerlings": new URL("./fixtures/stub-zerlings.ts", import.meta.url).href,
    "@": new URL("../src/", import.meta.url).href,
  },
});
const promotion = await jiti.import("../src/features/journal/lib/note-promotion.ts");
const {
  EMPTY_PROMOTION_FORM,
  featureCandidates,
  promotedCardHref,
  promotedLabel,
  promotionBlocker,
  promotionInvalidations,
  promotionRefusal,
  promotionRequest,
  promotionSubmitAllowed,
} = promotion as {
  EMPTY_PROMOTION_FORM: PromotionForm;
  featureCandidates: (
    nodes: readonly GraphNode[],
    projectId: string,
  ) => { id: string; label: string; done: boolean }[];
  promotedCardHref: (boardProjectId: string, featureSlug: string) => string;
  promotedLabel: (result: NotePromotionResult) => string;
  promotionBlocker: (form: PromotionForm) => string | null;
  promotionInvalidations: (
    result: NotePromotionResult,
    view: { boardProjectId: string; graphFilters?: unknown },
  ) => { queryKey: readonly unknown[]; reason: string }[];
  promotionRefusal: (error: unknown) => PromotionRefusalShape | null;
  promotionRequest: (form: PromotionForm) => Record<string, unknown> | null;
  promotionSubmitAllowed: (form: PromotionForm, busy: boolean) => boolean;
};

interface PromotionRefusalShape {
  code: string;
  headline: string;
  detail: string;
  boardSlug: string | null;
}

const { graphKeys, journalKeys } = (await jiti.import(
  "../src/features/journal/queries.ts",
)) as unknown as {
  graphKeys: { all: readonly unknown[]; field: (f: unknown) => readonly unknown[] };
  journalKeys: { detail: (id: string) => readonly unknown[]; lists: () => readonly unknown[] };
};
const { planningKeys } = (await jiti.import("../src/features/planning/queries.ts")) as unknown as {
  planningKeys: {
    board: (projectId: string) => readonly unknown[];
    feature: (projectId: string, slug: string) => readonly unknown[];
  };
};

const sliceFile = (path: string) =>
  readFileSync(new URL(`../src/features/journal/${path}`, import.meta.url), "utf8");

// The files this task shipped, named once so every guard says what it measured — the ledger style
// `journal-note-view.test.mts` keeps for T8 and `journal-graph.test.mts` for T6.
const T7_SLICE = [
  "components/promote-note.tsx",
  "lib/note-promotion.ts",
  "lib/use-promote-note.ts",
] as const;

const { PromoteNote } = await jiti.import(
  "../src/features/journal/components/promote-note.tsx",
);
const { invalidatePromotionCaches } = (await jiti.import(
  "../src/features/journal/lib/use-promote-note.ts",
)) as unknown as {
  invalidatePromotionCaches: (
    client: unknown,
    result: NotePromotionResult,
    view: { boardProjectId: string; graphFilters?: unknown },
  ) => Promise<void>;
};

const NOTE_ID = "7c1f6a10-0000-4000-8000-000000000001";
const BOARD_ID = "7c1f6a10-0000-4000-8000-000000000002";
const FEATURE_ID = "7c1f6a10-0000-4000-8000-000000000003";
const TASK_ID = "7c1f6a10-0000-4000-8000-000000000004";
const OTHER_PROJECT = "7c1f6a10-0000-4000-8000-000000000005";

const readyForm: PromotionForm = {
  target: "feature",
  projectId: BOARD_ID,
  featureId: "",
  name: "A card worth reading later",
  body: "What the card is for, written at the act.",
};

function featurePromotion(overrides: Partial<NotePromotionResult> = {}): NotePromotionResult {
  return {
    entryId: NOTE_ID,
    targetKind: "feature",
    targetId: FEATURE_ID,
    featureId: FEATURE_ID,
    featureSlug: "042-promoted-thought",
    ...overrides,
  };
}

const view: { boardProjectId: string } = { boardProjectId: BOARD_ID };

// ── The door's body, built from the form and nothing else ─────────────────────────────

test("a feature promotion sends the board, the title and the card body", () => {
  const request = promotionRequest(readyForm);
  assert.deepEqual(request, {
    target: "feature",
    projectId: BOARD_ID,
    title: "A card worth reading later",
    body: "What the card is for, written at the act.",
  });
});

test("a task promotion names the feature and carries the brief on one line", () => {
  const request = promotionRequest({
    ...readyForm,
    target: "task",
    featureId: FEATURE_ID,
    name: "  From the journal pile  ",
  });
  assert.deepEqual(request, {
    target: "task",
    featureId: FEATURE_ID,
    body: "What the card is for, written at the act.",
    brief: "From the journal pile",
  });
});

test("a blocked form never produces a request, so cancel cannot reach the door", () => {
  // The anti-pattern this pins is the pre-save: a surface that writes a draft to "attach" the
  // link later. `null` here means the component has nothing to send at all.
  assert.equal(promotionRequest(EMPTY_PROMOTION_FORM), null);
  assert.equal(promotionRequest({ ...readyForm, name: "   " }), null);
  assert.match(promotionBlocker({ ...readyForm, body: "" }) ?? "", /card/);
  assert.match(promotionBlocker({ ...readyForm, projectId: "" }) ?? "", /board/);
  assert.match(
    promotionBlocker({ ...readyForm, target: "task", featureId: "" }) ?? "",
    /feature/,
  );
  // The board cap on a title, and the door's own `Brief` rule, both answered before the wire.
  assert.match(promotionBlocker({ ...readyForm, name: "x".repeat(501) }) ?? "", /500/);
});

test("the api layer posts to the two human-door verbs and writes no note", () => {
  const api = sliceFile("api.ts");
  // The verbs are read off the door itself, not restated here: `JournalTriageApi` registers the
  // two routes, and the client builds the suffix from its own target union.
  const door = readFileSync(
    new URL("../../src/Overmind.Host/Api/JournalTriageApi.cs", import.meta.url),
    "utf8",
  );
  const suffixes = [...door.matchAll(/\/entries\/\{id:guid\}\/(promote-to-[a-z]+)/g)].map(
    (match) => match[1]!.replace("promote-to-", ""),
  );
  assert.deepEqual(suffixes, ["feature", "task"], "the door grew or renamed a verb this pin missed");
  assert.match(api, /promote-to-\$\{request\.target\}/, "the client stopped building the door's path");

  // The promotion body carries no note text: spec R6's rule that the note is never the artifact.
  const promoteBody = api.slice(api.indexOf("export function promoteNote"));
  assert.ok(promoteBody.length > 100, "the promote function is gone");
  assert.equal(
    /content:/.test(promoteBody),
    false,
    "the promotion started shipping the note's body onto the wire",
  );
  assert.equal(
    /apiPut|apiDelete/.test(promoteBody),
    false,
    "the promotion path reached for a note write — the door writes no note",
  );
});

test("the request the client builds is the body the door validates", async () => {
  // A real run through the slice's api layer and the shared client: the assertion is about the
  // bytes on the wire, not about a string in a source file. Both arms, because they are two doors
  // with two bodies and a test that walks one of them measures half the act.
  const calls: { url: string; method: string; body: string }[] = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: unknown, init?: RequestInit) => {
    calls.push({
      url: String(input),
      method: String(init?.method ?? "GET"),
      body: String(init?.body ?? ""),
    });
    return new Response(
      JSON.stringify({
        entryId: NOTE_ID,
        targetKind: JSON.parse(String(init?.body ?? "{}")).brief === undefined ? "feature" : "task",
        targetId: FEATURE_ID,
        featureId: FEATURE_ID,
        featureSlug: "042-promoted-thought",
      }),
      { status: 200, headers: { "content-type": "application/json" } },
    );
  }) as typeof fetch;

  try {
    const { promoteNote } = (await jiti.import("../src/features/journal/api.ts")) as unknown as {
      promoteNote: (id: string, request: unknown) => Promise<NotePromotionResult>;
    };

    const feature = await promoteNote(NOTE_ID, promotionRequest(readyForm));
    assert.equal(calls.length, 1, `the feature arm made ${calls.length} requests, expected one`);
    assert.equal(calls[0]!.url, `/api/entries/${NOTE_ID}/promote-to-feature`);
    assert.equal(calls[0]!.method, "POST");
    assert.deepEqual(JSON.parse(calls[0]!.body), {
      projectId: BOARD_ID,
      title: "A card worth reading later",
      body: "What the card is for, written at the act.",
    });
    assert.equal(feature.targetKind, "feature");
    assert.equal(feature.featureSlug, "042-promoted-thought");

    const task = await promoteNote(
      NOTE_ID,
      promotionRequest({ ...readyForm, target: "task", featureId: FEATURE_ID, name: " Brief " }),
    );
    assert.equal(calls.length, 2, `the task arm made ${calls.length - 1} requests, expected one`);
    assert.equal(calls[1]!.url, `/api/entries/${NOTE_ID}/promote-to-task`);
    assert.deepEqual(JSON.parse(calls[1]!.body), {
      featureId: FEATURE_ID,
      body: "What the card is for, written at the act.",
      brief: "Brief",
    });
    assert.equal(task.targetKind, "task");

    // Neither arm ever carries the note's own text.
    for (const call of calls) {
      assert.equal(/content/.test(call.body), false, "the promotion shipped note text on the wire");
    }
  } finally {
    globalThis.fetch = originalFetch;
  }
});

// ── Exactly the caches the response names ─────────────────────────────────────────────

test("invalidation is the response's own answer, and never a blanket", () => {
  const keys = promotionInvalidations(featurePromotion(), view).map((entry) => entry.queryKey);

  assert.deepEqual(keys, [
    journalKeys.detail(NOTE_ID),
    journalKeys.lists(),
    planningKeys.board(BOARD_ID),
    planningKeys.feature(BOARD_ID, "042-promoted-thought"),
    graphKeys.all,
  ]);

  // A blanket would be the whole journal family or the whole planning family: neither appears.
  for (const key of keys) {
    assert.ok(key.length > 0);
    assert.deepEqual(
      key,
      key.filter((part) => part !== undefined),
      "an undefined segment means an unscoped prefix",
    );
    const blanket =
      (key.length === 1 && key[0] === "journal") || (key.length === 1 && key[0] === "planning");
    assert.equal(blanket, false, `${JSON.stringify(key)} stales a whole feature family`);
  }
});

test("a task promotion stales the task artifact's own cache too", () => {
  const entries = promotionInvalidations(
    featurePromotion({ targetKind: "task", targetId: TASK_ID }),
    view,
  );
  const keys = entries.map((entry) => entry.queryKey);

  // The rail points at the task; the board card to invalidate is still its parent.
  assert.ok(keys.some((key) => key[0] === "journal" && key[1] === "detail" && key[2] === NOTE_ID));
  assert.ok(
    keys.some(
      (key) =>
        key[0] === "planning" &&
        key[1] === "detail" &&
        key[2] === BOARD_ID &&
        key[3] === "042-promoted-thought",
    ),
    "the parent card the response names was left fresh",
  );
});

test("a mounted graph field narrows the invalidation to the field being drawn", () => {
  const filters = { ...EMPTY_GRAPH_FILTERS, includeDone: true, project: BOARD_ID };
  const keys = promotionInvalidations(featurePromotion(), {
    ...view,
    graphFilters: filters,
  }).map((entry) => entry.queryKey);

  const graphKey = keys.at(-1);
  assert.deepEqual(graphKey, graphKeys.field(filters), "the panel staled fields it is not showing");
});

test("invalidatePromotionCaches settles every cache before it resolves", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const settled: string[] = [];

  // A mounted query whose refetch is slow: if the helper resolves first, the busy state lied.
  client.setQueryData(journalKeys.detail(NOTE_ID), { stale: true });
  const originalInvalidate = client.invalidateQueries.bind(client);
  const seenKeys: string[] = [];
  (client as { invalidateQueries: (o: unknown) => Promise<void> }).invalidateQueries = (
    options,
  ) => {
    const key = JSON.stringify((options as { queryKey: readonly unknown[] }).queryKey);
    seenKeys.push(key);
    return new Promise<void>((resolve) => {
      setTimeout(() => {
        settled.push(key);
        resolve();
      }, 20);
    });
  };

  const pending = invalidatePromotionCaches(client, featurePromotion(), view);
  assert.equal(settled.length, 0, "the settle step completed before any cache answered");
  await pending;

  assert.equal(seenKeys.length, settled.length, "a cache was counted without being awaited");
  assert.equal(seenKeys.length, 5, `staled ${seenKeys.length} caches, expected 5`);
  assert.ok(originalInvalidate);
});

// ── The single-click gate (review-237-04 finding 2) ───────────────────────────────────

test("the gate closes while a promotion is in flight, so the door cannot be clicked twice", () => {
  // The disposition recorded in T5's log accepts the door's TOCTOU window for one loopback writer
  // on the basis that the client makes the second click impossible. This is that basis: the same
  // complete form, busy flipped, changes the answer.
  assert.equal(promotionSubmitAllowed(readyForm, false), true);
  assert.equal(promotionSubmitAllowed(readyForm, true), false, "a second click could reach the door");
  assert.equal(promotionSubmitAllowed(EMPTY_PROMOTION_FORM, false), false);
});

test("the promote control wears the busy state on every control in the form", () => {
  const component = sliceFile("components/promote-note.tsx");
  assert.match(component, /<fieldset className="space-y-2" disabled=\{busy\}>/);
  assert.match(component, /disabled=\{busy \|\| blocker !== null\}/, "confirm ignores the busy state");
  assert.match(
    component,
    /<Button size=\{size\} variant="secondary" disabled=\{busy\} onClick=\{\(\) => setOpen\(true\)\}>/,
    "the trigger reopens mid-request",
  );
  assert.match(component, /\{busy \? "Sorting…" : promotionConfirmLabel/, "the label never says it is working");
  // The mutation's own guard, behind the disabled attribute.
  const hook = sliceFile("lib/use-promote-note.ts");
  assert.match(hook, /if \(mutation\.isPending\) \{\s*\n\s*return;/);
});

// ── The refusals, in the door's words ────────────────────────────────────────────────

// Bodies copied verbatim out of the built pipeline, not paraphrased:
// ~/scratch-237/t7-refusal-probe.log (JournalTriagePromotionApiTests-era door, tip 1df1ee46).
const ALREADY_FEATURE = {
  code: "TriageAlreadyPromoted",
  message:
    "Entry '46dbe0ec-da7a-48fb-8180-73f177d89251' already carries a promotion edge to feature " +
    "'001-probed-card' ('17e44b81-494b-4b97-9542-8c122ceeb35b'). The promotion edge is the triage " +
    "state, so one note is sorted by one board artifact: open the artifact it became and continue " +
    "the thought there, or remove that edge first if the note really is a second, separate piece " +
    "of work.",
};

const ALREADY_TASK = {
  code: "TriageAlreadyPromoted",
  message:
    "Entry '3f5d7894-84d4-45ae-8f6f-45b313f6e625' already carries a promotion edge to task " +
    "'099-probe' task 00 ('dc2368de-015a-4aef-8c50-52f6635448aa'). The promotion edge is the triage state.",
};

const NOT_PROMOTABLE = {
  code: "TriageNoteNotPromotable",
  message:
    "Entry '6bc3c26b-ca80-4bb9-b770-e96acedc3657' is in state 'lifted' and cannot be promoted. " +
    "Only a live note leaves a trace on the board.",
};

const MISSING_ENTRY = {
  code: "MissingEntry",
  message: "Entry 'e3b8b2bb-b66e-4d4a-ba39-a4b616f4a7e1' was not found in this project.",
};

const MISSING_FEATURE = {
  code: "MissingFeature",
  message: "Feature '47bca33b-0de0-4420-8013-a51a39296834' was not found.",
};

function apiErrorLike(shape: { code: string; message: string }): Error & { code: string } {
  const error = new Error(shape.message) as Error & { code: string };
  error.name = "ApiError";
  error.code = shape.code;
  return error;
}

test("TriageAlreadyPromoted is read as provenance, naming the feature card", () => {
  const refusal = promotionRefusal(apiErrorLike(ALREADY_FEATURE));
  assert.ok(refusal, "the door's own refusal code went unrecognised");
  assert.equal(refusal.code, "TriageAlreadyPromoted");
  assert.equal(refusal.headline, "Already sorted into 001-probed-card");
  assert.equal(refusal.boardSlug, "001-probed-card");
  // The server sentence survives untouched: it is the evidence the owner can check against the door.
  assert.equal(refusal.detail, ALREADY_FEATURE.message);
});

test("TriageAlreadyPromoted names a task with its order and parent", () => {
  const refusal = promotionRefusal(apiErrorLike(ALREADY_TASK));
  assert.ok(refusal);
  assert.equal(refusal.headline, "Already sorted into task 00 on 099-probe");
  assert.equal(refusal.boardSlug, "099-probe", "the board link must land on the parent, not on air");
});

test("TriageNoteNotPromotable says the note is not live, with its state", () => {
  const refusal = promotionRefusal(apiErrorLike(NOT_PROMOTABLE));
  assert.ok(refusal);
  assert.equal(refusal.code, "TriageNoteNotPromotable");
  assert.match(refusal.headline, /lifted/);
  assert.equal(refusal.boardSlug, null, "a buried note names no card to open");
});

test("the two 404s are named separately, and neither is a generic failure", () => {
  const entry = promotionRefusal(apiErrorLike(MISSING_ENTRY));
  const feature = promotionRefusal(apiErrorLike(MISSING_FEATURE));
  assert.ok(entry && feature);
  assert.equal(entry.code, "MissingEntry");
  assert.match(entry.headline, /note/);
  assert.equal(feature.code, "MissingFeature");
  assert.match(feature.headline, /feature/);
});

test("an unknown error is left to the ordinary banner", () => {
  assert.equal(promotionRefusal(apiErrorLike({ code: "SomeNewCode", message: "boom" })), null);
  assert.equal(promotionRefusal(new Error("Network request failed")), null);
  assert.equal(promotionRefusal(null), null);
});

test("the success line names the artifact the response identified", () => {
  assert.equal(promotedLabel(featurePromotion()), "Sorted into 042-promoted-thought");
  assert.equal(
    promotedLabel(featurePromotion({ targetKind: "task", targetId: TASK_ID })),
    "Sorted into a task on 042-promoted-thought",
  );
  assert.equal(
    promotedCardHref(BOARD_ID, "042-promoted-thought"),
    `/dashboard?project=${BOARD_ID}&feature=042-promoted-thought`,
  );
});

// ── The task arm's candidates ─────────────────────────────────────────────────────────

function featureNode(id: string, label: string, status: GraphNode["featureStatus"], projectId = BOARD_ID): GraphNode {
  return {
    kind: "feature",
    id,
    label,
    projectId,
    entryType: null,
    featureStatus: status,
    orphan: false,
  };
}

test("the task arm lists features by guid, because the board wire carries none", () => {
  const candidates = featureCandidates(
    [
      featureNode(TASK_ID, "Zebra card", "draft"),
      featureNode(FEATURE_ID, "Alpha card", "done"),
      { ...featureNode(OTHER_PROJECT, "Elsewhere", "ready"), projectId: OTHER_PROJECT },
      {
        kind: "entry",
        id: NOTE_ID,
        label: "Not a feature",
        projectId: BOARD_ID,
        entryType: "journal",
        featureStatus: null,
        orphan: true,
      },
    ],
    BOARD_ID,
  );

  // Open work first; Done is widened but parked at the end of the list.
  assert.deepEqual(candidates.map((c) => c.label), ["Zebra card", "Alpha card"]);
  assert.deepEqual(candidates.map((c) => c.done), [false, true]);
  assert.equal(candidates[1]!.id, FEATURE_ID, "the candidate must carry the guid the door asks for");
});

// ── The graph filter detail: an active-filter superset, never a culprit list ──────────

test("the filter detail is framed as active filters, not as the culprit", () => {
  assert.match(FILTER_DETAIL_FRAMING, /Widening one of them may bring the node back/);
  // The two phrasings review-237-05 M1 forbids: singling one filter out, or past-tense blaming.
  assert.doesNotMatch(FILTER_DETAIL_FRAMING, /because you (set|picked|filtered)/i);
  assert.doesNotMatch(FILTER_DETAIL_FRAMING, /\b(this|that|the) filter (hid|excluded|removed)\b/i);
});

test("the server sentence is rendered as it arrived, with no rewriting on the surface", () => {
  const page = readFileSync(
    new URL("../src/pages/journal-graph-page/ui/journal-graph-page.tsx", import.meta.url),
    "utf8",
  );
  assert.match(page, /<ErrorText error=\{error\} \/>/, "the server detail is no longer printed verbatim");
  assert.match(page, /\{FILTER_DETAIL_FRAMING\}/, "the framing line is missing from the refusal branch");
  assert.equal(
    /error\.message\.(replace|split|slice|replaceAll)/.test(page),
    false,
    "the surface started paraphrasing the door's sentence",
  );
});

test("the active-filter list reads from the field's own filters", () => {
  // The default field is narrowed by exactly one thing: Done features stay hidden until widened.
  assert.deepEqual(activeFilterNames(EMPTY_GRAPH_FILTERS), ["Done features hidden"]);
  assert.deepEqual(
    activeFilterNames({
      ...EMPTY_GRAPH_FILTERS,
      types: ["idea"],
      project: BOARD_ID,
      search: "Учет",
      orphansOnly: true,
    }),
    [
      "entry types",
      "project",
      "title search",
      "unlinked only",
      "Done features hidden",
    ],
  );
  // A focus window narrows the field too, and it is the filter the refusal was asked under.
  assert.deepEqual(
    activeFilterNames({ ...EMPTY_GRAPH_FILTERS, includeDone: true, focus: `entry:${NOTE_ID}` }),
    ["focus window"],
  );
  // Widening Done is a widening, so it leaves the narrowing list.
  assert.deepEqual(
    activeFilterNames({ ...EMPTY_GRAPH_FILTERS, includeDone: true }),
    [],
    "a widened filter was reported as active narrowing",
  );
});

test("no graph or triage string blames a single filter for a missing node", () => {
  const forbidden = /\b(the filter that|this filter (hid|dropped|removed)|excluded by|hidden by the filter)\b/i;
  const offenders: string[] = [];
  for (const file of [
    "lib/graph-copy.ts",
    "lib/graph-filters.ts",
    "components/graph-filters.tsx",
    "components/graph-legend.tsx",
    "components/graph-node-list.tsx",
    "components/graph-node-panel.tsx",
    "lib/note-promotion.ts",
    "components/promote-note.tsx",
  ]) {
    const source = file.startsWith("lib/") || file.startsWith("components/")
      ? sliceFile(file)
      : "";
    if (source !== "" && forbidden.test(source)) {
      offenders.push(file);
    }
  }
  const page = readFileSync(
    new URL("../src/pages/journal-graph-page/ui/journal-graph-page.tsx", import.meta.url),
    "utf8",
  );
  if (forbidden.test(page)) {
    offenders.push("pages/journal-graph-page");
  }
  assert.deepEqual(offenders, [], "culprit-only wording reached the surface");
});

// ── The back-link label follows the `from` parameter ──────────────────────────────────

test("a graph arrival reads as a return to the graph map, filters intact", () => {
  // T6 landed the href half and its tests; the label is this task's row.
  const href = noteHrefFromGraph(NOTE_ID, `type=idea&orphansOnly=true&note=stale`);
  assert.match(href, /from=journal-graph/);
  assert.match(href, /gq=type%3Didea%26orphansOnly%3Dtrue/);

  const search = new URLSearchParams(href.split("?")[1] ?? "").toString();
  assert.equal(backLinkLabelFrom(search), "Back to graph map");
  assert.equal(
    streamHrefFrom(search),
    "/journal/graph?type=idea&orphansOnly=true",
    "the round trip lost a filter",
  );
});

test("a stream arrival keeps today's wording", () => {
  assert.equal(backLinkLabelFrom(`note=${NOTE_ID}&day=2026-11-21`), "Journal");
  assert.equal(backLinkLabelFrom(""), "Journal");
  // A `from` that is not the graph's own changes nothing — the param is not an open redirect.
  assert.equal(backLinkLabelFrom(`note=${NOTE_ID}&from=elsewhere`), "Journal");
});

test("the note view reads both halves of the return from the same parameter", () => {
  const noteView = sliceFile("components/note-view.tsx");
  assert.match(noteView, /from "@\/features\/journal\/lib\/note-rails"/);
  const labels = noteView.match(/label=\{backLinkLabelFrom\(search\)\}/g) ?? [];
  assert.equal(labels.length, 2, `only ${labels.length} back links follow the from parameter`);
  assert.equal(
    /label="Journal"/.test(noteView),
    false,
    "a hard-coded stream label survived the round-trip pass",
  );
});

// ── The affordance in both surfaces ──────────────────────────────────────────────────

test("the promote affordance is mounted from the note view and the node panel", () => {
  assert.match(sliceFile("components/note-view.tsx"), /<PromoteNote entryId=\{note\.id\} noteTitle=\{note\.title\} \/>/);
  const panel = sliceFile("components/graph-node-panel.tsx");
  assert.match(panel, /<PromoteNote entryId=\{id\} noteTitle=\{data\.title\} graphFilters=\{filters\} \/>/);
  // R6's door is for notes. A project or feature node must not grow one.
  assert.match(panel, /node\.kind === "entry"[\s\S]{0,200}NoteContext/);
});

test("the open form asks for a target, a name and a body, and offers cancel", () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const html = renderToStaticMarkup(
    React.createElement(
      QueryClientProvider,
      { client },
      React.createElement(PromoteNote, {
        entryId: NOTE_ID,
        noteTitle: "Worth a card",
      }),
    ),
  );
  // Closed by default: the affordance is one button, so the rails stay the note's own.
  assert.match(html, /Sort into board work/);
  assert.doesNotMatch(html, /<form/, "the form rendered before it was asked for");
});

test("the mounted form carries the target choice and the board pick", () => {
  const component = sliceFile("components/promote-note.tsx");
  for (const needle of [
    'aria-pressed={form.target === target}',
    "promotionTargetLabel(target)",
    'label="Board"',
    'label="Existing feature"',
    "promotionNameLabel(form.target)",
    "Cancel",
  ]) {
    assert.ok(component.includes(needle), `the affordance lost "${needle}"`);
  }
  // No direct fetch from a client component: everything goes through the slice's api layer.
  assert.equal(/\bfetch\(/.test(component), false, "the component reached for fetch");
  assert.match(component, /from "@\/features\/journal\/lib\/use-promote-note"/);
  const hook = sliceFile("lib/use-promote-note.ts");
  assert.match(hook, /from "@\/features\/journal\/api"/);
  assert.equal(/\bfetch\(/.test(hook), false, "the hook reached for fetch");
});

test("a refusal renders as a named-card status, not as the error banner", () => {
  const component = sliceFile("components/promote-note.tsx");
  const refusalBlock = component.slice(
    component.indexOf("{refusal !== null ?"),
    component.indexOf(") : promoted !== null ?"),
  );
  assert.ok(refusalBlock.length > 40, "the refusal branch is gone");
  assert.match(refusalBlock, /role="status"/, "a refusal is announced like a failure");
  assert.match(refusalBlock, /\{refusal\.headline\}/);
  assert.match(refusalBlock, /\{refusal\.detail\}/, "the door's own sentence is not on screen");
  assert.match(refusalBlock, /promotedCardHref\(form\.projectId, refusal\.boardSlug\)/);
  // The banner only ever gets what the refusal mapper did not claim.
  assert.match(component, /<ErrorText error=\{promotion\.error\} \/>/);
});

test("cancel performs no request at all — there is no draft to abandon server-side", () => {
  // The door is only ever called from the confirm path, so "cancel leaves nothing behind" is a
  // property of this surface, not a rollback asked of the Host: no edge, no card, no draft entry
  // exists because no second request was ever made.
  const component = sliceFile("components/promote-note.tsx");
  const closeBody = component.slice(component.indexOf("const close = () => {"), component.indexOf("const patch ="));
  assert.ok(closeBody.length > 40, "the cancel handler is gone");
  for (const forbidden of ["submit", "mutate", "promoteNote"]) {
    assert.equal(
      closeBody.includes(forbidden),
      false,
      `cancel reached for "${forbidden}" — the act must belong to Confirm alone`,
    );
  }
  // Confirm is the single caller of the mutation, and it is a form submit, not a field change.
  const submits = component.match(/promotion\.submit\(/g) ?? [];
  assert.equal(submits.length, 1, `${submits.length} call sites can start a promotion`);
  assert.match(component, /const confirm = \(\) => \{[\s\S]{0,160}promotion\.submit\(/);
});

test("the note's own text is not an input to the act", () => {
  // The door writes no note, and neither may this surface: no update, no lift, no append anywhere
  // on the promotion path. Measured against the pipeline as well — see the report's probe log, the
  // note's content and currentVersion are byte-identical after both arms.
  const component = sliceFile("components/promote-note.tsx");
  for (const forbidden of ["updateJournalNote", "deleteJournalNote", "useJournalMutations"]) {
    assert.equal(
      component.includes(forbidden),
      false,
      `${forbidden} entered the promotion path — the note has to stay exactly as it is`,
    );
  }
  const hook = sliceFile("lib/use-promote-note.ts");
  assert.equal(hook.includes("updateJournalNote"), false, "the mutation path started writing the note");
});

test("the rails answer the promotion edge is NOT on the detail wire — the act answers it", () => {
  // Measured 2026-11-21 against the real pipeline: after a 200 promotion the note's `outgoing`
  // array is still empty (`~/scratch-237/t7-client-body-through-door.log`, `outgoing=0` on both
  // arms). `EntryLinkProjection` builds that array from `WikiLinkParser.Parse(entry.Content)`, so
  // the manual edge the door writes cannot reach it without a Host change — outside this task's
  // anti-scope. The consequence the component has to live with: the rails show the promoted card
  // only once the note itself names it, so the success answer is rendered from the response.
  const projection = readFileSync(
    new URL("../../src/Overmind.Host/Api/EntryLinkProjection.cs", import.meta.url),
    "utf8",
  );
  assert.match(
    projection,
    /WikiLinkParser\.Parse\(entry\.Content\)/,
    "the projection grew another outgoing source — re-check whether the rails now carry the promotion edge",
  );
  assert.equal(
    /Origin\s*==\s*EntryLinkOrigin\.Manual/.test(projection),
    false,
    "manual edges now reach the rails; the promoted answer can come from the payload instead",
  );

  // And the component's success line reads the response, never a synthesised rail row.
  const component = sliceFile("components/promote-note.tsx");
  assert.match(component, /\{promotedLabel\(promoted\)\}/);
  assert.match(component, /promotedCardHref\(form\.projectId, promoted\.featureSlug\)/);
  const hook = sliceFile("lib/use-promote-note.ts");
  assert.equal(
    /setQueryData/.test(hook),
    false,
    "the promotion wrote a rail row into the cache — the edge is the record, not the cache",
  );
});

test("no storage and no hex in the files this task shipped", () => {
  const offenders: string[] = [];
  for (const file of T7_SLICE) {
    const source = sliceFile(file);
    assert.ok(source.length > 200, `${file} is missing, empty or wrong`);
    source
      .split("\n")
      .forEach((line, i) => {
        if (/#[0-9a-fA-F]{3,8}\b/.test(line)) {
          offenders.push(`${file}:${i + 1}`);
        }
      });
    for (const forbidden of ["localStorage", "sessionStorage"]) {
      if (source.includes(forbidden)) {
        offenders.push(`${file}: ${forbidden}`);
      }
    }
  }
  assert.deepEqual(offenders, []);
  // The barrel still does not carry the graph island; this task touched neither door. Comments are
  // stripped the way `journal-graph.test.mts` strips them — the file names the ban in prose.
  const barrel = sliceFile("index.ts")
    .split("\n")
    .filter((line) => !line.trimStart().startsWith("//"))
    .join("\n");
  assert.equal(barrel.includes("graph-canvas"), false);
});

test("the promotion slice file names are the ones the guards above measure", () => {
  assert.equal(T7_SLICE.length, 3);
});

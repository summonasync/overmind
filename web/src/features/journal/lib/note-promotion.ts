import type { GraphViewFilters } from "@/features/journal/types";

// Triage is one act with two answers: the door mints an artifact plus the manual edge that is the
// record of the act, and the surface has to (a) name what the note became and (b) stale exactly the
// caches that answer changed. Everything below is the arithmetic of that act with no React and no
// network in it, so the invalidation set and the refusal copy are measurable in `node --test`
// instead of by eye in a browser.
//
// The target vocabulary is the rail's, not a fifth union: the door answers `EntryLinkKind` through
// the Host's camelCase enum converter, and the two promotion targets are `feature` and `task` — the
// same two arms `note-rails.ts` already groups. `PromotionTargetKind` in types.ts is the narrow
// slice of `JournalLinkKind` a promotion can mint; `entry` and `project` are what a marker writes.

import { graphKeys, journalKeys } from "@/features/journal/queries";
import { planningKeys } from "@/features/planning";
import type {
  FeatureCandidate,
  GraphNode,
  NotePromotionResult,
  PromotionForm,
  PromotionTargetKind,
} from "@/features/journal/types";

/** One mounted surface's view of the cache: which note, which graph field it is drawing. */
export interface PromotionCacheView {
  /** The board the card landed on. The response names the card by slug; the project is what the
   *  form the owner just filled in knows and the response does not carry. */
  boardProjectId: string;
  /** The graph field mounted under this surface, when there is one. Without it the whole graph
   *  family is the honest scope: the edge was added to the field, not to one filter answer. */
  graphFilters?: GraphViewFilters;
}

export const EMPTY_PROMOTION_FORM: PromotionForm = {
  target: "feature",
  projectId: "",
  featureId: "",
  name: "",
  body: "",
};

// Both doors cap the text they accept: `Title` at 500 on the feature arm, `Brief` at
// `PlanningArtifact.BriefMaxLength` (500, single line) on the task arm. Refusing locally costs
// nothing; a round trip to a 400 teaches the owner nothing the field cannot say.
export const PROMOTION_NAME_MAX = 500;

export function promotionTargetLabel(target: PromotionTargetKind): string {
  return target === "feature" ? "New feature" : "Task on an existing feature";
}

export function promotionNameLabel(target: PromotionTargetKind): string {
  return target === "feature" ? "Feature title" : "Task brief";
}

export function promotionConfirmLabel(target: PromotionTargetKind): string {
  return target === "feature" ? "Promote to a feature" : "Promote to a task";
}

// The blockers read as instructions, not as fault: the owner is mid-thought and the form is the
// one thing standing between the thought and the board.
export function promotionBlocker(form: PromotionForm): string | null {
  if (form.projectId.length === 0) {
    return "Pick the board the work lands on.";
  }

  if (form.target === "task" && form.featureId.length === 0) {
    return "Pick the feature the task hangs under.";
  }

  const name = form.name.trim();
  if (name.length === 0) {
    return form.target === "feature" ? "Name the feature." : "Write the task brief.";
  }
  if (name.length > PROMOTION_NAME_MAX) {
    return `Keep it to ${PROMOTION_NAME_MAX} characters — the board column is narrow.`;
  }

  // Both doors pin Body NotEmpty (`PromoteNoteToFeatureRequestValidator`, `…ToTaskRequest…`), and
  // the note itself is never the card's text — spec R6 — so the card has to be written, not derived.
  if (form.body.trim().length === 0) {
    return "Write what the card is for. The note keeps its own text.";
  }

  return null;
}

/**
 * The single-submission gate. `isPending` covers the POST **and** the awaited invalidation, so a
 * second click inside that window is the thing that used to be able to reach the door twice:
 * review-237-04 finding 2 recorded the repeat check running before the promotion transaction, and
 * its disposition (T5's log, AC 11) accepts the window for a single loopback writer precisely
 * because the client makes the second click impossible. This function is that basis.
 */
export function promotionSubmitAllowed(form: PromotionForm, busy: boolean): boolean {
  return !busy && promotionBlocker(form) === null;
}

export interface PromoteToFeatureRequest {
  target: "feature";
  projectId: string;
  title: string;
  body: string;
}

export interface PromoteToTaskRequest {
  target: "task";
  featureId: string;
  body: string;
  brief: string;
}

export type PromotionRequest = PromoteToFeatureRequest | PromoteToTaskRequest;

/**
 * The form state onto the door's body. Trimming happens here, once, so the wire never carries
 * whitespace the owner did not mean to send and the panel can show the text it submitted.
 */
export function promotionRequest(form: PromotionForm): PromotionRequest | null {
  if (promotionBlocker(form) !== null) {
    return null;
  }

  const name = form.name.trim();
  const body = form.body.trim();

  return form.target === "feature"
    ? { target: "feature", projectId: form.projectId, title: name, body }
    : { target: "task", featureId: form.featureId, body, brief: name };
}

export interface CacheInvalidation {
  queryKey: readonly unknown[];
  /** Why this cache went stale — the reason is the reviewable part, not the key. */
  reason: string;
}

/**
 * Exactly the caches the response says changed, and nothing wider.
 *
 * The response names the entry (`entryId`), the artifact the provenance edge points at
 * (`targetKind`/`targetId`) and the card to invalidate (`featureId`/`featureSlug`). The board's own
 * query keys address a card by project + slug — measured: `PlanningFeatureBoardItem` carries no id
 * on the wire — so the slug comes from the response and the project from the form the owner just
 * filled in. Two halves of the same answer, not a guess at a cache layout.
 */
export function promotionInvalidations(
  result: NotePromotionResult,
  view: PromotionCacheView,
): CacheInvalidation[] {
  const invalidations: CacheInvalidation[] = [
    {
      queryKey: journalKeys.detail(result.entryId),
      reason: "the note's own rails are the read this act changed",
    },
    {
      queryKey: journalKeys.lists(),
      reason: "the note left the unsorted pile — the accent and the linked cohort answer off the edge",
    },
    {
      queryKey: planningKeys.board(view.boardProjectId),
      reason: `the board the card landed on (${view.boardProjectId}) has one more row`,
    },
    {
      queryKey: planningKeys.feature(view.boardProjectId, result.featureSlug),
      reason: `the card the response names (${result.featureSlug})`,
    },
    {
      queryKey: view.graphFilters ? graphKeys.field(view.graphFilters) : graphKeys.all,
      reason: "the graph field the edge was added to",
    },
  ];

  return invalidations;
}

export const PROMOTED_HEADLINE = "Sorted into";

/**
 * The board address of the card the response names. A task promotion names its **parent** feature
 * (`featureSlug`), and that is where the owner has to go to read the task — the artifact-level truth
 * is the edge, which is what the rails and the graph draw.
 */
export function promotedCardHref(boardProjectId: string, featureSlug: string): string {
  const params = new URLSearchParams({ project: boardProjectId, feature: featureSlug });
  return `/dashboard?${params.toString()}`;
}

/** What the success line says, from the response fields only. */
export function promotedLabel(result: NotePromotionResult): string {
  if (result.targetKind === "task") {
    return `${PROMOTED_HEADLINE} a task on ${result.featureSlug}`;
  }
  return `${PROMOTED_HEADLINE} ${result.featureSlug}`;
}

// ── The refusals, in the door's own words ────────────────────────────────────────────────
//
// Each of these is a `PlanningException` the generic mapper turns into problem+json: the refusal
// CODE rides in `title`, the sentence naming the card rides in `detail` (`PlanningErrors.Map` →
// `HttpErrors.Conflict/NotFound`). The strings below were read off the wire, not paraphrased —
// `~/scratch-237/t7-refusal-probe.log` holds the four bodies this mapper is written against.
//
// So the UI never invents a message and never shows the screaming banner for what is a fact about
// the note rather than a failure of the app.

export interface PromotionRefusal {
  /** The `PlanningFailureCode` this refusal is keyed on — the test names it, the copy never restates it. */
  code: string;
  headline: string;
  /** The server sentence, verbatim. It is the one text that names the card the note became. */
  detail: string;
  /** The board card the door named, when the sentence carried one. */
  boardSlug: string | null;
}

// `TriagePromotionService.TriageAlreadyPromoted` answers either
//   … promotion edge to feature '042-foo' ('<guid>').
// or, through `DescribeTargetAsync`,
//   … promotion edge to task '042-foo' task 03 ('<guid>').
// The quoted half is the card; a task adds its own order and still names the parent.
const EDGE_TARGET = /promotion edge to (feature|task) '([^']+)'(?: task (\d+))?/;
const NOTE_STATE = /is in state '([^']+)'/;

function alreadySortedHeadline(detail: string): string {
  const match = EDGE_TARGET.exec(detail);
  if (match === null) {
    // An unrecognised sentence means the door changed its wording, not that nothing is sorted: say
    // the flat truth and let the server text below carry whatever name it did give.
    return "Already sorted into board work";
  }

  const [, kind, label, order] = match;
  if (kind === "task") {
    return order === undefined
      ? `Already sorted into a task on ${label}`
      : `Already sorted into task ${order} on ${label}`;
  }
  return `Already sorted into ${label}`;
}

function namedCard(detail: string): string | null {
  return EDGE_TARGET.exec(detail)?.[2] ?? null;
}

/**
 * Maps a thrown error onto a refusal the owner can act on. Returns `null` for anything that is
 * genuinely a failure (a dead network, a 500, a validation problem) so the caller keeps the
 * ordinary error banner for those.
 */
export function promotionRefusal(error: unknown): PromotionRefusal | null {
  if (typeof error !== "object" || error === null) {
    return null;
  }

  const candidate = error as { code?: unknown; message?: unknown };
  const code = typeof candidate.code === "string" ? candidate.code : "";
  const detail = typeof candidate.message === "string" ? candidate.message : "";
  if (code.length === 0 || detail.length === 0) {
    return null;
  }

  switch (code) {
    case "TriageAlreadyPromoted":
      return { code, headline: alreadySortedHeadline(detail), detail, boardSlug: namedCard(detail) };

    case "TriageNoteNotPromotable": {
      const state = NOTE_STATE.exec(detail)?.[1];
      return {
        code,
        headline:
          state === undefined
            ? "This note is not a live note, so it cannot be sorted"
            : `This note is ${state}, not a live note, so it cannot be sorted`,
        detail,
        boardSlug: null,
      };
    }

    case "MissingEntry":
      return {
        code,
        headline: "That note is not on this board, so nothing was created",
        detail,
        boardSlug: null,
      };

    case "MissingFeature":
      return {
        code,
        headline: "That feature is not on the board — pick another",
        detail,
        boardSlug: null,
      };

    default:
      return null;
  }
}

/**
 * The features the task arm can hang a task under.
 *
 * `promote-to-task` binds `featureId`, and no board door puts a feature guid on the wire (measured:
 * `GET /projects/{id}/planning/features` rows carry slug + title + state, no `id`). The graph read
 * does: a feature node is `{kind:"feature", id, label, projectId, featureStatus}`. So the candidate
 * list is the graph field for the chosen project, widened to include Done cards, filtered to feature
 * nodes — the slice's own reviewed read door, not an invented one.
 */
export function featureCandidates(
  nodes: readonly GraphNode[],
  projectId: string,
): FeatureCandidate[] {
  return nodes
    .filter((node) => node.kind === "feature")
    .filter((node) => projectId.length === 0 || node.projectId === projectId)
    .map((node) => ({
      id: node.id,
      label: node.label.trim().length > 0 ? node.label : "Untitled feature",
      done: node.featureStatus === "done",
    }))
    .sort((a, b) => (a.done === b.done ? a.label.localeCompare(b.label, "en") : a.done ? 1 : -1));
}

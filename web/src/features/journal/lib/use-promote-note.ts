"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { QueryClient } from "@tanstack/react-query";
import { promoteNote } from "@/features/journal/api";
import {
  featureCandidates,
  promotionInvalidations,
  promotionRequest,
  type PromotionCacheView,
  type PromotionRequest,
} from "@/features/journal/lib/note-promotion";
import { graphQueryOptions } from "@/features/journal/queries";
import type { NotePromotionResult, PromotionForm } from "@/features/journal/types";

// The candidates a task can hang under, read off the graph field for the board the owner picked.
// The board door carries no feature id on the wire (measured on the built pipeline: the rows are
// slug + title + state), so the graph read is the one reviewed door that names a feature the way
// `promote-to-task` asks for it.
//
// Done features are widened in: putting a task on a closed card is the owner's call, not one this
// surface gets to foreclose by hiding the option.
export function useFeatureCandidates(projectId: string) {
  const field = useQuery({
    ...graphQueryOptions({
      types: [],
      project: projectId,
      focus: "",
      depth: "",
      orphansOnly: false,
      includeDone: true,
      search: "",
    }),
    // No board picked, no candidates asked for: the unfiltered field is the whole graph, and a
    // note view that has not been opened to the task arm has no business loading it.
    enabled: projectId.length > 0,
  });

  return {
    candidates: featureCandidates(field.data?.nodes ?? [], projectId),
    loading: field.isPending && projectId.length > 0,
    error: field.error,
  };
}

interface PromoteVariables {
  entryId: string;
  request: PromotionRequest;
  view: PromotionCacheView;
}

export interface PromoteOutcome {
  promotion: NotePromotionResult | null;
  pending: boolean;
  error: Error | null;
  reset: () => void;
  submit: (entryId: string, form: PromotionForm, view: PromotionCacheView) => void;
}

/**
 * The one mutation the triage UI runs, and the only place its invalidations are written down.
 *
 * A blocked form never reaches the network: `promotionRequest` answers null and the submit is a
 * no-op, which is what makes "cancel leaves nothing behind" a property of this surface rather than
 * a favour asked of the door — the door is simply never called.
 *
 * The invalidations are awaited inside `onSuccess`, and React Query settles the mutation only
 * after that promise resolves, so `pending` stays true across the refetch. Clearing busy before the
 * caches land is the stale-panel bug; the timing is pinned in `web/tests/journal-triage.test.mts`,
 * not trusted.
 */
export function usePromoteNote(): PromoteOutcome {
  const queryClient = useQueryClient();

  const mutation = useMutation<NotePromotionResult, Error, PromoteVariables>({
    mutationFn: ({ entryId, request }) => promoteNote(entryId, request),
    onSuccess: (result, variables) =>
      invalidatePromotionCaches(queryClient, result, variables.view),
  });

  return {
    promotion: mutation.data ?? null,
    pending: mutation.isPending,
    error: mutation.error,
    reset: () => mutation.reset(),
    submit: (entryId, form, view) => {
      // The disabled button is the interface's half of the single-click guarantee and this guard is
      // the mutation's; both exist because of review-237-04 finding 2's accepted TOCTOU window.
      if (mutation.isPending) {
        return;
      }
      const request = promotionRequest(form);
      if (request === null) {
        return;
      }
      mutation.mutate({ entryId, request, view });
    },
  };
}

// Split out so the invalidation set is testable against a real QueryClient without mounting the
// hook: the assertion a reviewer needs is "these caches, and only these, went stale".
export function invalidatePromotionCaches(
  queryClient: QueryClient,
  result: NotePromotionResult,
  view: PromotionCacheView,
): Promise<void> {
  const invalidations = promotionInvalidations(result, view);
  return Promise.all(
    invalidations.map((entry) => queryClient.invalidateQueries({ queryKey: entry.queryKey })),
  ).then(() => undefined);
}

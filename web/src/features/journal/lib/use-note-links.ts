"use client";

import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { ceremoniesQueryOptions } from "@/features/attention";
import { projectListQueryOptions } from "@/features/projects";
import { searchQueryOptions } from "@/features/search";
import { EMPTY_CANDIDATES, type CandidateSet } from "@/features/journal/lib/link-targets";
import { relatedRows, type RelatedRow } from "@/features/journal/lib/note-rails";
import { useJournalStream } from "@/features/journal/lib/use-journal";
import type { JournalServerFilters } from "@/features/journal/types";

// The picker's three arms, all from queries the app already runs — no new endpoint, because
// the anti-scope is explicit that a missing DTO field is a report, not a field to bolt on.
// The notes arm is the inbox's own stream query under the inbox's own key: arriving from the
// list costs one cache hit, and the beat that keeps the stream honest also makes a note an
// agent captured a moment ago linkable without a reload.
const NO_SERVER_FILTERS: JournalServerFilters = { tag: "", link: "", project: "" };

export interface LinkCandidates {
  candidates: CandidateSet;
  loading: boolean;
}

export function useLinkCandidates(containerId: string): LinkCandidates {
  const notes = useJournalStream(containerId, NO_SERVER_FILTERS);
  const projects = useQuery(projectListQueryOptions());
  const ceremonies = useQuery(ceremoniesQueryOptions());

  return useMemo(() => {
    const featureRows = ceremonies.data?.features ?? [];
    const projectNameById = new Map(
      (projects.data ?? []).map((project) => [project.id, project.name]),
    );
    const notesArm = notes.data.map((note) => ({ id: note.id, title: note.title }));
    const projectsArm = (projects.data ?? []).map((project) => ({
      id: project.id,
      name: project.name,
    }));
    const featuresArm = featureRows.map((feature) => ({
      projectId: feature.projectId,
      projectName: feature.projectName || projectNameById.get(feature.projectId) || "",
      slug: feature.featureSlug,
      title: feature.title,
    }));
    // The shared constant, not a fresh triple of empty arrays: every keystroke in the editor
    // recomputes the suggestion list off this object's identity, and an empty-but-new candidate
    // set throws that work away on the way to the first real answer.
    if (notesArm.length === 0 && projectsArm.length === 0 && featuresArm.length === 0) {
      return { candidates: EMPTY_CANDIDATES, loading: notes.loading && projects.isPending };
    }
    return {
      candidates: { notes: notesArm, projects: projectsArm, features: featuresArm },
      // A dead ceremonies call must not blank the picker: notes and projects still resolve,
      // the feature arm is the one that goes quiet.
      loading: notes.loading && projects.isPending,
    };
  }, [ceremonies.data, notes.data, notes.loading, projects.data, projects.isPending]);
}

export interface RelatedNotes {
  rows: RelatedRow[];
  loading: boolean;
  error: Error | null;
}

// Riding the semantic search door rather than a new "similar" one (spec R7 keeps the read
// surface additive). The query stays empty until the owner asks, and an empty query
// disables the options — so opening a note embeds nothing, and the degraded case (Qdrant
// down) is a button that answers with a line, not a broken section.
export function useRelatedNotes(
  query: string,
  selfId: string,
  linkedIds: ReadonlySet<string>,
): RelatedNotes {
  const search = useQuery(searchQueryOptions(query, "semantic", ""));
  return {
    rows: relatedRows(search.data?.hits ?? [], { selfId, linkedIds }),
    loading: query.trim().length > 0 && search.isPending,
    error: query.trim().length > 0 ? search.error : null,
  };
}

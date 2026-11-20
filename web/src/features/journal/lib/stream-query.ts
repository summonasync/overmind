import type { JournalServerFilters } from "../types.ts";

// One builder for every stream request the inbox makes — the page rows AND the linked
// subset the card accent reads. Both go through here so the accent and the chip cannot
// drift into two different definitions of "linked"; the Host owns that definition
// (EntryLinkTriage).
export function journalStreamParams(filters: JournalServerFilters): URLSearchParams {
  const params = new URLSearchParams();
  params.set("type", "journal");
  // The server default for journal is state=all; the inbox must not surface graves or
  // superseded drafts, so it asks for the living rows explicitly.
  params.set("state", "active");

  const tag = filters.tag.trim();
  if (tag.length > 0) {
    params.set("tag", tag);
  }
  const link = filters.link.trim();
  if (link.length > 0) {
    params.set("link", link);
  }
  const project = filters.project.trim();
  if (project.length > 0) {
    params.set("project", project);
  }
  return params;
}

export function journalStreamPath(projectId: string, filters: JournalServerFilters): string {
  return `/projects/${projectId}/entries?${journalStreamParams(filters).toString()}`;
}

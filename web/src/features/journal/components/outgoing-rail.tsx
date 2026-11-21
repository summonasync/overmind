"use client";

import { RailHeading } from "@/features/journal/components/backlinks-rail";
import {
  groupOutgoing,
  outgoingDeadEndReason,
  outgoingHref,
} from "@/features/journal/lib/note-rails";
import type { JournalOutgoingLink } from "@/features/journal/types";

interface OutgoingRailProps {
  links: readonly JournalOutgoingLink[];
  onNavigate: (href: string) => void;
}

// The outgoing half of the graph, read off the Host's resolved edges (R7) — nothing here
// re-parses the body. Passport §4 groups it as Linked project / Linked features; notes are
// the third group the grammar allows, tasks the fourth that 237's promotion edge made
// reachable, and an empty group renders nothing rather than a heading over air.
export function OutgoingRail({ links, onNavigate }: OutgoingRailProps) {
  if (links.length === 0) {
    return null;
  }
  const groups = groupOutgoing(links);
  const sections = [
    { title: "Linked notes", rows: groups.notes },
    { title: "Linked projects", rows: groups.projects },
    { title: "Linked features", rows: groups.features },
    { title: "Linked tasks", rows: groups.tasks },
    { title: "Links this build cannot label", rows: groups.other },
  ].filter((section) => section.rows.length > 0);

  return (
    <div className="space-y-3">
      {sections.map((section) => (
        <section key={section.title} aria-label={section.title}>
          <RailHeading title={section.title} count={section.rows.length} />
          <ul className="mt-1 space-y-1">
            {section.rows.map((link) => {
              const href = outgoingHref(link);
              if (href === null) {
                // A row the payload cannot address says so in visible text, not only in a
                // tooltip: an unclickable row that looks like every other row is how a
                // mis-filed kind stays unnoticed. The reasons live in `outgoingDeadEndReason`,
                // next to the href rule that decides them.
                return (
                  <li key={`${link.kind}:${link.id}`} className="min-w-0">
                    <span
                      title={outgoingDeadEndReason(link) ?? undefined}
                      className="flex min-w-0 items-baseline gap-2 text-sm"
                    >
                      <span className="truncate text-foreground">{label(link)}</span>
                      <span className="shrink-0 text-xs text-muted">no link yet</span>
                    </span>
                  </li>
                );
              }
              return (
                <li key={`${link.kind}:${link.id}`} className="min-w-0">
                  <a
                    href={href}
                    onClick={(event) => {
                      event.preventDefault();
                      onNavigate(href);
                    }}
                    className="block truncate text-sm text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
                  >
                    {label(link)}
                  </a>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

function label(link: JournalOutgoingLink): string {
  const trimmed = link.label.trim();
  if (trimmed.length > 0) {
    return trimmed;
  }
  // Only the two kinds the payload genuinely labels by title get a noun; guessing "note" for a
  // task row would put a word on screen the edge never carried.
  if (link.kind === "project") {
    return "Untitled project";
  }
  return link.kind === "entry" ? "Untitled note" : "Untitled target";
}

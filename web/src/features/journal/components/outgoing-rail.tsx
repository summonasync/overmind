"use client";

import { RailHeading } from "@/features/journal/components/backlinks-rail";
import { groupOutgoing, outgoingHref } from "@/features/journal/lib/note-rails";
import type { JournalOutgoingLink } from "@/features/journal/types";

interface OutgoingRailProps {
  links: readonly JournalOutgoingLink[];
  onNavigate: (href: string) => void;
}

// The outgoing half of the graph, read off the Host's resolved edges (R7) — nothing here
// re-parses the body. Passport §4 groups it as Linked project / Linked features; notes are
// the third group the grammar allows, and an empty group renders nothing rather than a
// heading over air.
export function OutgoingRail({ links, onNavigate }: OutgoingRailProps) {
  if (links.length === 0) {
    return null;
  }
  const groups = groupOutgoing(links);
  const sections = [
    { title: "Linked notes", rows: groups.notes },
    { title: "Linked projects", rows: groups.projects },
    { title: "Linked features", rows: groups.features },
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
                // A resolved feature edge carries the feature's guid and title, and every
                // feature URL in this app is built from `project=<id>&feature=<slug>`
                // (`feature-dependencies-panel.tsx:39`), which the DTO does not have. Guessing
                // one would land the owner on the wrong board, so the row shows the truth it
                // was given and stays a label. Filed as an obstacle on the task.
                return (
                  <li key={`${link.kind}:${link.id}`} className="min-w-0">
                    <span
                      title="The detail payload labels this edge by title and guid only — no slug to build a link from."
                      className="block truncate text-sm text-foreground"
                    >
                      {label(link)}
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
  return link.kind === "project" ? "Untitled project" : "Untitled note";
}

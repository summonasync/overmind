"use client";

import { FilePlus2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { RailHeading } from "@/features/journal/components/backlinks-rail";
import {
  creatableTitle,
  deadEndDetail,
  deadEndKind,
  deadEndLabel,
} from "@/features/journal/lib/note-rails";

interface UnresolvedListProps {
  targets: readonly string[];
  creating: string | null;
  onCreate: (target: string, title: string) => void;
}

// Spec R3: an unresolved target is not stored, is not an error, and must be shown verbatim so
// the owner can see the exact marker that failed — this is the dead-end list, triage fuel for
// the inbox's unlinked filter. The Host already normalizes the prefix casing in its projection
// (`EntryLinkProjection.Verbatim`), so the string here is the display form; `deadEndLabel`
// strips it for the title of the note the create affordance would write.
export function UnresolvedList({ targets, creating, onCreate }: UnresolvedListProps) {
  if (targets.length === 0) {
    return null;
  }

  return (
    <section aria-label="Unresolved links">
      <RailHeading title="Unresolved links" count={targets.length} />
      <ul className="mt-1 space-y-2">
        {targets.map((target) => {
          const kind = deadEndKind(target);
          const title = creatableTitle(target);
          return (
            <li key={target} className="min-w-0">
              <div className="flex items-center gap-1.5">
                <Badge size="xs" tone="warning">
                  {kind}
                </Badge>
                <span className="min-w-0 truncate text-sm text-foreground font-mono">
                  {target}
                </span>
              </div>
              <p className="mt-0.5 text-xs text-muted">{deadEndDetail(kind)}</p>
              {title === null ? null : (
                <Button
                  size="xs"
                  variant="accent"
                  className="mt-1"
                  disabled={creating !== null}
                  onClick={() => onCreate(target, title)}
                >
                  <FilePlus2 size={12} aria-hidden />
                  {creating === target ? "Creating…" : `Create “${deadEndLabel(target)}”`}
                </Button>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

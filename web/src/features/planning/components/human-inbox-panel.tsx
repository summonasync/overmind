"use client";

import { FeatureNumberBadge } from "@/features/planning/components/feature-number-badge";
import { inboxHolderLabel, inboxRowHref } from "@/features/planning/lib/human-inbox";
import { useHumanInbox } from "@/features/planning/lib/use-planning";
import type { PlanningInboxItem } from "@/features/planning/types";
import type { ProjectNumbering } from "@/shared/lib/feature-number";
import { Badge } from "@/shared/ui/badge";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { formatRelativeTime } from "@/shared/lib/relative-time";

interface HumanInboxPanelProps {
  projectId: string;
  numbering: ProjectNumbering;
}

export function HumanInboxPanel({ projectId, numbering }: HumanInboxPanelProps) {
  const { items, loading, error } = useHumanInbox(projectId);

  return (
    <section
      aria-labelledby="human-inbox-heading"
      className="space-y-2 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex items-center gap-2">
        <h2 id="human-inbox-heading" className="text-sm font-semibold text-foreground">
          Waiting on you
        </h2>
        {items.length > 0 ? (
          <Badge tone="warning" size="xs">
            {items.length}
          </Badge>
        ) : null}
      </div>
      {error ? (
        <ErrorText error={error} />
      ) : loading ? (
        <LoadingState label="Loading the human inbox…" />
      ) : items.length === 0 ? (
        <p className="text-xs text-success">All clear — nothing is waiting on you.</p>
      ) : (
        <ul className="divide-y divide-border">
          {items.map((item) => (
            <HumanInboxRow key={item.slug} projectId={projectId} item={item} numbering={numbering} />
          ))}
        </ul>
      )}
    </section>
  );
}

interface HumanInboxRowProps {
  projectId: string;
  item: PlanningInboxItem;
  numbering: ProjectNumbering;
}

function HumanInboxRow({ projectId, item, numbering }: HumanInboxRowProps) {
  const href = inboxRowHref(projectId, item);
  const holder = inboxHolderLabel(item);

  return (
    <li className="flex flex-wrap items-center gap-2 py-1.5">
      <FeatureNumberBadge slug={item.slug} numbering={numbering} />
      <a
        href={href}
        className="group rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <span className="text-sm text-foreground group-hover:text-primary">{item.title}</span>
      </a>
      <Badge tone="primary" size="xs" className="normal-case">
        {item.phase}
      </Badge>
      {holder !== null ? (
        <Badge tone={item.claimState === "alive" ? "success" : "muted"} size="xs">
          {holder}
        </Badge>
      ) : null}
      <span className="ml-auto text-xs text-muted">{formatRelativeTime(item.pendingSince)}</span>
      <a href={href} className="text-xs font-medium text-primary hover:underline">
        Open →
      </a>
    </li>
  );
}

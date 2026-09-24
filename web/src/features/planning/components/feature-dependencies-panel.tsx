"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Select } from "@/shared/ui/select";
import { usePlanningBoard, usePlanningMutations } from "@/features/planning/lib/use-planning";
import type {
  PlanningFeatureDependencyEntry,
  PlanningFeatureResponse,
} from "@/features/planning/types";

interface BlockerChipProps {
  projectId: string;
  entry: PlanningFeatureDependencyEntry;
  pending: boolean;
  onRemove: (blockedBySlug: string) => void;
}

function BlockerChip({ projectId, entry, pending, onRemove }: BlockerChipProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-md border px-2 py-0.5 font-mono text-xs font-medium",
        entry.closed ? "border-border/50 text-muted/70" : "border-danger/40 bg-danger/10 text-danger",
      )}
    >
      <a
        href={`/dashboard?project=${projectId}&feature=${entry.slug}`}
        className={cn(
          "rounded-sm underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          entry.closed && "line-through",
        )}
      >
        {entry.slug}
      </a>
      <button
        type="button"
        aria-label={`Stop waiting for ${entry.slug}`}
        disabled={pending}
        onClick={() => onRemove(entry.slug)}
        className="rounded-sm p-0.5 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <X size={12} aria-hidden />
      </button>
    </span>
  );
}

interface FeatureDependenciesPanelProps {
  projectId: string;
  feature: PlanningFeatureResponse;
}

export function FeatureDependenciesPanel({ projectId, feature }: FeatureDependenciesPanelProps) {
  const [nextBlocker, setNextBlocker] = useState("");
  const { data: board } = usePlanningBoard(projectId);
  const { addFeatureDependency, removeFeatureDependency } = usePlanningMutations(projectId);

  const openBlockers = feature.blockedBy.filter((entry) => !entry.closed);
  const candidates = board
    .filter(
      (item) =>
        item.slug !== feature.slug &&
        !feature.blockedBy.some((entry) => entry.slug === item.slug),
    )
    .sort((left, right) => left.slug.localeCompare(right.slug));
  const pending = addFeatureDependency.isPending || removeFeatureDependency.isPending;

  const addBlocker = () => {
    if (nextBlocker.length === 0) {
      return;
    }
    addFeatureDependency.mutate(
      { slug: feature.slug, blockedBy: nextBlocker },
      { onSuccess: () => setNextBlocker("") },
    );
  };

  return (
    <section className="space-y-2 rounded-lg border border-border bg-surface p-4" aria-label="Dependencies">
      <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
        <div className="flex flex-wrap items-center gap-1.5">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">Waits for</h3>
          {openBlockers.length > 0 ? (
            <Badge tone="danger">{openBlockers.length} open</Badge>
          ) : null}
          {feature.blockedBy.length === 0 ? (
            <span className="text-xs text-muted">nothing yet</span>
          ) : (
            feature.blockedBy.map((entry) => (
              <BlockerChip
                key={entry.slug}
                projectId={projectId}
                entry={entry}
                pending={pending}
                onRemove={(blockedBySlug) =>
                  removeFeatureDependency.mutate({ slug: feature.slug, blockedBy: blockedBySlug })
                }
              />
            ))
          )}
          {candidates.length > 0 ? (
            <span className="flex items-center gap-1.5">
              <Select
                aria-label="Add blocker"
                className="px-2 py-0.5 text-xs"
                value={nextBlocker}
                disabled={pending}
                onChange={(event) => setNextBlocker(event.target.value)}
              >
                <option value="">Add blocker…</option>
                {candidates.map((item) => (
                  <option key={item.slug} value={item.slug}>
                    {item.slug}
                  </option>
                ))}
              </Select>
              <Button
                variant="secondary"
                className="px-2.5 py-1 text-xs"
                disabled={nextBlocker.length === 0 || pending}
                onClick={addBlocker}
              >
                Add
              </Button>
            </span>
          ) : null}
        </div>

        {feature.blocks.length > 0 ? (
          <div className="flex flex-wrap items-center gap-1.5">
            <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">Blocks</h3>
            {feature.blocks.map((entry) => (
              <a
                key={entry.slug}
                href={`/dashboard?project=${projectId}&feature=${entry.slug}`}
                className={cn(
                  "inline-flex items-center rounded-md border border-border px-2 py-0.5 font-mono text-xs font-medium text-muted underline-offset-2 hover:border-border-strong hover:text-foreground hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                  entry.closed && "line-through",
                )}
              >
                {entry.slug}
              </a>
            ))}
          </div>
        ) : null}
      </div>

      <ErrorText
        error={(addFeatureDependency.error as Error | null) ?? (removeFeatureDependency.error as Error | null)}
      />
    </section>
  );
}

"use client";

import { useCeremonies } from "@/features/attention";
import { FeatureNumberBadge } from "@/features/planning";
import { useProjectNumbering } from "@/features/projects";
import type { CeremonyGroup } from "@/features/attention";
import { Badge } from "@/shared/ui/badge";
import { ErrorText } from "@/shared/ui/error-text";

export function CeremoniesSection() {
  const { groups, isPending, error } = useCeremonies();

  if (isPending || (error === null && groups.length === 0)) {
    return null;
  }

  return (
    <section aria-labelledby="ceremonies-heading" className="space-y-2">
      <h2 id="ceremonies-heading" className="text-base font-semibold text-foreground">
        Ceremonies
      </h2>
      {error ? <ErrorText error={error} /> : null}
      {groups.map((group) => (
        <CeremonyGroupCard key={group.projectId} group={group} />
      ))}
    </section>
  );
}

function CeremonyGroupCard({ group }: { group: CeremonyGroup }) {
  const numbering = useProjectNumbering(group.projectId);

  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface">
      <p className="border-b border-border bg-surface-raised/50 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted">
        {group.projectName}
      </p>
      <ul className="divide-y divide-border">
        {group.features.map((feature) => (
          <li key={feature.featureSlug} className="flex flex-wrap items-center gap-2 px-3 py-2">
            <FeatureNumberBadge slug={feature.featureSlug} numbering={numbering} />
            {feature.url ? (
              <a
                href={feature.url}
                className="group rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                <span className="text-sm text-foreground group-hover:text-primary">{feature.title}</span>
              </a>
            ) : (
              <span className="text-sm text-foreground">{feature.title}</span>
            )}
            <span className="ml-auto flex flex-wrap items-center gap-1">
              {feature.reasons.map((reason) => (
                <Badge key={reason} tone="warning">
                  {reason}
                </Badge>
              ))}
            </span>
            {feature.url ? (
              <a href={feature.url} className="text-xs font-medium text-primary hover:underline">
                Open →
              </a>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}

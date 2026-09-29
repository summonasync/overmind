"use client";

import { useCeremonies } from "@/features/attention";
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
        <div key={group.projectId} className="overflow-hidden rounded-lg border border-border bg-surface">
          <p className="border-b border-border bg-surface-raised/50 px-3 py-2 text-xs font-semibold uppercase tracking-wide text-muted">
            {group.projectName}
          </p>
          <ul className="divide-y divide-border">
            {group.features.map((feature) => (
              <li key={feature.featureSlug} className="flex flex-wrap items-center gap-2 px-3 py-2">
                {feature.number ? (
                  <span className="font-mono text-xs font-semibold text-foreground">{feature.number}</span>
                ) : null}
                <span className="text-sm text-foreground">{feature.title}</span>
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
      ))}
    </section>
  );
}

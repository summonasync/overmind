"use client";

import { Check } from "lucide-react";
import { useAwaitingNotifications } from "@/features/notifications";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { AwaitingRow } from "@/pages/attention-page/ui/awaiting-row";

export function AwaitingSection() {
  const { items, isPending, error } = useAwaitingNotifications();

  return (
    <section aria-labelledby="awaiting-heading" className="space-y-2">
      <div className="flex items-center gap-2">
        <h2 id="awaiting-heading" className="text-base font-semibold text-foreground">
          Waiting on you
        </h2>
        {items.length > 0 ? (
          <span className="rounded bg-warning/10 px-1.5 py-0.5 text-xs font-medium text-warning">
            {items.length}
          </span>
        ) : null}
      </div>
      {isPending ? (
        <LoadingState label="Loading waiting items…" />
      ) : error ? (
        <ErrorText error={error} />
      ) : items.length === 0 ? (
        <div className="flex items-center gap-2 rounded-lg border border-dashed border-border px-3 py-2.5 text-xs text-muted">
          <Check size={14} className="text-success" aria-hidden />
          All clear — nothing is waiting on you.
        </div>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item) => (
            <AwaitingRow key={item.id} item={item} />
          ))}
        </ul>
      )}
    </section>
  );
}

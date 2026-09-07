import type { ReactNode } from "react";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";

interface EmptyStateProps {
  title: string;
  hint?: string;
  action?: ReactNode;
}

export function EmptyState({ title, hint, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-surface/50 py-10 text-center">
      <Zerling pose="confused" size={ZERLING_SIZES.display} />
      <p className="text-sm font-medium text-foreground">{title}</p>
      {hint ? <p className="max-w-sm text-xs text-muted">{hint}</p> : null}
      {action}
    </div>
  );
}

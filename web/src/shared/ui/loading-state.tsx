import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";

interface LoadingStateProps {
  label: string;
}

export function LoadingState({ label }: LoadingStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface px-4 py-10 text-sm text-muted">
      <Zerling pose="surprised" size={ZERLING_SIZES.display} />
      <span>{label}</span>
    </div>
  );
}

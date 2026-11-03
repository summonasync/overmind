import { Badge } from "@/shared/ui/badge";

export function WaveStreamChip() {
  return (
    <span role="status">
      <Badge tone="warning">
        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-warning" aria-hidden="true" />
        Reconnecting
      </Badge>
    </span>
  );
}

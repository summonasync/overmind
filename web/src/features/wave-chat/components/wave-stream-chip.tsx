import { Badge } from "@/shared/ui/badge";
import { useWaveStreamStatus } from "@/features/wave-chat/lib/wave-stream-state";

export function WaveStreamChip() {
  const status = useWaveStreamStatus();
  return (
    <span role="status">
      {status === "reconnecting" ? (
        <Badge tone="warning">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-warning" aria-hidden="true" />
          Reconnecting
        </Badge>
      ) : null}
    </span>
  );
}

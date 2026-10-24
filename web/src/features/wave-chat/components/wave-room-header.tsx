import { roomLabel } from "@/features/wave-chat/lib/room-order";
import { presenceLine } from "@/features/wave-chat/lib/roster";
import type { WaveParticipant } from "@/features/wave-chat/types";

interface WaveRoomHeaderProps {
  room: string;
  participants: WaveParticipant[];
}

export function WaveRoomHeader({ room, participants }: WaveRoomHeaderProps) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 rounded-lg border border-border bg-surface px-3 py-2">
      <h2 className="text-sm font-semibold text-foreground">{roomLabel(room)}</h2>
      <p className="text-xs text-muted">{presenceLine(participants)}</p>
    </div>
  );
}

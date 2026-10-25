import { Fragment } from "react";
import { cn } from "@/shared/lib/cn";
import { roomLabel } from "@/features/wave-chat/lib/room-order";
import { presenceSeats } from "@/features/wave-chat/lib/roster";
import { authorLabel, roleTextClassOf } from "@/features/wave-chat/lib/role-color";
import type { WaveParticipant } from "@/features/wave-chat/types";

interface WaveRoomHeaderProps {
  room: string;
  participants: WaveParticipant[];
}

export function WaveRoomHeader({ room, participants }: WaveRoomHeaderProps) {
  const seats = presenceSeats(participants);
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 rounded-lg border border-border bg-surface px-3 py-2">
      <h2 className="text-sm font-semibold text-foreground">{roomLabel(room)}</h2>
      <p className="text-xs text-muted">
        {seats.length === 0
          ? "nobody in the wave"
          : seats.map((seat, index) => (
              <Fragment key={`${seat.alias}:${index}`}>
                {index > 0 ? " · " : null}
                <span className={cn("font-mono font-semibold", roleTextClassOf(seat.role))}>
                  {authorLabel(seat.alias, seat.role)}
                </span>
              </Fragment>
            ))}
      </p>
    </div>
  );
}

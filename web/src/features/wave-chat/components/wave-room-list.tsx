"use client";

import { Badge } from "@/shared/ui/badge";
import { cn } from "@/shared/lib/cn";
import { ALL_ROOM } from "@/features/wave-chat/lib/room-order";
import type { WaveRoom } from "@/features/wave-chat/types";

interface WaveRoomListProps {
  rooms: WaveRoom[];
  activeRoom: string;
  counts: Record<string, number>;
  onOpen: (room: string) => void;
}

export function WaveRoomList({ rooms, activeRoom, counts, onOpen }: WaveRoomListProps) {
  return (
    <nav aria-label="Wave rooms" className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-2">
      {rooms.map((room) => {
        const count = counts[room.room] ?? 0;
        const active = room.room === activeRoom;
        return (
          <button
            key={room.room}
            type="button"
            onClick={() => onOpen(room.room)}
            aria-current={active ? "true" : undefined}
            className={cn(
              "flex items-center justify-between gap-2 rounded-md px-2.5 py-1.5 text-left text-sm transition-colors",
              active
                ? "bg-surface-raised font-medium text-foreground"
                : "text-muted hover:bg-surface-raised hover:text-foreground",
            )}
          >
            <span className="truncate">{roomLabel(room.room)}</span>
            {count > 0 ? <Badge tone="primary">{count}</Badge> : null}
          </button>
        );
      })}
    </nav>
  );
}

function roomLabel(room: string): string {
  return room === ALL_ROOM ? "All lanes" : `#${room}`;
}

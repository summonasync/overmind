import type { WaveParticipant, WaveRole } from "@/features/wave-chat/types";

export function ownerOf(participants: WaveParticipant[]): WaveParticipant | null {
  return participants.find((participant) => participant.kind === "owner") ?? null;
}

export function agentsOf(participants: WaveParticipant[]): WaveParticipant[] {
  return participants.filter((participant) => participant.kind === "agent");
}

export interface PresenceSeat {
  alias: string;
  role: WaveRole;
}

export function presenceSeats(participants: WaveParticipant[]): PresenceSeat[] {
  const seats = agentsOf(participants).map((agent) => ({ alias: agent.alias, role: agent.role }));
  const owner = ownerOf(participants);
  if (owner !== null) {
    seats.push({ alias: owner.alias, role: owner.role });
  }
  return seats;
}

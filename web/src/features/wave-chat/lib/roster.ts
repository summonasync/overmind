import type { WaveParticipant } from "@/features/wave-chat/types";

export function ownerOf(participants: WaveParticipant[]): WaveParticipant | null {
  return participants.find((participant) => participant.kind === "owner") ?? null;
}

export function agentsOf(participants: WaveParticipant[]): WaveParticipant[] {
  return participants.filter((participant) => participant.kind === "agent");
}

export function presenceLine(participants: WaveParticipant[]): string {
  const agents = agentsOf(participants).length;
  if (agents === 0) {
    return "Owner in the wave";
  }
  return `${agents} agent${agents === 1 ? "" : "s"} + owner in the wave`;
}

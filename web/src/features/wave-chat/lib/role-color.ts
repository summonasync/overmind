import type { BadgeTone } from "@/shared/ui/badge";
import type { WaveRole } from "@/features/wave-chat/types";

const roleTextClasses: Record<WaveRole, string> = {
  owner: "text-primary",
  worker: "text-agent",
  marshal: "text-marshal",
};

const roleBadgeTones: Record<WaveRole, BadgeTone> = {
  owner: "primary",
  worker: "agent",
  marshal: "marshal",
};

export function roleTextClassOf(role: WaveRole): string {
  return roleTextClasses[role];
}

export function roleBadgeToneOf(role: WaveRole): BadgeTone {
  return roleBadgeTones[role];
}

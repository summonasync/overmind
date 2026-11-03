import type { BadgeTone } from "@/shared/ui/badge";
import type { WaveRole } from "@/features/wave-chat/types";

const roleTextClasses: Record<WaveRole, string> = {
  owner: "text-primary",
  worker: "text-agent",
  marshal: "text-marshal",
  embedded: "text-agent",
};

const roleBadgeTones: Record<WaveRole, BadgeTone> = {
  owner: "primary",
  worker: "agent",
  marshal: "marshal",
  embedded: "agent",
};

export function roleTextClassOf(role: WaveRole): string {
  return roleTextClasses[role];
}

export function roleBadgeToneOf(role: WaveRole): BadgeTone {
  return roleBadgeTones[role];
}

export function authorLabel(alias: string, role: WaveRole): string {
  if (role === "owner") {
    return alias;
  }
  return role === "marshal" ? `Marshal ${alias}` : `Agent ${alias}`;
}

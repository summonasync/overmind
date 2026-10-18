import {
  FileText,
  GitBranch,
  Lightbulb,
  Lock,
  ShieldCheck,
  ShieldHalf,
  Skull,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import type { EntryType } from "@/shared/api/types";

export type EntryTone =
  | "info"
  | "warning"
  | "success"
  | "danger"
  | "skill"
  | "idea"
  | "muted"
  | "audit";

export interface EntryTypeMeta {
  tone: EntryTone;
  icon: LucideIcon;
  label: string;
}

export const ENTRY_TYPE_META: Record<EntryType, EntryTypeMeta> = {
  note: { tone: "info", icon: FileText, label: "Note" },
  decision: { tone: "warning", icon: GitBranch, label: "Decision" },
  gate: { tone: "success", icon: ShieldCheck, label: "Gate" },
  guard: { tone: "danger", icon: Lock, label: "Guard" },
  skill: { tone: "skill", icon: Sparkles, label: "Skill" },
  tombstone: { tone: "muted", icon: Skull, label: "Tombstone" },
  featureRequest: { tone: "idea", icon: Lightbulb, label: "Feature request" },
  audit: { tone: "audit", icon: ShieldHalf, label: "Audit" },
};

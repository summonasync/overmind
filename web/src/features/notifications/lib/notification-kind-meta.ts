import { CircleHelp, Info, TriangleAlert, type LucideIcon } from "lucide-react";
import type { BadgeTone } from "@/shared/ui/badge";
import type { NotificationKind } from "../types";

export const NOTIFICATION_KIND_META: Record<NotificationKind, { tone: BadgeTone; icon: LucideIcon; iconClass: string }> = {
  approval: { tone: "warning", icon: TriangleAlert, iconClass: "text-warning" },
  question: { tone: "info", icon: CircleHelp, iconClass: "text-info" },
  info: { tone: "neutral", icon: Info, iconClass: "text-muted" },
};

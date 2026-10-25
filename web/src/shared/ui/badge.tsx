import type { CSSProperties, ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import type { EntryTone } from "@/shared/lib/entry-type";

export type BadgeTone = "neutral" | "primary" | "agent" | "marshal" | EntryTone;

interface BadgeProps {
  tone?: BadgeTone;
  children: ReactNode;
  className?: string;
  style?: CSSProperties;
}

const toneClasses: Record<BadgeTone, string> = {
  neutral: "border border-border bg-surface-raised text-muted",
  primary: "bg-primary/10 text-primary",
  agent: "bg-agent/10 text-agent",
  marshal: "bg-marshal/10 text-marshal",
  muted: "border border-border/50 text-muted/70",
  info: "bg-info/10 text-info",
  warning: "bg-warning/10 text-warning",
  success: "bg-success/10 text-success",
  danger: "bg-danger/10 text-danger",
  skill: "bg-type-skill/10 text-type-skill",
  idea: "bg-type-featureRequest/10 text-type-featureRequest",
  audit: "bg-type-audit/10 text-type-audit",
};

export function Badge({ tone = "neutral", children, className, style }: BadgeProps) {
  return (
    <span
      style={style}
      className={cn(
        "inline-flex items-center gap-1 rounded px-1.5 py-0.5 text-xs font-medium capitalize",
        toneClasses[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}

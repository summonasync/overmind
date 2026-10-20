"use client";

import { cn } from "@/shared/lib/cn";
import { FEATURE_PRIORITIES } from "@/features/planning";
import type { PlanningFeaturePriority } from "@/features/planning";

export type LanePriorityChoice = PlanningFeaturePriority | "none";

export const LANE_PRIORITY_CHOICES: readonly LanePriorityChoice[] = [
  "none",
  ...FEATURE_PRIORITIES,
];

interface LanePriorityPickerProps {
  value: LanePriorityChoice;
  onChange: (choice: LanePriorityChoice) => void;
}

export function LanePriorityPicker({ value, onChange }: LanePriorityPickerProps) {
  return (
    <div>
      <span className="mb-1 block text-xs font-medium text-muted">Priority</span>
      <div className="flex flex-wrap gap-1" role="group" aria-label="Lane priority">
        {LANE_PRIORITY_CHOICES.map((choice) => {
          const selected = value === choice;
          return (
            <button
              key={choice}
              type="button"
              onClick={() => onChange(choice)}
              aria-pressed={selected}
              className={cn(
                "rounded-md border px-2.5 py-1 text-xs font-medium transition-colors",
                "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                selected
                  ? "border-primary bg-primary/15 text-primary"
                  : "border-border text-muted hover:border-border-strong hover:text-foreground",
              )}
            >
              {choice === "none" ? "None" : choice.toUpperCase()}
            </button>
          );
        })}
      </div>
    </div>
  );
}

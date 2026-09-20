"use client";

import { cn } from "@/shared/lib/cn";

const PRESET_COLORS = [
  { name: "Red", hex: "#ef4444" },
  { name: "Orange", hex: "#fb923c" },
  { name: "Amber", hex: "#f59e0b" },
  { name: "Yellow", hex: "#facc15" },
  { name: "Lime", hex: "#a3e635" },
  { name: "Green", hex: "#22c55e" },
  { name: "Teal", hex: "#2dd4bf" },
  { name: "Cyan", hex: "#22d3ee" },
  { name: "Blue", hex: "#60a5fa" },
  { name: "Indigo", hex: "#818cf8" },
  { name: "Purple", hex: "#a855f7" },
  { name: "Pink", hex: "#f472b6" },
];

const swatchClass =
  "inline-flex h-5 w-5 items-center justify-center rounded-full transition-transform hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:scale-100";

interface TagColorPaletteProps {
  tagName: string;
  value: string | null;
  disabled?: boolean;
  onPick: (color: string) => void;
}

export function TagColorPalette({ tagName, value, disabled, onPick }: TagColorPaletteProps) {
  return (
    <div className="flex flex-wrap items-center gap-1" role="group" aria-label={`Color for ${tagName}`}>
      {PRESET_COLORS.map((color) => (
        <button
          key={color.hex}
          type="button"
          className={cn(
            swatchClass,
            "border border-black/30",
            value === color.hex && "ring-2 ring-foreground/60 ring-offset-2 ring-offset-surface-raised",
          )}
          style={{ backgroundColor: color.hex }}
          title={`${color.name} (${color.hex})`}
          aria-label={`Set ${color.name} (${color.hex})`}
          aria-pressed={value === color.hex}
          disabled={disabled}
          onClick={() => onPick(color.hex)}
        />
      ))}
      <button
        type="button"
        className={cn(
          swatchClass,
          "border border-dashed border-border-strong text-xs font-semibold text-muted",
          value === null && "ring-2 ring-foreground/60 ring-offset-2 ring-offset-surface-raised",
        )}
        title="No color"
        aria-label="Clear color"
        aria-pressed={value === null}
        disabled={disabled}
        onClick={() => onPick("")}
      >
        ×
      </button>
    </div>
  );
}

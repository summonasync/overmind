import { Badge } from "@/shared/ui/badge";
import type { EntryResponse, EntryState } from "@/features/entries/types";

type NonActiveEntryState = Exclude<EntryState, "active">;

const STATE_CHIP_META: Record<NonActiveEntryState, { label: string; tone: "warning" | "muted" }> = {
  lifted: { label: "lifted", tone: "warning" },
  superseded: { label: "superseded", tone: "muted" },
};

export function EntryStateChip({ entry }: { entry: EntryResponse }) {
  const state = entry.state;
  if (state !== "lifted" && state !== "superseded") {
    return null;
  }
  const meta = STATE_CHIP_META[state];
  const descId = `entry-state-${entry.id}-desc`;
  return (
    <span aria-describedby={descId} title={entry.stateReason ?? "No reason recorded"}>
      <Badge tone={meta.tone} size="xs">
        {meta.label}
      </Badge>
      <span id={descId} className="sr-only">
        {`${meta.label}: ${entry.stateReason ?? "no reason recorded"}`}
      </span>
    </span>
  );
}

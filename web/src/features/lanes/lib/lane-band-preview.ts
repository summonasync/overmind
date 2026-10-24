import { compareBoardItems } from "@/features/planning";
import type { PlanningFeatureBoardItem } from "@/features/planning";
import { reorderBandSlugs } from "@/features/lanes/lib/lane-optimistic";

// The board-facing half of the reorder preview: the band is read and sorted with the dashboard
// comparator, so the optimistic numbering matches what the server will renumber to, while the
// pure splice stays in a module the node tests can import without the slice alias.
export function previewBandReorder(
  board: PlanningFeatureBoardItem[],
  dragSlug: string,
  anchorSlug: string,
): PlanningFeatureBoardItem[] | null {
  const dragged = board.find((item) => item.slug === dragSlug);
  if (dragged === undefined || dragged.status === "done") {
    return null;
  }
  const band = board
    .filter((item) => item.priority === dragged.priority && item.status !== "done")
    .sort(compareBoardItems);
  const ordered = reorderBandSlugs(
    band.map((item) => item.slug),
    dragSlug,
    anchorSlug,
  );
  if (ordered === null) {
    return null;
  }
  const slotBySlug = new Map(ordered.map((slug, index) => [slug, index + 1] as const));
  return board.map((item) => {
    const slot = slotBySlug.get(item.slug);
    return slot === undefined ? item : { ...item, queuePosition: slot };
  });
}

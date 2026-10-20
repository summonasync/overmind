import { compareBoardItems } from "@/features/planning";
import type {
  PlanningFeatureBoardItem,
  PlanningFeaturePriority,
  PlanningFeatureStatus,
} from "@/features/planning";
import { isStatusVisible } from "@/features/lanes/lib/lane-filters";
import type { LaneFeatureSummary, LaneRow } from "@/features/lanes/types";

export interface LaneMoveTarget {
  position: number;
  upTo: number | null;
  downTo: number | null;
}

interface LaneMember {
  slug: string;
  position: number;
}

export function boardItemsBySlug(
  board: PlanningFeatureBoardItem[],
): ReadonlyMap<string, PlanningFeatureBoardItem> {
  return new Map(board.map((item) => [item.slug, item] as const));
}

// A band is one priority's non-done features ordered the way LoadBandAsync reads them, and a toPosition
// addresses a slot in it — the numbering is the server's, so the slot a control sends has to be derived
// from the same ordering instead of a lane-local index.
function bandSlotBySlug(board: PlanningFeatureBoardItem[]): Map<string, number> {
  const bands = new Map<PlanningFeaturePriority, PlanningFeatureBoardItem[]>();
  for (const item of board) {
    if (item.status === "done") {
      continue;
    }
    const band = bands.get(item.priority);
    if (band === undefined) {
      bands.set(item.priority, [item]);
    } else {
      band.push(item);
    }
  }

  const slots = new Map<string, number>();
  for (const band of bands.values()) {
    band.sort(compareBoardItems).forEach((item, index) => {
      slots.set(item.slug, index + 1);
    });
  }
  return slots;
}

// The displayed slice picks the mate, the full band picks the slot: a move aimed at a card this column
// hides does land on the band, but the reader sees nothing move — so the target walks only rendered
// cards, while the number it sends stays the server's own band slot, untouched by any filter.
export function laneMoveTargetsBySlug(
  lanes: LaneRow[],
  board: PlanningFeatureBoardItem[],
  statusFilter: ReadonlySet<PlanningFeatureStatus>,
): ReadonlyMap<string, LaneMoveTarget> {
  const slots = bandSlotBySlug(board);
  const priorityBySlug = new Map(
    board.map((item) => [item.slug, item.priority] as const),
  );
  const targets = new Map<string, LaneMoveTarget>();

  for (const lane of lanes) {
    const bands = new Map<PlanningFeaturePriority, LaneMember[]>();
    for (const feature of lane.features) {
      const position = slots.get(feature.slug);
      const priority = priorityBySlug.get(feature.slug);
      if (
        position === undefined ||
        priority === undefined ||
        feature.status === "done" ||
        !isStatusVisible(statusFilter, feature.status)
      ) {
        continue;
      }
      const band = bands.get(priority);
      if (band === undefined) {
        bands.set(priority, [{ slug: feature.slug, position }]);
      } else {
        band.push({ slug: feature.slug, position });
      }
    }

    for (const band of bands.values()) {
      const ordered = band.sort((a, b) => a.position - b.position);
      ordered.forEach((member, index) => {
        targets.set(member.slug, {
          position: member.position,
          upTo: index > 0 ? ordered[index - 1].position : null,
          downTo: index < ordered.length - 1 ? ordered[index + 1].position : null,
        });
      });
    }
  }
  return targets;
}

function boardRank(item: PlanningFeatureBoardItem | undefined): number {
  return item === undefined ? 1 : 0;
}

export function sortLaneFeatures(
  features: LaneFeatureSummary[],
  board: ReadonlyMap<string, PlanningFeatureBoardItem>,
): LaneFeatureSummary[] {
  return [...features].sort((a, b) => {
    const left = board.get(a.slug);
    const right = board.get(b.slug);
    // The roster and the board are two live reads, so a feature created between them is in one and not
    // the other; those sink to the end of the column instead of inventing an ordering rule.
    if (left === undefined || right === undefined) {
      return boardRank(left) - boardRank(right) || a.slug.localeCompare(b.slug);
    }
    return compareBoardItems(left, right);
  });
}

export function sortBoardItems(items: PlanningFeatureBoardItem[]): PlanningFeatureBoardItem[] {
  return [...items].sort(compareBoardItems);
}

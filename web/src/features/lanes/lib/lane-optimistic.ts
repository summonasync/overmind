import type { PlanningFeatureBoardItem } from "@/features/planning";
import type { LanesRoster } from "@/features/lanes/types";

type LaneBucket = LanesRoster["lanes"][number];

// The roster preview moves one summary between lane buckets and recomputes the open count from the
// buckets it leaves behind, because the server derives openFeatureCount the same way and the roster
// read is what the columns render between the mutation and its invalidation refetch.
export function previewLaneAssignment(
  roster: LanesRoster,
  slug: string,
  laneName: string | null,
): LanesRoster {
  const source = roster.lanes.find((lane) => lane.features.some((feature) => feature.slug === slug));
  if (source === undefined) {
    return roster;
  }
  if (laneName !== null && !roster.lanes.some((lane) => lane.name === laneName)) {
    return roster;
  }
  const moved = source.features.find((feature) => feature.slug === slug);
  if (moved === undefined) {
    return roster;
  }
  const openCount = (features: LaneBucket["features"]): number =>
    features.filter((feature) => feature.status !== "done").length;
  return {
    lanes: roster.lanes.map((lane) => {
      const withoutMoved =
        lane === source ? lane.features.filter((feature) => feature.slug !== slug) : lane.features;
      const features = lane.name === laneName ? [...withoutMoved, moved] : withoutMoved;
      return { ...lane, features, openFeatureCount: openCount(features) };
    }),
  };
}

export function previewBoardLane(
  board: PlanningFeatureBoardItem[],
  slug: string,
  laneName: string | null,
): PlanningFeatureBoardItem[] {
  return board.map((item) => (item.slug === slug ? { ...item, lane: laneName } : item));
}

// A queue-move addresses one band slot the way the server numbers them: remove the dragged feature,
// insert it at the anchor's pre-move slot, renumber the band densely. The anchor must share the
// dragged feature's priority band, so a drop across priorities never invents a cross-band write.
export function reorderBandSlugs(
  band: readonly string[],
  dragSlug: string,
  anchorSlug: string,
): readonly string[] | null {
  const from = band.indexOf(dragSlug);
  const to = band.indexOf(anchorSlug);
  if (from === -1 || to === -1 || from === to) {
    return null;
  }
  const next = [...band];
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

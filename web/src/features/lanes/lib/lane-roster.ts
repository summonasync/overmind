import type { LaneRow, LaneUpsertInput } from "@/features/lanes/types";

export const LANE_NAME_MAX_LENGTH = 64;

export function toUpsertRows(lanes: LaneRow[]): LaneUpsertInput[] {
  return lanes.map((lane) => ({
    name: lane.name,
    contour: lane.contour,
    serialOrder: lane.serialOrder,
    priority: lane.priority,
  }));
}

export function appendLane(
  lanes: LaneRow[],
  name: string,
  contour: string | null,
  priority: number | null,
): LaneUpsertInput[] {
  const trimmedName = name.trim();
  const trimmedContour = contour?.trim() ?? "";
  return [
    ...toUpsertRows(lanes),
    {
      name: trimmedName,
      contour: trimmedContour.length > 0 ? trimmedContour : null,
      serialOrder: null,
      priority,
    },
  ];
}

export function removeLane(lanes: LaneRow[], name: string): LaneUpsertInput[] {
  return toUpsertRows(lanes.filter((lane) => lane.name !== name));
}

export function validateLaneName(name: string, lanes: LaneRow[]): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return "Lane name is required.";
  }
  if (trimmed.length > LANE_NAME_MAX_LENGTH) {
    return `Lane name must be ${LANE_NAME_MAX_LENGTH} characters or fewer.`;
  }
  if (lanes.some((lane) => lane.name.toLowerCase() === trimmed.toLowerCase())) {
    return `Lane "${trimmed}" already exists.`;
  }
  return null;
}

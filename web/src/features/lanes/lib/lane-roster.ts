import type { LaneRow } from "@/features/lanes/types";

export const LANE_NAME_MAX_LENGTH = 64;

export const LANE_NAME_SHAPE_HINT =
  "A lane name addresses itself in the URL, so it cannot contain / or \\, control characters, or be . and ..";

export function isAddressableLaneName(name: string): boolean {
  if (name === "." || name === "..") {
    return false;
  }
  return ![...name].some((character) => {
    const code = character.codePointAt(0) ?? 0;
    return code <= 0x1f || (code >= 0x7f && code <= 0x9f);
  });
}

export function validateLaneName(name: string, lanes: LaneRow[]): string | null {
  const trimmed = name.trim();
  if (trimmed.length === 0) {
    return "Lane name is required.";
  }
  if (trimmed.length > LANE_NAME_MAX_LENGTH) {
    return `Lane name must be ${LANE_NAME_MAX_LENGTH} characters or fewer.`;
  }
  if (trimmed.includes("/") || trimmed.includes("\\")) {
    return 'Lane name cannot contain "/" or "\\".';
  }
  if (!isAddressableLaneName(trimmed)) {
    return "Lane name cannot be a bare dot or carry control characters.";
  }
  if (lanes.some((lane) => lane.name.toLowerCase() === trimmed.toLowerCase())) {
    return `Lane "${trimmed}" already exists.`;
  }
  return null;
}

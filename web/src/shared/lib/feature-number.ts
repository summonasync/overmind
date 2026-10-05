export type FeatureNumberStyle = "hash" | "key" | "plain";

export const FEATURE_NUMBER_STYLES: readonly FeatureNumberStyle[] = ["hash", "key", "plain"];

export interface ProjectNumbering {
  style: FeatureNumberStyle;
  prefix: string | null;
}

export const DEFAULT_NUMBERING: ProjectNumbering = { style: "hash", prefix: null };

const FEATURE_NUMBER_PATTERN = /^(\d+[a-z]?)(?:-|$)/;

const FEATURE_NUMBER_PREFIX_PATTERN = /^[A-Z][A-Z0-9_]{1,15}$/;

export function featureNumber(slug: string): string | null {
  const match = FEATURE_NUMBER_PATTERN.exec(slug);
  return match === null ? null : match[1];
}

export function isValidFeatureNumberPrefix(prefix: string): boolean {
  return FEATURE_NUMBER_PREFIX_PATTERN.test(prefix);
}

export function formatFeatureNumber(
  slug: string,
  style: FeatureNumberStyle = "hash",
  prefix: string | null = null,
): string | null {
  const number = featureNumber(slug);
  if (number === null) {
    return null;
  }
  if (style === "plain") {
    return number;
  }
  if (style === "key" && prefix !== null && isValidFeatureNumberPrefix(prefix)) {
    return `${prefix}-${number}`;
  }
  return `#${number}`;
}

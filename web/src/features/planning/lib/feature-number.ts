const FEATURE_NUMBER_PATTERN = /^(\d+)(?:-|$)/;

export function featureNumber(slug: string): string | null {
  const match = FEATURE_NUMBER_PATTERN.exec(slug);
  return match === null ? null : match[1];
}

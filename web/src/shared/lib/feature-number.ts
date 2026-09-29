const FEATURE_NUMBER_PATTERN = /^(\d+[a-z]?)(?:-|$)/;

export function featureNumber(slug: string): string | null {
  const match = FEATURE_NUMBER_PATTERN.exec(slug);
  return match === null ? null : match[1];
}

export function formatFeatureNumber(slug: string): string | null {
  const number = featureNumber(slug);
  return number === null ? null : `#${number}`;
}

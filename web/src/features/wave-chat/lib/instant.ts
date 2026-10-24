const EXPLICIT_ZONE = /(?:Z|[+-]\d{2}:?\d{2})$/i;

export function instantOf(iso: string | null): number {
  if (iso === null) {
    return 0;
  }
  const timestamp = Date.parse(/T/.test(iso) && !EXPLICIT_ZONE.test(iso) ? `${iso}Z` : iso);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

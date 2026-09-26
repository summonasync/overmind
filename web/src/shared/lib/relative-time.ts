const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;
const MONTH = 30 * DAY;

const EXPLICIT_ZONE = /(?:Z|[+-]\d{2}:?\d{2})$/i;

export function parseInstant(iso: string): number | null {
  const normalized = /T/.test(iso) && !EXPLICIT_ZONE.test(iso) ? `${iso}Z` : iso;
  const timestamp = Date.parse(normalized);
  return Number.isNaN(timestamp) ? null : timestamp;
}

export function formatRelativeTime(iso: string): string {
  const timestamp = parseInstant(iso);
  if (timestamp === null) {
    return "";
  }

  const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
  if (seconds < MINUTE) {
    return "just now";
  }
  if (seconds < HOUR) {
    return `${Math.floor(seconds / MINUTE)}m ago`;
  }
  if (seconds < DAY) {
    return `${Math.floor(seconds / HOUR)}h ago`;
  }
  if (seconds < MONTH) {
    return `${Math.floor(seconds / DAY)}d ago`;
  }
  return new Date(timestamp).toLocaleDateString();
}

export function formatBacklogAge(iso: string): string {
  const timestamp = parseInstant(iso);
  if (timestamp === null) {
    return "";
  }
  const seconds = Math.max(0, (Date.now() - timestamp) / 1000);
  if (seconds < HOUR) {
    return `${Math.max(1, Math.round(seconds / MINUTE))}m`;
  }
  if (seconds < DAY) {
    return `${Math.floor(seconds / HOUR)}h`;
  }
  return `${Math.floor(seconds / DAY)}d`;
}

export function formatNotificationTime(iso: string): string {
  const timestamp = parseInstant(iso);
  if (timestamp === null) {
    return "";
  }
  const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
  if (seconds < MINUTE) {
    return "just now";
  }
  if (seconds < HOUR) {
    return `${Math.floor(seconds / MINUTE)}m ago`;
  }
  if (seconds < DAY) {
    return `${Math.floor(seconds / HOUR)}h ago`;
  }
  return `${Math.floor(seconds / DAY)}d ago`;
}

export function formatCalendarDate(iso: string): string {
  const timestamp = parseInstant(iso);
  if (timestamp === null) {
    return "";
  }
  return new Date(timestamp).toLocaleDateString();
}

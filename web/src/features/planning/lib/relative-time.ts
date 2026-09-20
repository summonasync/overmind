const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export function formatRelativeTime(iso: string): string {
  const timestamp = Date.parse(iso);
  if (Number.isNaN(timestamp)) {
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
  if (seconds < 30 * DAY) {
    return `${Math.floor(seconds / DAY)}d ago`;
  }
  return new Date(timestamp).toLocaleDateString();
}

export function formatBacklogAge(iso: string): string {
  const timestamp = Date.parse(iso);
  if (Number.isNaN(timestamp)) {
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

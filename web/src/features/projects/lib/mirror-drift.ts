export interface MirrorDrift {
  total: number;
  failedCount: number;
  files: string[];
  hiddenCount: number;
}

const DETAIL_PATTERN =
  /^Mirror verification failed: (\d+) of (\d+) files drifted or missing at the candidate path\. Files: /;
const MORE_TAIL_PATTERN = / and (\d+) more$/;

export function parseMirrorDrift(detail: string): MirrorDrift | null {
  const summary = DETAIL_PATTERN.exec(detail);
  if (summary === null) {
    return null;
  }

  const listing = detail.slice(summary[0].length).trim();
  let body = listing.endsWith(".") ? listing.slice(0, -1) : listing;

  let hiddenCount = 0;
  const tail = MORE_TAIL_PATTERN.exec(body);
  if (tail !== null) {
    hiddenCount = Number(tail[1]);
    body = body.slice(0, tail.index);
  }

  const files = body.split(", ").filter((part) => part.length > 0);
  if (files.length === 0) {
    return null;
  }

  return {
    total: Number(summary[2]),
    failedCount: Number(summary[1]),
    files,
    hiddenCount,
  };
}

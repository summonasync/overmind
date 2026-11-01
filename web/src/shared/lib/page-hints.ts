export interface PageHint {
  title: string;
  text: string;
}

export const PAGE_HINTS: Record<string, PageHint[]> = {
  "/planning": [
    {
      title: "Numbering",
      text: "Feature slugs carry an NNN prefix that is unique forever — a retired number is never reused, even after the feature is deleted from the board.",
    },
    {
      title: "Validation gates",
      text: "Specs and plans freeze once a human validates them. Agents may only log patched changes or re-run the validation gate.",
    },
    {
      title: "Task lifecycle",
      text: "Tasks move todo → inProgress → review → done. A feature itself closes done only after a human approves its result artifact.",
    },
  ],
  "/lanes": [
    {
      title: "Lane ownership",
      text: "A lane is a named code contour owned by one agent at a time. Claiming a feature inside a lane held by someone else is refused.",
    },
    {
      title: "Claims",
      text: "Claims follow session liveness: a stale claim can be taken over, an alive claim can only receive directives — never be stolen mid-flight.",
    },
  ],
  "/wave-chat": [
    {
      title: "Signal only",
      text: "Wave rooms carry blockers, handoffs and broadcasts — no chatter. A message that @mentions you is one you are expected to read.",
    },
    {
      title: "Marshal calls",
      text: "The marshal reads the whole board across lanes and issues move or wait decisions. A HOLD directive means sync before your next push.",
    },
  ],
};

export function pageHintId(path: string, index: number): string {
  const slug = path
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return `page-hint-${slug}-${index}`;
}

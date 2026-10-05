import { formatFeatureNumber, type ProjectNumbering } from "@/shared/lib/feature-number";

interface FeatureNumberBadgeProps {
  slug: string;
  numbering: ProjectNumbering;
}

export function FeatureNumberBadge({ slug, numbering }: FeatureNumberBadgeProps) {
  const tag = formatFeatureNumber(slug, numbering.style, numbering.prefix);
  if (tag === null) {
    return null;
  }
  return (
    <span className="inline-flex shrink-0 items-center rounded-md border border-primary/40 px-2.5 py-0.5 font-mono text-base font-semibold leading-none text-primary">
      {tag}
    </span>
  );
}

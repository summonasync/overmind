import { formatFeatureNumber } from "@/shared/lib/feature-number";

interface FeatureNumberBadgeProps {
  slug: string;
}

export function FeatureNumberBadge({ slug }: FeatureNumberBadgeProps) {
  const tag = formatFeatureNumber(slug);
  if (tag === null) {
    return null;
  }
  return (
    <span className="inline-flex shrink-0 items-center rounded-md border border-primary/40 px-2.5 py-0.5 font-mono text-base font-semibold leading-none text-primary">
      {tag}
    </span>
  );
}

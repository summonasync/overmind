import { featureNumber } from "@/features/planning/lib/feature-number";

interface FeatureNumberProps {
  slug: string;
}

export function FeatureNumber({ slug }: FeatureNumberProps) {
  const number = featureNumber(slug);
  if (number === null) {
    return null;
  }
  return <span className="mr-1 font-mono text-xs font-semibold text-muted">{number} ·</span>;
}

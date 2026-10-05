import { formatFeatureNumber, type ProjectNumbering } from "@/shared/lib/feature-number";

interface FeatureNumberProps {
  slug: string;
  numbering: ProjectNumbering;
}

export function FeatureNumber({ slug, numbering }: FeatureNumberProps) {
  const tag = formatFeatureNumber(slug, numbering.style, numbering.prefix);
  if (tag === null) {
    return null;
  }
  return <span className="mr-1 font-mono text-xs font-semibold text-muted">{tag} ·</span>;
}

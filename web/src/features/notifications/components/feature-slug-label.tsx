import {
  featureNumber,
  formatFeatureNumber,
  type ProjectNumbering,
} from "@/shared/lib/feature-number";

interface FeatureSlugLabelProps {
  slug: string;
  numbering: ProjectNumbering;
}

export function FeatureSlugLabel({ slug, numbering }: FeatureSlugLabelProps) {
  const number = featureNumber(slug);
  if (number === null) {
    return <>{slug}</>;
  }
  const tag = formatFeatureNumber(slug, numbering.style, numbering.prefix) ?? slug;
  return (
    <>
      <span className="font-mono font-semibold">{tag}</span>
      {slug.slice(number.length)}
    </>
  );
}

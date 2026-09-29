import { featureNumber } from "@/shared/lib/feature-number";

interface FeatureSlugLabelProps {
  slug: string;
}

export function FeatureSlugLabel({ slug }: FeatureSlugLabelProps) {
  const number = featureNumber(slug);
  if (number === null) {
    return <>{slug}</>;
  }
  return (
    <>
      <span className="font-mono font-semibold">{number}</span>
      {slug.slice(number.length)}
    </>
  );
}

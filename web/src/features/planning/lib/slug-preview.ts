import { composeFeatureSlug } from "../../../shared/lib/feature-slug.ts";

interface SlugPreviewInput {
  title: string;
  slug: string;
  takenSlugs: readonly string[];
}

export function slugPreview({ title, slug, takenSlugs }: SlugPreviewInput): string | null {
  if (slug.trim().length > 0) {
    return null;
  }
  const cleanTitle = title.trim();
  if (cleanTitle.length === 0) {
    return null;
  }
  return `would create: ${composeFeatureSlug(takenSlugs, cleanTitle)}`;
}

export function featureHref(projectId: string, slug: string): string {
  const params = new URLSearchParams({ project: projectId, feature: slug });
  return `/dashboard?${params.toString()}`;
}

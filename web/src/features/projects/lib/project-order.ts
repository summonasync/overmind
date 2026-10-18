import type { ProjectResponse } from "@/features/projects/types";

export type ProjectSortField = "name" | "openFeatures";

export function sortProjects(
  projects: ProjectResponse[],
  field: ProjectSortField,
): ProjectResponse[] {
  return [...projects].sort((a, b) => {
    if (a.isFavorite !== b.isFavorite) {
      return a.isFavorite ? -1 : 1;
    }
    if (field === "openFeatures" && a.openFeatureCount !== b.openFeatureCount) {
      return b.openFeatureCount - a.openFeatureCount;
    }
    return a.name.localeCompare(b.name);
  });
}

export function filterFavoriteProjects(
  projects: ProjectResponse[],
  favoritesOnly: boolean,
): ProjectResponse[] {
  return favoritesOnly ? projects.filter((project) => project.isFavorite) : projects;
}

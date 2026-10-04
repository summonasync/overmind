export interface ProjectRef {
  id: string;
  name: string;
}

export const PROJECT_NAME_FALLBACK = "Unknown project";

export function resolvedProjectName(projects: ProjectRef[], projectId: string | null): string | null {
  const id = projectId?.trim() ?? "";
  if (id.length === 0) {
    return null;
  }

  const name = projects.find((project) => project.id === id)?.name.trim() ?? "";
  return name.length > 0 ? name : null;
}

export function projectDisplayName(projects: ProjectRef[], projectId: string): string {
  const id = projectId.trim();
  if (id.length === 0) {
    return PROJECT_NAME_FALLBACK;
  }

  const match = projects.find((project) => project.id === id);
  if (match !== undefined) {
    const name = match.name.trim();
    if (name.length > 0) {
      return name;
    }
  }

  return id.slice(0, 8);
}

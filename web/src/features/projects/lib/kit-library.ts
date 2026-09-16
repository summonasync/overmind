import type { ProjectResponse } from "@/features/projects/types";

const KIT_LIBRARY_PROJECT_NAME = "Kit Library";

export function isKitLibraryProject(project: ProjectResponse): boolean {
  return project.name === KIT_LIBRARY_PROJECT_NAME;
}

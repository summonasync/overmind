"use client";

import { useProjectList } from "@/features/projects/lib/use-projects";
import { projectDisplayName } from "@/shared/lib/project-display";

export function useProjectName(projectId: string): string {
  const { data } = useProjectList();
  return projectDisplayName(data, projectId);
}

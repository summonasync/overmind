"use client";

import { useProjectList } from "@/features/projects/lib/use-projects";
import { DEFAULT_NUMBERING, type ProjectNumbering } from "@/shared/lib/feature-number";

export function useProjectNumbering(projectId: string): ProjectNumbering {
  const { data } = useProjectList();
  const project = data.find((item) => item.id === projectId);
  if (project === undefined) {
    return DEFAULT_NUMBERING;
  }
  return { style: project.numberStyle, prefix: project.numberPrefix };
}

"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateProject } from "@/features/projects/api";
import { projectKeys } from "@/features/projects/queries";
import type { ProjectResponse } from "@/features/projects/types";

export function useProjectMoved(project: ProjectResponse) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (diskPath: string) =>
      updateProject(project.id, {
        name: project.name,
        diskPath,
        description: project.description,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
      queryClient.invalidateQueries({ queryKey: projectKeys.detail(project.id) });
      queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "tree"] });
    },
  });
}

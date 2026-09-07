"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createProject,
  deleteProject,
  updateProject,
} from "@/features/projects/api";
import { projectKeys, projectListQueryOptions } from "@/features/projects/queries";
import type { ProjectFormValues } from "@/features/projects/types";

export function useProjectList() {
  const query = useQuery(projectListQueryOptions());
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function useProjectMutations() {
  const queryClient = useQueryClient();

  const invalidate = () => queryClient.invalidateQueries({ queryKey: projectKeys.lists() });

  const create = useMutation({
    mutationFn: (values: ProjectFormValues) => createProject(values),
    onSuccess: invalidate,
  });

  const update = useMutation({
    mutationFn: ({ id, values }: { id: string; values: ProjectFormValues }) =>
      updateProject(id, values),
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: invalidate,
  });

  return { create, update, remove };
}

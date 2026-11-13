"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createProject,
  deleteProject,
  importProjectManifest,
  setProjectAutoAct,
  setProjectHumanDigest,
  toggleProjectFavorite,
  updateProject,
} from "@/features/projects/api";
import { projectKeys, projectListQueryOptions } from "@/features/projects/queries";
import type {
  ProjectFormValues,
  ProjectManifestImportResponse,
  ProjectUpdateValues,
} from "@/features/projects/types";

export function useProjectList() {
  const query = useQuery(projectListQueryOptions());
  return { data: query.data ?? [], loading: query.isPending, error: query.error, refetch: query.refetch };
}

export function useProjectMutations() {
  const queryClient = useQueryClient();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "tree"] });
  };

  const create = useMutation({
    mutationFn: (values: ProjectFormValues) => createProject(values),
    onSuccess: invalidate,
  });

  const update = useMutation({
    mutationFn: ({ id, values }: { id: string; values: ProjectUpdateValues }) =>
      updateProject(id, values),
    onSuccess: invalidate,
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteProject(id),
    onSuccess: invalidate,
  });

  return { create, update, remove };
}

export function useProjectFavoriteToggle() {
  const queryClient = useQueryClient();

  const toggle = useMutation({
    mutationFn: ({ id, isFavorite }: { id: string; isFavorite: boolean }) =>
      toggleProjectFavorite(id, isFavorite),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
      queryClient.invalidateQueries({ queryKey: [...projectKeys.all, "tree"] });
    },
  });

  return toggle;
}

export function useProjectAutoActToggle() {
  const queryClient = useQueryClient();

  const toggle = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setProjectAutoAct(id, enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });

  return toggle;
}

export function useProjectHumanDigestToggle() {
  const queryClient = useQueryClient();

  const toggle = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      setProjectHumanDigest(id, enabled),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });

  return toggle;
}

export function useProjectManifestImport() {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (id: string) => importProjectManifest(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: projectKeys.lists() });
    },
  });

  return {
    importManifest: (id: string, onSuccess: (result: ProjectManifestImportResponse) => void) =>
      mutation.mutate(id, { onSuccess }),
    sourceFile: mutation.data?.sourceFile ?? null,
    pending: mutation.isPending,
    error: mutation.error as Error | null,
  };
}

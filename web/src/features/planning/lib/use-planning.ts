"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  addPlanningFeatureDependency,
  appendPlanningLog,
  claimPlanningFeature,
  createPlanningArtifact,
  createPlanningFeature,
  declinePlanningArtifact,
  deletePlanningArtifact,
  deletePlanningFeature,
  importPlanningFeature,
  materializePlanning,
  removePlanningFeatureDependency,
  scanPlanningBacklog,
  setPlanningArtifactStatus,
  unclaimPlanningFeature,
  updatePlanningArtifact,
  updatePlanningFeature,
  updatePlanningWorkflow,
  validatePlanningArtifact,
} from "@/features/planning/api";
import { createFeatureWithCollisionRetry } from "@/features/planning/lib/feature-create-retry";
import {
  boardOnceQueryOptions,
  boardQueryOptions,
  driftQueryOptions,
  featureQueryOptions,
  humanInboxQueryOptions,
  planningKeys,
  workflowQueryOptions,
} from "@/features/planning/queries";
import type {
  CreatePlanningArtifactValues,
  PlanningArtifactStatus,
  PlanningFeatureFormValues,
  PlanningFeatureImportReport,
  UpdatePlanningArtifactValues,
  UpdatePlanningFeatureValues,
  UpdatePlanningWorkflowValues,
} from "@/features/planning/types";

export function usePlanningBoard(projectId: string) {
  const query = useQuery(boardQueryOptions(projectId));
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function usePlanningBoardOnce(projectId: string) {
  const query = useQuery(boardOnceQueryOptions(projectId));
  return { data: query.data ?? [], loading: query.isPending, error: query.error };
}

export function usePlanningFeature(projectId: string, slug: string) {
  const query = useQuery(featureQueryOptions(projectId, slug));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function usePlanningMutations(projectId: string) {
  const queryClient = useQueryClient();

  const invalidate = (slug?: string) => {
    queryClient.invalidateQueries({ queryKey: planningKeys.lists() });
    queryClient.invalidateQueries({ queryKey: planningKeys.inbox(projectId) });
    if (slug !== undefined) {
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, slug) });
    }
  };

  const createFeature = useMutation({
    mutationFn: (values: PlanningFeatureFormValues) =>
      createFeatureWithCollisionRetry(projectId, values, createPlanningFeature),
    onSuccess: () => invalidate(),
  });

  const updateFeature = useMutation({
    mutationFn: ({ slug, values }: { slug: string; values: UpdatePlanningFeatureValues }) =>
      updatePlanningFeature(projectId, slug, values),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const deleteFeature = useMutation({
    mutationFn: (slug: string) => deletePlanningFeature(projectId, slug),
    onSuccess: () => invalidate(),
  });

  const claimFeature = useMutation({
    mutationFn: (input: { slug: string; alias: string; vendor: string }) =>
      claimPlanningFeature(projectId, input.slug, input.alias, input.vendor),
    onSuccess: (_result, variables) => {
      queryClient.invalidateQueries({ queryKey: planningKeys.board(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.inbox(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, variables.slug) });
    },
  });

  const unclaimFeature = useMutation({
    mutationFn: (input: { slug: string }) => unclaimPlanningFeature(projectId, input.slug),
    onSuccess: (_result, variables) => {
      queryClient.invalidateQueries({ queryKey: planningKeys.board(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.inbox(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, variables.slug) });
    },
  });

  const addFeatureDependency = useMutation({
    mutationFn: (input: { slug: string; blockedBy: string }) =>
      addPlanningFeatureDependency(projectId, input.slug, input.blockedBy),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: planningKeys.all });
    },
  });

  const removeFeatureDependency = useMutation({
    mutationFn: (input: { slug: string; blockedBy: string }) =>
      removePlanningFeatureDependency(projectId, input.slug, input.blockedBy),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: planningKeys.all });
    },
  });

  const createArtifact = useMutation({
    mutationFn: ({ slug, values }: { slug: string; values: CreatePlanningArtifactValues }) =>
      createPlanningArtifact(projectId, slug, values),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const updateArtifact = useMutation({
    mutationFn: (input: {
      artifactId: string;
      slug: string;
      values: UpdatePlanningArtifactValues;
    }) => updatePlanningArtifact(input.artifactId, input.values),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const deleteArtifact = useMutation({
    mutationFn: (input: { artifactId: string; slug: string }) =>
      deletePlanningArtifact(input.artifactId),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const setArtifactStatus = useMutation({
    mutationFn: (input: {
      artifactId: string;
      slug: string;
      status: PlanningArtifactStatus;
      logNote: string;
    }) => setPlanningArtifactStatus(input.artifactId, input.status, input.logNote),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const appendArtifactLog = useMutation({
    mutationFn: (input: { artifactId: string; slug: string; note: string }) =>
      appendPlanningLog(input.artifactId, input.note),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const validateArtifact = useMutation({
    mutationFn: (input: { artifactId: string; slug: string }) =>
      validatePlanningArtifact(input.artifactId),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  const declineArtifact = useMutation({
    mutationFn: (input: { artifactId: string; slug: string; reason?: string }) =>
      declinePlanningArtifact(input.artifactId, input.reason),
    onSuccess: (_result, variables) => invalidate(variables.slug),
  });

  return {
    createFeature,
    updateFeature,
    deleteFeature,
    claimFeature,
    unclaimFeature,
    addFeatureDependency,
    removeFeatureDependency,
    createArtifact,
    updateArtifact,
    deleteArtifact,
    setArtifactStatus,
    appendArtifactLog,
    validateArtifact,
    declineArtifact,
  };
}

export function usePlanningWorkflow(projectId: string) {
  const query = useQuery(workflowQueryOptions(projectId));
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function useWorkflowMutations(projectId: string) {
  const queryClient = useQueryClient();

  const update = useMutation({
    mutationFn: (values: UpdatePlanningWorkflowValues) => updatePlanningWorkflow(projectId, values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: planningKeys.workflow(projectId) }),
  });

  return { update };
}

export function usePlanningDrift(projectId: string) {
  const query = useQuery(driftQueryOptions(projectId));
  return {
    data: query.data ?? [],
    loading: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}

export function useHumanInbox(projectId: string) {
  const query = useQuery(humanInboxQueryOptions(projectId));
  return {
    items: query.data?.items ?? [],
    loading: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}

export function usePlanningImportScan(projectId: string) {
  const scan = useMutation({
    mutationFn: (subPath?: string) => scanPlanningBacklog(projectId, subPath),
  });

  return {
    report: scan.data ?? null,
    scanning: scan.isPending,
    error: scan.error as Error | null,
    scan: (subPath?: string) => scan.mutate(subPath),
  };
}

export function usePlanningFolderImport(projectId: string) {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: (folder: string) => importPlanningFeature(projectId, folder),
    onSuccess: (report) => {
      queryClient.invalidateQueries({ queryKey: planningKeys.lists() });
      queryClient.invalidateQueries({ queryKey: planningKeys.drift(projectId) });
      queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, report.slug) });
    },
  });

  return {
    importFolder: (folder: string, onSuccess: (report: PlanningFeatureImportReport) => void) =>
      mutation.mutate(folder, { onSuccess }),
    importingFolder: mutation.isPending ? mutation.variables ?? null : null,
    pending: mutation.isPending,
    error: mutation.error as Error | null,
  };
}

export function useMaterializeMutation(projectId: string) {
  const queryClient = useQueryClient();

  const materialize = useMutation({
    mutationFn: (force: boolean) => materializePlanning(projectId, force),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: planningKeys.drift(projectId) }),
  });

  return { materialize };
}

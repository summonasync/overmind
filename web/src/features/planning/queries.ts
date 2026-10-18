import { queryOptions } from "@tanstack/react-query";
import {
  getPlanningFeature,
  getPlanningWorkflow,
  listArtifactVersions,
  listPlanningBoard,
  listPlanningDrift,
} from "@/features/planning/api";
import { LIVE_SYNC_INTERVAL_MS } from "@/shared/lib/live-sync";

export const planningKeys = {
  all: ["planning"] as const,
  lists: () => [...planningKeys.all, "list"] as const,
  board: (projectId: string) => [...planningKeys.lists(), projectId] as const,
  feature: (projectId: string, slug: string) =>
    [...planningKeys.all, "detail", projectId, slug] as const,
  versions: (artifactId: string) => [...planningKeys.all, "artifact", artifactId, "versions"] as const,
  workflow: (projectId: string) => [...planningKeys.all, "workflow", projectId] as const,
  drift: (projectId: string) => [...planningKeys.all, "drift", projectId] as const,
};

export const boardQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: planningKeys.board(projectId),
    queryFn: () => listPlanningBoard(projectId),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const boardOnceQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: planningKeys.board(projectId),
    queryFn: () => listPlanningBoard(projectId),
    enabled: projectId.length > 0,
  });

export const featureQueryOptions = (projectId: string, slug: string) =>
  queryOptions({
    queryKey: planningKeys.feature(projectId, slug),
    queryFn: () => getPlanningFeature(projectId, slug),
    enabled: projectId.length > 0 && slug.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

export const artifactVersionsQueryOptions = (artifactId: string) =>
  queryOptions({
    queryKey: planningKeys.versions(artifactId),
    queryFn: () => listArtifactVersions(artifactId),
    enabled: artifactId.length > 0,
  });

export const workflowQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: planningKeys.workflow(projectId),
    queryFn: () => getPlanningWorkflow(projectId),
    enabled: projectId.length > 0,
  });

export const driftQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: planningKeys.drift(projectId),
    queryFn: () => listPlanningDrift(projectId),
    enabled: projectId.length > 0,
    refetchInterval: LIVE_SYNC_INTERVAL_MS,
  });

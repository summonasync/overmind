import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type {
  CreatePlanningArtifactValues,
  PlanningArtifactResponse,
  PlanningArtifactStatus,
  PlanningArtifactVersionResponse,
  PlanningDriftItem,
  PlanningFeatureBoardItem,
  PlanningFeatureFormValues,
  PlanningFeatureResponse,
  PlanningMaterializationReport,
  PlanningWorkflowResponse,
  UpdatePlanningArtifactValues,
  UpdatePlanningFeatureValues,
  UpdatePlanningWorkflowValues,
} from "@/features/planning/types";

export function listPlanningBoard(projectId: string): Promise<PlanningFeatureBoardItem[]> {
  return apiGet<PlanningFeatureBoardItem[]>(`/projects/${projectId}/planning/features`);
}

export function getPlanningFeature(projectId: string, slug: string): Promise<PlanningFeatureResponse> {
  return apiGet<PlanningFeatureResponse>(`/projects/${projectId}/planning/features/${slug}`);
}

export function createPlanningFeature(
  projectId: string,
  values: PlanningFeatureFormValues,
): Promise<PlanningFeatureResponse> {
  return apiPost<PlanningFeatureResponse>(`/projects/${projectId}/planning/features`, {
    slug: values.slug,
    title: values.title,
    body: values.body.trim().length > 0 ? values.body : null,
  });
}

export function updatePlanningFeature(
  projectId: string,
  slug: string,
  values: UpdatePlanningFeatureValues,
): Promise<PlanningFeatureResponse> {
  return apiPut<PlanningFeatureResponse>(`/projects/${projectId}/planning/features/${slug}`, {
    title: values.title ?? null,
    body: values.body ?? null,
    status: values.status ?? null,
  });
}

export function deletePlanningFeature(projectId: string, slug: string): Promise<void> {
  return apiDelete(`/projects/${projectId}/planning/features/${slug}`);
}

export function getPlanningArtifact(artifactId: string): Promise<PlanningArtifactResponse> {
  return apiGet<PlanningArtifactResponse>(`/planning/artifacts/${artifactId}`);
}

export function listArtifactVersions(artifactId: string): Promise<PlanningArtifactVersionResponse[]> {
  return apiGet<PlanningArtifactVersionResponse[]>(`/planning/artifacts/${artifactId}/versions`);
}

export function createPlanningArtifact(
  projectId: string,
  slug: string,
  values: CreatePlanningArtifactValues,
): Promise<PlanningArtifactResponse> {
  return apiPost<PlanningArtifactResponse>(
    `/projects/${projectId}/planning/features/${slug}/artifacts`,
    {
      kind: values.kind,
      body: values.body,
      order: values.order ?? null,
      review: values.review ?? null,
      logNote: values.logNote ?? null,
    },
  );
}

export function updatePlanningArtifact(
  artifactId: string,
  values: UpdatePlanningArtifactValues,
): Promise<PlanningArtifactResponse> {
  return apiPut<PlanningArtifactResponse>(`/planning/artifacts/${artifactId}`, {
    body: values.body,
    logNote: values.logNote ?? null,
  });
}

export function deletePlanningArtifact(artifactId: string): Promise<void> {
  return apiDelete(`/planning/artifacts/${artifactId}`);
}

export function setPlanningArtifactStatus(
  artifactId: string,
  status: PlanningArtifactStatus,
  logNote: string,
): Promise<PlanningArtifactResponse> {
  return apiPost<PlanningArtifactResponse>(`/planning/artifacts/${artifactId}/status`, {
    status,
    logNote,
  });
}

export function appendPlanningLog(artifactId: string, note: string): Promise<PlanningArtifactResponse> {
  return apiPost<PlanningArtifactResponse>(`/planning/artifacts/${artifactId}/log`, { note });
}

export function validatePlanningArtifact(artifactId: string): Promise<PlanningArtifactResponse> {
  return apiPost<PlanningArtifactResponse>(`/planning/artifacts/${artifactId}/validate`);
}

export function getPlanningWorkflow(projectId: string): Promise<PlanningWorkflowResponse> {
  return apiGet<PlanningWorkflowResponse>(`/projects/${projectId}/planning/workflow`);
}

export function updatePlanningWorkflow(
  projectId: string,
  values: UpdatePlanningWorkflowValues,
): Promise<PlanningWorkflowResponse> {
  return apiPut<PlanningWorkflowResponse>(`/projects/${projectId}/planning/workflow`, {
    materialization: values.materialization ?? null,
    requireHumanValidation: values.requireHumanValidation ?? null,
  });
}

export function listPlanningDrift(projectId: string): Promise<PlanningDriftItem[]> {
  return apiGet<PlanningDriftItem[]>(`/projects/${projectId}/planning/drift`);
}

export function materializePlanning(projectId: string, force: boolean): Promise<PlanningMaterializationReport> {
  return apiPost<PlanningMaterializationReport>(`/projects/${projectId}/planning/materialize`, {
    force,
  });
}

import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type {
  ClaimSettingsResponse,
  ProjectAutoActResponse,
  ProjectFormValues,
  ProjectManifestImportResponse,
  ProjectResponse,
  ProjectUpdateValues,
} from "@/features/projects/types";

export function listProjects(): Promise<ProjectResponse[]> {
  return apiGet<ProjectResponse[]>("/projects/");
}

export function getProject(id: string): Promise<ProjectResponse> {
  return apiGet<ProjectResponse>(`/projects/${id}`);
}

export function createProject(values: ProjectFormValues): Promise<ProjectResponse> {
  return apiPost<ProjectResponse>("/projects/", {
    name: values.name,
    diskPath: values.diskPath,
    description: values.description || null,
  });
}

export function updateProject(id: string, values: ProjectUpdateValues): Promise<ProjectResponse> {
  return apiPut<ProjectResponse>(`/projects/${id}`, {
    name: values.name,
    diskPath: values.diskPath,
    description: values.description || null,
    ...(values.setManifest ? { manifest: values.manifest ?? null, setManifest: true } : {}),
    ...(values.numberStyle !== undefined
      ? {
          numberStyle: values.numberStyle,
          ...(values.numberStyle === "key" && values.numberPrefix !== undefined
            ? { numberPrefix: values.numberPrefix }
            : {}),
        }
      : {}),
  });
}

export function toggleProjectFavorite(id: string, isFavorite: boolean): Promise<ProjectResponse> {
  return apiPost<ProjectResponse>(`/projects/${id}/favorite`, { isFavorite });
}

export function setProjectAutoAct(
  id: string,
  enabled: boolean,
): Promise<ProjectAutoActResponse> {
  return apiPost<ProjectAutoActResponse>(`/projects/${id}/auto-act`, { enabled });
}

export function importProjectManifest(id: string): Promise<ProjectManifestImportResponse> {
  return apiPost<ProjectManifestImportResponse>(`/projects/${id}/manifest/import`);
}

export function deleteProject(id: string): Promise<void> {
  return apiDelete(`/projects/${id}`);
}

export function getClaimSettings(id: string): Promise<ClaimSettingsResponse> {
  return apiGet<ClaimSettingsResponse>(`/projects/${id}/claim-settings`);
}

export function updateClaimSettings(
  id: string,
  values: ClaimSettingsResponse,
): Promise<ClaimSettingsResponse> {
  return apiPut<ClaimSettingsResponse>(`/projects/${id}/claim-settings`, {
    claimStaleThresholdMinutes: values.claimStaleThresholdMinutes,
    agentNamingRules: values.agentNamingRules,
  });
}

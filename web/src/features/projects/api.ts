import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type {
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
  });
}

export function importProjectManifest(id: string): Promise<ProjectManifestImportResponse> {
  return apiPost<ProjectManifestImportResponse>(`/projects/${id}/manifest/import`);
}

export function deleteProject(id: string): Promise<void> {
  return apiDelete(`/projects/${id}`);
}

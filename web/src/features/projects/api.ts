import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { ProjectFormValues, ProjectResponse } from "@/features/projects/types";

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

export function updateProject(id: string, values: ProjectFormValues): Promise<ProjectResponse> {
  return apiPut<ProjectResponse>(`/projects/${id}`, {
    name: values.name,
    diskPath: values.diskPath,
    description: values.description || null,
  });
}

export function deleteProject(id: string): Promise<void> {
  return apiDelete(`/projects/${id}`);
}

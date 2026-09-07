import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type {
  DriftItemResponse,
  KitFormValues,
  KitResponse,
  MaterializationReportResponse,
} from "@/features/kits/types";

export function listKits(): Promise<KitResponse[]> {
  return apiGet<KitResponse[]>("/kits");
}

export function createKit(values: KitFormValues): Promise<KitResponse> {
  return apiPost<KitResponse>("/kits", {
    name: values.name,
    version: values.version,
    description: values.description || null,
  });
}

export function updateKit(id: string, values: KitFormValues): Promise<KitResponse> {
  return apiPut<KitResponse>(`/kits/${id}`, {
    name: values.name,
    version: values.version,
    description: values.description || null,
  });
}

export function deleteKit(id: string): Promise<void> {
  return apiDelete(`/kits/${id}`);
}

export function addKitEntry(kitId: string, entryId: string): Promise<KitResponse> {
  return apiPost<KitResponse>(`/kits/${kitId}/entries/${entryId}`);
}

export function removeKitEntry(kitId: string, entryId: string): Promise<void> {
  return apiDelete(`/kits/${kitId}/entries/${entryId}`);
}

export function activateKit(projectId: string, kitId: string): Promise<MaterializationReportResponse> {
  return apiPost<MaterializationReportResponse>(`/projects/${projectId}/kits/${kitId}/activate`);
}

export function deactivateKit(projectId: string, kitId: string): Promise<MaterializationReportResponse> {
  return apiPost<MaterializationReportResponse>(`/projects/${projectId}/kits/${kitId}/deactivate`);
}

export function getDrift(projectId: string): Promise<DriftItemResponse[]> {
  return apiGet<DriftItemResponse[]>(`/projects/${projectId}/drift`);
}

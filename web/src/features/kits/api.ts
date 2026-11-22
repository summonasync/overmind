import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type {
  DriftItemResponse,
  GlobalDeployReportResponse,
  GlobalKitStateItemResponse,
  KitFormValues,
  KitImportReportResponse,
  KitImportSelectionRequest,
  KitReimportReportResponse,
  KitResponse,
  KitScanPreviewResponse,
  MaterializationReportResponse,
} from "@/features/kits/types";

export function listKits(): Promise<KitResponse[]> {
  return apiGet<KitResponse[]>("/kits");
}

export function scanKitFolder(path: string): Promise<KitScanPreviewResponse> {
  return apiPost<KitScanPreviewResponse>("/kits/import/scan", { path });
}

export function importKitFolder(
  path: string,
  kits: KitImportSelectionRequest[],
): Promise<KitImportReportResponse> {
  return apiPost<KitImportReportResponse>("/kits/import", { path, kits });
}

export function reimportKit(id: string): Promise<KitReimportReportResponse> {
  return apiPost<KitReimportReportResponse>(`/kits/${id}/reimport`);
}

export function clearKitSource(id: string): Promise<KitResponse> {
  return apiPost<KitResponse>(`/kits/${id}/clear-source`);
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

export function deleteKit(id: string, deployedFiles?: "keep" | "remove"): Promise<void> {
  return apiDelete(`/kits/${id}${deployedFiles ? `?deployedFiles=${deployedFiles}` : ""}`);
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

export function getGlobalKitState(): Promise<GlobalKitStateItemResponse[]> {
  return apiGet<GlobalKitStateItemResponse[]>("/kits/global/state");
}

export function deployKitGlobal(
  kitId: string,
  agent: string,
  force: boolean,
): Promise<GlobalDeployReportResponse> {
  return apiPost<GlobalDeployReportResponse>(`/kits/${kitId}/global/${agent}?force=${force}`);
}

export function removeKitGlobal(kitId: string, agent: string): Promise<GlobalDeployReportResponse> {
  return apiDelete<GlobalDeployReportResponse>(`/kits/${kitId}/global/${agent}`);
}

import { apiDelete, apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { LaneMutableFields, LaneUpsertInput, LanesRoster } from "@/features/lanes/types";

export function listLanes(projectId: string): Promise<LanesRoster> {
  return apiGet<LanesRoster>(`/projects/${projectId}/planning/lanes`);
}

export function setLanes(projectId: string, lanes: LaneUpsertInput[]): Promise<LanesRoster> {
  return apiPut<LanesRoster>(`/projects/${projectId}/planning/lanes`, { lanes });
}

export function createLane(projectId: string, lane: LaneUpsertInput): Promise<LanesRoster> {
  return apiPost<LanesRoster>(`/projects/${projectId}/planning/lanes`, lane);
}

export function updateLane(projectId: string, lane: LaneUpsertInput): Promise<LanesRoster> {
  return apiPut<LanesRoster>(lanePath(projectId, lane.name), laneFields(lane));
}

export function deleteLane(projectId: string, name: string): Promise<LanesRoster> {
  return apiDelete<LanesRoster>(lanePath(projectId, name));
}

function lanePath(projectId: string, name: string): string {
  return `/projects/${projectId}/planning/lanes/${encodeURIComponent(name)}`;
}

function laneFields(lane: LaneUpsertInput): LaneMutableFields {
  return {
    contour: lane.contour,
    serialOrder: lane.serialOrder,
    priority: lane.priority,
  };
}

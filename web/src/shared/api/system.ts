import { apiDelete, apiGet, apiPost } from "@/shared/api/client";
import type {
  BrowseRootsResponse,
  ListDirectoryResponse,
  TreeRootsResponse,
} from "@/shared/api/types";

export function browseRoots(): Promise<BrowseRootsResponse> {
  return apiGet<BrowseRootsResponse>("/system/browse-roots");
}

export function getTreeRoots(): Promise<TreeRootsResponse> {
  return apiGet<TreeRootsResponse>("/system/tree-roots");
}

export function addTreeRoot(path: string): Promise<TreeRootsResponse> {
  return apiPost<TreeRootsResponse>("/system/tree-roots", { path });
}

export function removeTreeRoot(path: string, force = false): Promise<TreeRootsResponse> {
  const forceParam = force ? "&force=true" : "";
  return apiDelete<TreeRootsResponse>(`/system/tree-roots?path=${encodeURIComponent(path)}${forceParam}`);
}

export function listDirectory(path: string): Promise<ListDirectoryResponse> {
  return apiGet<ListDirectoryResponse>(`/system/list-directory?path=${encodeURIComponent(path)}`);
}

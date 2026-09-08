import { apiDelete, apiGet, apiPost } from "@/shared/api/client";
import type {
  BrowseFolderResponse,
  ListDirectoryResponse,
  TreeRootsResponse,
} from "@/shared/api/types";

export function browseFolder(): Promise<BrowseFolderResponse | undefined> {
  return apiPost<BrowseFolderResponse | undefined>("/system/browse-folder");
}

export function getTreeRoots(): Promise<TreeRootsResponse> {
  return apiGet<TreeRootsResponse>("/system/tree-roots");
}

export function addTreeRoot(path: string): Promise<TreeRootsResponse> {
  return apiPost<TreeRootsResponse>("/system/tree-roots", { path });
}

export function removeTreeRoot(path: string): Promise<TreeRootsResponse> {
  return apiDelete<TreeRootsResponse>(`/system/tree-roots?path=${encodeURIComponent(path)}`);
}

export function listDirectory(path: string): Promise<ListDirectoryResponse> {
  return apiGet<ListDirectoryResponse>(`/system/list-directory?path=${encodeURIComponent(path)}`);
}

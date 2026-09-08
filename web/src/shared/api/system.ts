import { apiGet, apiPost } from "@/shared/api/client";
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

export function listDirectory(path: string): Promise<ListDirectoryResponse> {
  return apiGet<ListDirectoryResponse>(`/system/list-directory?path=${encodeURIComponent(path)}`);
}

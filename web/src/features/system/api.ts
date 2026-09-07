import { apiPost } from "@/shared/api/client";
import type { BrowseFolderResponse } from "@/shared/api/types";

export function browseFolder(): Promise<BrowseFolderResponse | undefined> {
  return apiPost<BrowseFolderResponse | undefined>("/system/browse-folder");
}

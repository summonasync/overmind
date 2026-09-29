import { apiGet } from "@/shared/api/client";
import type { CeremoniesResponse } from "@/features/attention/types";

export function listCeremonies(): Promise<CeremoniesResponse> {
  return apiGet<CeremoniesResponse>("/attention/ceremonies");
}

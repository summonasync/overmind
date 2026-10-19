import { apiGet, apiPost } from "@/shared/api/client";
import type {
  GrimoireAssistedInstallResponse,
  GrimoireDisconnectResponse,
  GrimoireStatusResponse,
  GrimoireWireInput,
  GrimoireWireResponse,
} from "@/features/grimoire/types";

export function getGrimoireStatus(): Promise<GrimoireStatusResponse> {
  return apiGet<GrimoireStatusResponse>("/grimoire/status");
}

export function wireGrimoire(input: GrimoireWireInput): Promise<GrimoireWireResponse> {
  return apiPost<GrimoireWireResponse>("/grimoire/wire", {
    bookName: input.bookName,
    overridePath: input.overridePath,
  });
}

export function runAssistedInstall(channel: string | null): Promise<GrimoireAssistedInstallResponse> {
  return apiPost<GrimoireAssistedInstallResponse>("/grimoire/assisted-install", { channel });
}

export function disconnectGrimoire(): Promise<GrimoireDisconnectResponse> {
  return apiPost<GrimoireDisconnectResponse>("/grimoire/disconnect");
}

import { apiGet, apiPost } from "@/shared/api/client";
import type {
  GrimoireAssistedInstallInput,
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

export function runAssistedInstall(input: GrimoireAssistedInstallInput): Promise<GrimoireAssistedInstallResponse> {
  return apiPost<GrimoireAssistedInstallResponse>("/grimoire/assisted-install", {
    intent: input.intent,
    channel: input.channel,
  });
}

export function disconnectGrimoire(): Promise<GrimoireDisconnectResponse> {
  return apiPost<GrimoireDisconnectResponse>("/grimoire/disconnect");
}

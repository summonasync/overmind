import { readJson, writeJson } from "@/shared/lib/session-json";

const OWNER_ALIAS_KEY = "overmind.wave-chat.ownerAlias";

export const DEFAULT_OWNER_ALIAS = "owner";

export function loadOwnerAlias(): string {
  const stored = readJson<unknown>(OWNER_ALIAS_KEY, DEFAULT_OWNER_ALIAS);
  return typeof stored === "string" && stored.trim().length > 0 ? stored.trim() : DEFAULT_OWNER_ALIAS;
}

export function saveOwnerAlias(alias: string): void {
  const trimmed = alias.trim();
  writeJson(OWNER_ALIAS_KEY, trimmed.length > 0 ? trimmed : DEFAULT_OWNER_ALIAS);
}

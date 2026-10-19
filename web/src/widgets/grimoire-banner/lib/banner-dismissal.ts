import { readJson, writeJson } from "@/shared/lib/session-json";

const DISMISSAL_KEY = "overmind.grimoire.bannerDismissed";

export function isGrimoireBannerDismissed(): boolean {
  return readJson<unknown>(DISMISSAL_KEY, false) === true;
}

export function dismissGrimoireBanner(): void {
  writeJson(DISMISSAL_KEY, true);
}

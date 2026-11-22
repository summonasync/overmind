// Test shim, not production code. `shared/ui/zerling` imports the sprite PNGs, which `node
// --test` cannot load as modules, so any journal component that mounts LoadingState or ErrorText
// dies at import time before a single assert runs. This file answers the same barrel with plain
// strings, and `web/tests/journal-graph.test.mts` points the specifier at it through the loader
// alias. Nothing outside the test tree reads it.
export type ZerlingPose =
  | "angry"
  | "celebrating"
  | "confused"
  | "love"
  | "menacing"
  | "silly"
  | "sleeping"
  | "smug"
  | "surprised"
  | "yelling";

export const ZERLING_SPRITES: Record<ZerlingPose, string> = {
  angry: "stub://angry",
  celebrating: "stub://celebrating",
  confused: "stub://confused",
  love: "stub://love",
  menacing: "stub://menacing",
  silly: "stub://silly",
  sleeping: "stub://sleeping",
  smug: "stub://smug",
  surprised: "stub://surprised",
  yelling: "stub://yelling",
};

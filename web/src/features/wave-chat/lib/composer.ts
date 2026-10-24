export const WAVE_TEXT_MAX_LENGTH = 500;

export type ComposerValidation =
  | { valid: true; text: string }
  | { valid: false; reason: string };

export function validateComposerText(text: string): ComposerValidation {
  const trimmed = text.trim();
  if (trimmed.length === 0) {
    return { valid: false, reason: "Write a message before sending." };
  }
  if (trimmed.length > WAVE_TEXT_MAX_LENGTH) {
    return { valid: false, reason: `Messages are capped at ${WAVE_TEXT_MAX_LENGTH} characters.` };
  }
  return { valid: true, text: trimmed };
}

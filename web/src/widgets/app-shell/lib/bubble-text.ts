export const BUBBLE_TEXT_LIMIT = 800;

const ELLIPSIS = "…";

export function bubbleText(text: string): string {
  return text.length > BUBBLE_TEXT_LIMIT ? `${text.slice(0, BUBBLE_TEXT_LIMIT)}${ELLIPSIS}` : text;
}

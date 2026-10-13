export const BUBBLE_TEXT_LIMIT = 800;

const ELLIPSIS = "…";

const graphemes = new Intl.Segmenter(undefined, { granularity: "grapheme" });

export function bubbleText(text: string): string {
  if (text.length <= BUBBLE_TEXT_LIMIT) {
    return text;
  }
  let shown = "";
  for (const { segment } of graphemes.segment(text)) {
    if (shown.length + segment.length > BUBBLE_TEXT_LIMIT) {
      break;
    }
    shown += segment;
  }
  return `${shown}${ELLIPSIS}`;
}

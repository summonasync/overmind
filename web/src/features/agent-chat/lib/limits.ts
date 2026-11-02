export const AGENT_MESSAGE_MAX_LENGTH = 32_000;
export const AGENT_TITLE_MAX_LENGTH = 200;
export const AGENT_TITLE_PREVIEW_LENGTH = 80;
export const AGENT_HISTORY_PAGE_LIMIT = 50;
export const AGENT_SCROLL_BOTTOM_THRESHOLD = 96;

export function agentTitlePreview(text: string): string | null {
  const collapsed = text.replace(/\s+/g, " ").trim();
  if (collapsed.length === 0) {
    return null;
  }
  const preview = collapsed.slice(0, AGENT_TITLE_PREVIEW_LENGTH);
  return preview.length === 0 ? null : preview;
}

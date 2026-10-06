const TOAST_BODY_LIMIT = 240;

const ELLIPSIS = "…";

export interface AttentionToastCopy {
  title: string;
  body: string;
}

export interface AttentionToastEvent {
  agent: string;
  featureSlug?: string | null;
  text: string;
}

export function attentionToastCopy(
  projectName: string | null,
  event: AttentionToastEvent,
): AttentionToastCopy {
  const origin = [event.featureSlug ?? null, event.agent]
    .filter((part) => part !== null && part.length > 0)
    .join(" · ");
  const text =
    event.text.length > TOAST_BODY_LIMIT
      ? `${event.text.slice(0, TOAST_BODY_LIMIT)}${ELLIPSIS}`
      : event.text;
  return {
    title: projectName ?? "Overmind",
    body: origin.length > 0 ? `${origin}\n${text}` : text,
  };
}

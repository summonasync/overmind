"use client";

const BADGE_COLOR = "#f59e0b";

let defaultHref: string | null = null;
let created = false;

export function attentionFaviconSvg(baseHref: string | null): string {
  const badge = `<circle cx="47" cy="47" r="17" fill="${BADGE_COLOR}" stroke="rgba(0,0,0,0.35)" stroke-width="3"/>`;
  const image = baseHref
    ? `<image href="${baseHref}" width="64" height="64"/>`
    : "";
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${image}${badge}</svg>`;
}

function iconLink(): HTMLLinkElement {
  const existing = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
  if (existing !== null) {
    return existing;
  }
  const link = document.createElement("link");
  link.rel = "icon";
  document.head.append(link);
  created = true;
  return link;
}

export function defaultFaviconHref(): string | null {
  if (defaultHref !== null) {
    return defaultHref;
  }
  const href = document.querySelector<HTMLLinkElement>('link[rel="icon"]')?.getAttribute("href") ?? null;
  defaultHref = href !== null && href.length > 0 ? href : null;
  return defaultHref;
}

export function setAttentionFavicon(on: boolean): void {
  const link = iconLink();
  if (!on) {
    const base = defaultFaviconHref();
    if (base !== null) {
      link.href = base;
    } else if (created) {
      link.remove();
    }
    return;
  }
  link.href = `data:image/svg+xml,${encodeURIComponent(attentionFaviconSvg(defaultFaviconHref()))}`;
}

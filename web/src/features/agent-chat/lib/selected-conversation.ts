"use client";

const STORAGE_KEY = "overmind.agent-chat.selected-conversation";

export function readSelectedConversationId(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  let raw: string | null;
  try {
    raw = window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
  return raw !== null && raw.length > 0 ? raw : null;
}

export function writeSelectedConversationId(conversationId: string | null): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    if (conversationId === null) {
      window.localStorage.removeItem(STORAGE_KEY);
    } else {
      window.localStorage.setItem(STORAGE_KEY, conversationId);
    }
  } catch {
    return;
  }
}

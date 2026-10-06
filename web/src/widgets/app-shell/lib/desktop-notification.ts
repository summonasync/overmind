"use client";

import type { DesktopPermission } from "@/widgets/app-shell/lib/attention-plan";

interface DesktopToast {
  onclick: ((event: Event) => void) | null;
  close(): void;
}

interface DesktopToastCtor {
  new (
    title: string,
    options: { body?: string; tag?: string; icon?: string; silent?: boolean },
  ): DesktopToast;
  readonly permission: "granted" | "denied" | "default";
  requestPermission(): Promise<"granted" | "denied" | "default">;
}

function toastCtor(): DesktopToastCtor | null {
  return typeof window === "undefined" || typeof Notification === "undefined" ? null : Notification;
}

export function desktopNotificationsSupported(): boolean {
  return toastCtor() !== null;
}

export function desktopPermission(): DesktopPermission {
  return toastCtor()?.permission ?? "unsupported";
}

export async function requestDesktopPermission(): Promise<DesktopPermission> {
  const Ctor = toastCtor();
  if (Ctor === null) {
    return "unsupported";
  }
  try {
    return await Ctor.requestPermission();
  } catch {
    return Ctor.permission === "granted" ? "granted" : "denied";
  }
}

export interface DesktopToastPayload {
  title: string;
  body: string;
  tag: string;
  icon: string | null;
  onClick: () => void;
}

export function showDesktopNotification(payload: DesktopToastPayload): boolean {
  const Ctor = toastCtor();
  if (Ctor === null || Ctor.permission !== "granted") {
    return false;
  }
  try {
    const toast = new Ctor(payload.title, {
      body: payload.body,
      tag: payload.tag,
      silent: true,
      ...(payload.icon === null ? {} : { icon: payload.icon }),
    });
    toast.onclick = () => {
      payload.onClick();
      toast.close();
    };
    return true;
  } catch {
    return false;
  }
}

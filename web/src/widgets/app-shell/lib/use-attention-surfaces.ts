"use client";

import { useCallback, useEffect, useMemo, useRef, useSyncExternalStore } from "react";
import {
  isAwaitingNotification,
  notificationHref,
  useNotifications,
} from "@/features/notifications";
import { useProjectList } from "@/features/projects";
import { projectDisplayName } from "@/shared/lib/project-display";
import { attentionDecision } from "@/widgets/app-shell/lib/attention-plan";
import { defaultFaviconHref, setAttentionFavicon } from "@/widgets/app-shell/lib/attention-favicon";
import {
  attentionSeen,
  clearAttentionSurfaces,
  markAttentionSeen,
  primeAttentionSeen,
  pruneAttentionAgainst,
  raiseAttentionFor,
  useAttentionCount,
} from "@/widgets/app-shell/lib/attention-store";
import { playChirp } from "@/widgets/app-shell/lib/chirp";
import { desktopPermission, showDesktopNotification } from "@/widgets/app-shell/lib/desktop-notification";
import { attentionToastCopy } from "@/widgets/app-shell/lib/attention-toast-copy";
import { useAttentionPrefs } from "@/widgets/app-shell/lib/use-attention-prefs";

function useDocumentHidden(): boolean {
  const subscribe = useCallback((onChange: () => void) => {
    document.addEventListener("visibilitychange", onChange);
    return () => document.removeEventListener("visibilitychange", onChange);
  }, []);
  return useSyncExternalStore(
    subscribe,
    () => document.hidden,
    () => false,
  );
}

export function useAttentionSurfaces(): void {
  const { items, isPending, error } = useNotifications();
  const { prefs } = useAttentionPrefs();
  const { data: projects } = useProjectList();
  const hidden = useDocumentHidden();
  const attention = useAttentionCount();
  const primedRef = useRef(false);

  const awaiting = useMemo(() => items.filter(isAwaitingNotification), [items]);
  const awaitingIds = useMemo(() => awaiting.map((item) => item.id), [awaiting]);

  useEffect(() => {
    pruneAttentionAgainst(awaitingIds);
  }, [awaitingIds]);

  useEffect(() => {
    if (isPending || error) {
      return;
    }
    if (!primedRef.current) {
      primedRef.current = true;
      primeAttentionSeen(awaitingIds);
      return;
    }
    const permission = desktopPermission();
    for (const item of awaiting) {
      if (attentionSeen(item.id)) {
        continue;
      }
      const decision = attentionDecision(
        { id: item.id, kind: item.kind },
        false,
        {
          hidden: document.hidden,
          desktopEnabled: prefs.desktop,
          chirpEnabled: prefs.chirp,
          permission,
        },
      );
      markAttentionSeen(item.id);
      if (!decision.raise) {
        continue;
      }
      raiseAttentionFor(item.id);
      if (decision.chirp) {
        playChirp();
      }
      if (decision.toast) {
        const copy = attentionToastCopy(
          item.projectId ? projectDisplayName(projects ?? [], item.projectId) : null,
          item,
        );
        const url = notificationHref(item);
        showDesktopNotification({
          title: copy.title,
          body: copy.body,
          tag: item.id,
          icon: defaultFaviconHref(),
          onClick: () => {
            clearAttentionSurfaces();
            window.focus();
            window.location.assign(url);
          },
        });
      }
    }
  }, [awaiting, awaitingIds, isPending, error, prefs.desktop, prefs.chirp, projects]);

  useEffect(() => {
    const clear = () => {
      if (!document.hidden) {
        clearAttentionSurfaces();
      }
    };
    document.addEventListener("visibilitychange", clear);
    window.addEventListener("focus", clear);
    return () => {
      document.removeEventListener("visibilitychange", clear);
      window.removeEventListener("focus", clear);
    };
  }, []);

  useEffect(() => {
    setAttentionFavicon(hidden && attention > 0);
  }, [hidden, attention]);
}

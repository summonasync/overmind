"use client";

import { useCallback, useSyncExternalStore } from "react";
import {
  dismissMarshalHint,
  marshalHintDismissed,
  subscribeMarshalHint,
} from "@/widgets/marshal-hint/lib/marshal-hint-prefs";

interface MarshalHintApi {
  dismissed: boolean;
  dismiss: () => void;
}

export function useMarshalHint(): MarshalHintApi {
  const dismissed = useSyncExternalStore(
    subscribeMarshalHint,
    marshalHintDismissed,
    () => false,
  );

  const dismiss = useCallback(() => dismissMarshalHint(), []);

  return { dismissed, dismiss };
}

"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { disconnectGrimoire, runAssistedInstall, wireGrimoire } from "@/features/grimoire/api";
import { grimoireKeys } from "@/features/grimoire/queries";

export function useGrimoireMutations() {
  const queryClient = useQueryClient();

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: grimoireKeys.all });
  };

  const wire = useMutation({ mutationFn: wireGrimoire, onSuccess: invalidate });

  const assisted = useMutation({
    mutationFn: runAssistedInstall,
    onSuccess: (result) => {
      if (!result.degraded) {
        invalidate();
      }
    },
  });

  const disconnect = useMutation({ mutationFn: disconnectGrimoire, onSuccess: invalidate });

  return { wire, assisted, disconnect };
}

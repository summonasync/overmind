"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { clearKitSource, importKitFolder, reimportKit, scanKitFolder } from "@/features/kits/api";
import { kitKeys } from "@/features/kits/queries";
import type { KitImportSelectionRequest } from "@/features/kits/types";

export function useKitImport() {
  const queryClient = useQueryClient();

  const invalidateKits = () => queryClient.invalidateQueries({ queryKey: kitKeys.all });

  const scan = useMutation({
    mutationFn: (path: string) => scanKitFolder(path),
  });

  const runImport = useMutation({
    mutationFn: ({ path, kits }: { path: string; kits: KitImportSelectionRequest[] }) =>
      importKitFolder(path, kits),
    onSuccess: invalidateKits,
  });

  const reimport = useMutation({
    mutationFn: (kitId: string) => reimportKit(kitId),
    onSuccess: invalidateKits,
  });

  const clearSource = useMutation({
    mutationFn: (kitId: string) => clearKitSource(kitId),
    onSuccess: invalidateKits,
  });

  return { scan, runImport, reimport, clearSource };
}

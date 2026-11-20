"use client";

import { ErrorText } from "@/shared/ui/error-text";
import { useProjectList } from "@/features/projects";
import type { KitResponse } from "@/features/kits";
import { useKitMutations } from "@/features/kits";
import { useAllEntries } from "@/widgets/kit-workspace/lib/use-all-entries";
import { KitActivationSection } from "@/widgets/kit-workspace/ui/kit-activation-section";
import { KitDriftSection } from "@/widgets/kit-workspace/ui/kit-drift-section";
import { KitEntriesSection } from "@/widgets/kit-workspace/ui/kit-entries-section";
import { KitGlobalMandatorySection } from "@/widgets/kit-workspace/ui/kit-global-mandatory-section";
import { KitSourceSection } from "@/widgets/kit-workspace/ui/kit-source-section";

interface KitDetailPanelProps {
  kit: KitResponse;
}

export function KitDetailPanel({ kit }: KitDetailPanelProps) {
  const entryMap = useAllEntries();
  const { data: projects } = useProjectList();
  const mutations = useKitMutations();

  // Activation errors render inside the activation section with reframed copy.
  const actionError = mutations.addEntry.error ?? mutations.removeEntry.error;

  return (
    <div className="space-y-4 rounded-b-lg border border-t-0 border-border bg-surface px-4 py-3">
      <ErrorText error={actionError} />
      <KitSourceSection kit={kit} />
      <KitEntriesSection kit={kit} projects={projects} entryMap={entryMap} mutations={mutations} />
      {kit.mandatory ? (
        <KitGlobalMandatorySection kitName={kit.name} />
      ) : (
        <KitActivationSection kit={kit} projects={projects} mutations={mutations} />
      )}
      <KitDriftSection projects={projects} />
    </div>
  );
}

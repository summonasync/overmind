"use client";

import { Package } from "lucide-react";
import { InfoPanelSection, InfoPanelStat } from "@/widgets/app-shell";
import { useKitList } from "@/features/kits";

export function KitsInfoPanel() {
  const { data: kits, loading } = useKitList();

  if (loading) {
    return null;
  }

  const totalEntries = kits.reduce((sum, kit) => sum + kit.entryIds.length, 0);

  return (
    <div className="space-y-6">
      <InfoPanelSection title="Kits Overview" icon={Package}>
        <div className="grid grid-cols-2 gap-2">
          <InfoPanelStat value={kits.length} label="Total Kits" />
          <InfoPanelStat value={totalEntries} label="Bundled Entries" />
        </div>
      </InfoPanelSection>

      <InfoPanelSection title="Kit Classes">
        <div className="space-y-2 text-xs leading-relaxed text-muted">
          <p>
            <span className="font-semibold text-foreground">Mandatory</span> kits materialize into
            global skills — the agent home skill directories, always on, no per-project activation.
            The global home door ships with the global kit materialization feature; until then a
            mandatory kit cannot be deployed from here.
          </p>
          <p>
            <span className="font-semibold text-foreground">Optional</span> kits activate per
            project and stack — a project can run several optional kits at once.
          </p>
          <p>
            <span className="font-semibold text-foreground">Legacy</span> kits were retired by the
            kit split; they stay in the library for reference and no longer refresh.
          </p>
        </div>
      </InfoPanelSection>

      <InfoPanelSection title="Drift Checks">
        <p className="text-xs leading-relaxed text-muted">
          Expand a kit and run a drift check against a linked project to compare materialized files
          with the kit&apos;s current entries. Drifted files are reported as missing or modified.
        </p>
      </InfoPanelSection>
    </div>
  );
}

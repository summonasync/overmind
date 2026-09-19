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

      <InfoPanelSection title="Drift Checks">
        <p className="text-xs leading-relaxed text-muted">
          Expand a kit and run a drift check against a linked project to compare materialized files
          with the kit&apos;s current entries. Drifted files are reported as missing or modified.
        </p>
      </InfoPanelSection>
    </div>
  );
}

"use client";

import { Package } from "lucide-react";
import { useKitList } from "@/features/kits";

export function KitsInfoPanel() {
  const { data: kits, loading } = useKitList();

  if (loading) {
    return null;
  }

  const totalEntries = kits.reduce((sum, kit) => sum + kit.entryIds.length, 0);

  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <Package size={15} aria-hidden className="text-primary" />
          Kits Overview
        </h2>
        <div className="grid grid-cols-2 gap-2">
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{kits.length}</p>
            <p className="text-xs text-muted">Total Kits</p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{totalEntries}</p>
            <p className="text-xs text-muted">Bundled Entries</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-foreground">Drift Checks</h2>
        <p className="text-xs leading-relaxed text-muted">
          Expand a kit and run a drift check against a linked project to compare materialized files
          with the kit&apos;s current entries. Drifted files are reported as missing or modified.
        </p>
      </section>
    </div>
  );
}

import { Badge } from "@/shared/ui/badge";
import { formatImportedAt } from "@/features/kits/lib/import-labels";
import type { KitResponse } from "@/features/kits/types";

export function kitOriginTooltip(kit: KitResponse): string | undefined {
  if (kit.sourceKind !== "imported") {
    return undefined;
  }
  const importedAt = kit.importedAtUtc ? ` — imported ${formatImportedAt(kit.importedAtUtc)}` : "";
  return `${kit.sourcePath ?? "unknown source"}${importedAt}`;
}

export function KitOriginBadge({ kit }: { kit: KitResponse }) {
  const tooltip = kitOriginTooltip(kit);

  const badge =
    kit.sourceKind === "imported" ? (
      <Badge tone="info">imported</Badge>
    ) : kit.sourceKind === "bundled" ? (
      <Badge tone="skill">bundled</Badge>
    ) : (
      <Badge>manual</Badge>
    );

  return tooltip ? (
    <span title={tooltip} className="inline-flex">
      {badge}
    </span>
  ) : (
    badge
  );
}

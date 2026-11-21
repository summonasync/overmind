import { Badge } from "@/shared/ui/badge";
import { Hint } from "@/shared/ui/hint";
import { formatImportedAt } from "@/features/kits/lib/import-labels";
import type { KitResponse } from "@/features/kits/types";

export function kitOriginHint(kit: KitResponse): string | undefined {
  if (kit.sourceKind !== "imported") {
    return undefined;
  }
  const importedAt = kit.importedAtUtc ? ` — imported ${formatImportedAt(kit.importedAtUtc)}` : "";
  return `${kit.sourcePath ?? "unknown source"}${importedAt}`;
}

export function KitOriginBadge({ kit }: { kit: KitResponse }) {
  const hint = kitOriginHint(kit);

  const badge =
    kit.sourceKind === "imported" ? (
      <Badge tone="info">imported</Badge>
    ) : kit.sourceKind === "bundled" ? (
      <Badge tone="skill">bundled</Badge>
    ) : (
      <Badge>manual</Badge>
    );

  if (!hint) {
    return badge;
  }

  return (
    <Hint
      label="Import source"
      align="left"
      id={`hint-kit-${kit.id}-source`}
      trigger={badge}
    >
      {hint}
    </Hint>
  );
}

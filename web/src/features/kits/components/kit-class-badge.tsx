import { Badge } from "@/shared/ui/badge";
import type { KitResponse } from "@/features/kits/types";

const LEGACY_TOOLTIP = "Retired by the kit split — kept in the library for reference, no longer refreshed by bundle updates.";

export function KitClassBadge({ kit }: { kit: KitResponse }) {
  if (kit.legacy) {
    return (
      <span title={LEGACY_TOOLTIP} className="inline-flex">
        <Badge tone="warning">legacy</Badge>
      </span>
    );
  }

  return kit.mandatory ? (
    <Badge tone="primary">mandatory</Badge>
  ) : (
    <Badge tone="muted">optional</Badge>
  );
}

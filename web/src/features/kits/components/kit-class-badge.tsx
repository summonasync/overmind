import { Badge } from "@/shared/ui/badge";
import { Hint } from "@/shared/ui/hint";
import type { KitResponse } from "@/features/kits/types";

const LEGACY_HINT =
  "Retired by the kit split — kept in the library for reference, no longer refreshed by bundle updates.";

export function KitClassBadge({ kit }: { kit: KitResponse }) {
  if (kit.legacy) {
    return (
      <Hint
        label="Legacy kit"
        align="left"
        id={`hint-kit-${kit.id}-class`}
        trigger={<Badge tone="warning">legacy</Badge>}
      >
        {LEGACY_HINT}
      </Hint>
    );
  }

  return kit.mandatory ? (
    <Badge tone="primary">mandatory</Badge>
  ) : (
    <Badge tone="muted">optional</Badge>
  );
}

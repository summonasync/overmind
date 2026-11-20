import { Globe } from "lucide-react";
import { Badge } from "@/shared/ui/badge";

interface KitGlobalMandatorySectionProps {
  kitName: string;
}

export function KitGlobalMandatorySection({ kitName }: KitGlobalMandatorySectionProps) {
  return (
    <section>
      <h3 className="mb-1 flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-muted">
        <Globe size={13} aria-hidden />
        Global activation
      </h3>
      <div className="space-y-2 rounded-md border border-primary/30 bg-primary/5 px-3 py-2.5 text-xs leading-relaxed text-foreground">
        <p>
          <Badge tone="primary">mandatory</Badge>{" "}
          <span className="font-semibold">{kitName}</span> is a mandatory-global kit: it materializes
          into the global skills of every detected agent (their home skill directories), always on —
          no per-project activation, no opt-out.
        </p>
        <p className="text-muted">
          Not deployable from this screen yet: the home-directory door lands with the global kit
          materialization feature, and per-project activation of a mandatory kit is refused by design
          until then. There is intentionally no activate button here.
        </p>
        <p className="text-muted">
          This kit also ships the <code>.qwen/settings-snippet.jsonc</code> and{" "}
          <code>hooks/post-edit-verify.*</code> fragments — .NET-flavored enforcement that installs
          globally with the kit; the hooks fail open by design.
        </p>
      </div>
    </section>
  );
}

"use client";

import { PageHeader } from "@/shared/ui/page-header";
import { useInfoPanel } from "@/widgets/app-shell";
import { WikiContent } from "@/pages/wiki-page/ui/wiki-content";
import { WikiToc } from "@/pages/wiki-page/ui/wiki-toc";

export function WikiPage() {
  useInfoPanel(<WikiToc />);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Wiki"
        description="How the board works: numbering, the planning ceremony, lanes, waves and the nuances documented nowhere else."
      />

      <details className="rounded-lg border border-border bg-surface p-3 xl:hidden">
        <summary className="cursor-pointer text-sm font-semibold text-foreground">
          On this page
        </summary>
        <div className="mt-2">
          <WikiToc />
        </div>
      </details>

      <WikiContent />
    </div>
  );
}

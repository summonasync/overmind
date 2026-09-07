"use client";

import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { Zerling } from "@/shared/ui/zerling";
import { ReindexButton, SettingsForm, useSettings, useSettingsMutations } from "@/features/settings";
import type { ReindexReport } from "@/features/settings";
import { useInfoPanel } from "@/widgets/app-shell/lib/info-panel-context";
import { useState } from "react";
import { SettingsHelpPanel } from "@/pages/settings-page/ui/settings-help-panel";

export function SettingsPage() {
  const { data: settings, loading, error } = useSettings();
  const { save, rebuild } = useSettingsMutations();
  const [report, setReport] = useState<ReindexReport | null>(null);

  useInfoPanel(<SettingsHelpPanel />);

  if (loading) {
    return <LoadingState label="Loading settings…" />;
  }

  if (!settings) {
    return (
      <div className="space-y-4">
        <PageHeader title="Settings" description="Configure models, APIs, and indexing for your Overmind instance." />
        <ErrorText error={error} />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Settings" description="Configure models, APIs, and indexing for your Overmind instance." />

      <section className="rounded-lg border border-border bg-surface p-5">
        <h2 className="mb-1 text-base font-semibold text-foreground">Embedding Provider</h2>
        <p className="mb-4 text-xs text-muted">Select the model provider used for text embeddings.</p>
        <SettingsForm
          settings={settings}
          pending={save.isPending}
          error={save.error as Error | null}
          onSubmit={(values) => save.mutate(values)}
        />
        {save.isSuccess ? (
          <p className="mt-3 flex items-center gap-2 text-xs text-success">
            <Zerling pose="celebrating" size={24} />
            Settings saved.
          </p>
        ) : null}
      </section>

      <section className="rounded-lg border border-border bg-surface p-5">
        <h2 className="mb-1 text-base font-semibold text-foreground">Vector Search</h2>
        <p className="mb-4 text-xs text-muted">
          Rebuilds the semantic search index from all entries using the configured embedding provider.
        </p>
        <ReindexButton
          pending={rebuild.isPending}
          report={report}
          onReindex={() => rebuild.mutate(undefined, { onSuccess: setReport })}
        />
        {rebuild.error ? <ErrorText error={rebuild.error as Error} /> : null}
      </section>
    </div>
  );
}

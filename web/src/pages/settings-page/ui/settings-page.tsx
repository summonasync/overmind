"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { Zerling } from "@/shared/ui/zerling";
import { healthKeys } from "@/features/health";
import { GrimoireStatusTile } from "@/features/grimoire";
import { ReindexButton, SettingsForm, useSettings, useSettingsMutations } from "@/features/settings";
import type { ReindexReport, SettingsFormValues } from "@/features/settings";
import { useInfoPanel } from "@/widgets/app-shell";
import { AttentionSettingsSection } from "@/pages/settings-page/ui/attention-settings-section";
import { SettingsHelpPanel } from "@/pages/settings-page/ui/settings-help-panel";
import { SettingsHub } from "@/pages/settings-page/ui/settings-hub";

export function SettingsPage() {
  const { data: settings, loading, error } = useSettings();
  const { save, test, rebuild } = useSettingsMutations();
  const queryClient = useQueryClient();
  const [report, setReport] = useState<ReindexReport | null>(null);

  useInfoPanel(<SettingsHelpPanel />);

  const handleSave = (values: SettingsFormValues) => {
    save.mutate(values, {
      onSuccess: () => queryClient.invalidateQueries({ queryKey: healthKeys.all }),
    });
  };

  if (loading) {
    return (
      <SettingsHub>
        <LoadingState label="Loading settings…" />
      </SettingsHub>
    );
  }

  if (!settings) {
    return (
      <SettingsHub>
        <div className="space-y-4">
          <PageHeader title="Settings" description="Configure models, APIs, and indexing for your Overmind instance." />
          <ErrorText error={error} />
        </div>
      </SettingsHub>
    );
  }

  return (
    <SettingsHub>
      <div className="space-y-6">
        <PageHeader title="Settings" description="Configure models, APIs, and indexing for your Overmind instance." />

        <section className="rounded-lg border border-border bg-surface p-5">
          <h2 className="mb-1 text-base font-semibold text-foreground">Embedding, Vector Search & Planning</h2>
          <p className="mb-4 text-xs text-muted">
            Embedding provider and Qdrant endpoint for semantic search, plus the planning workflow
            defaults every project can fall back to.
          </p>
          <SettingsForm
            settings={settings}
            pending={save.isPending}
            error={save.error as Error | null}
            onSubmit={handleSave}
            onTest={(values) => test.mutate(values)}
            testPending={test.isPending}
            testReport={test.data ?? null}
            testError={test.error as Error | null}
          />
          {save.isSuccess ? (
            <p className="mt-3 flex items-center gap-2 text-xs text-success">
              <Zerling pose="celebrating" size={24} />
              Settings saved.
            </p>
          ) : null}
          {save.data?.reindexRecommended ? (
            <div
              className="mt-3 flex flex-wrap items-center gap-x-2 gap-y-1 rounded-md border border-warning/40 bg-warning/10 px-3 py-2 text-sm text-warning"
              role="status"
            >
              <span>Embedding or Qdrant endpoint changed — the vector index is stale.</span>
              <a
                href="#vector-search"
                className="font-medium underline underline-offset-2 hover:text-warning/80"
              >
                Reindex now
              </a>
            </div>
          ) : null}
        </section>

        <section id="vector-search" className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
          <h2 className="mb-1 text-base font-semibold text-foreground">Vector Search</h2>
          <p className="mb-4 text-xs text-muted">
            Rebuilds the semantic search index from all entries using the configured embedding provider.
          </p>
          <ReindexButton
            pending={rebuild.isPending}
            report={report}
            onReindex={() =>
              rebuild.mutate(undefined, {
                onSuccess: (r) => {
                  save.reset();
                  setReport(r);
                },
              })
            }
          />
          {rebuild.error ? <ErrorText error={rebuild.error as Error} /> : null}
        </section>

        <AttentionSettingsSection />

        <GrimoireStatusTile />
      </div>
    </SettingsHub>
  );
}

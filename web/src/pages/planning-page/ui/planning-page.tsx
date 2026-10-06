"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { TabHeader } from "@/shared/ui/tab-header";
import { useInfoPanel } from "@/widgets/app-shell";
import {
  BoardFilterBar,
  boardItemMatchesSearch,
  DriftPanel,
  FeatureBoard,
  FeatureForm,
  FeatureWorkspace,
  filterBoardItems,
  useBoardFilters,
  useBoardSearch,
  usePlanningBoard,
  usePlanningFeature,
  usePlanningMutations,
} from "@/features/planning";
import type { PlanningFeatureBoardItem, PlanningFeatureFormValues } from "@/features/planning";
import { useProjectNumbering } from "@/features/projects";
import { PlanningInfoPanel } from "@/pages/planning-page/ui/planning-info-panel";
import { EmptyState } from "@/shared/ui/empty-state";

const PLANNING_DESCRIPTION =
  "Plan features as spec, plan, tasks and result. Track ceremony from draft to done. The board order is the queue — first card is next up; drag within a priority band to reorder.";

const DASHBOARD_TAB_DESCRIPTION =
  "Plan features as spec, plan, tasks and result. The board order is the queue — first card is next up.";

function PlanningPageContent() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const featureSlug = searchParams?.get("feature") ?? "";

  const { data: features, loading, error } = usePlanningBoard(projectId);
  const numbering = useProjectNumbering(projectId);
  const boardFilters = useBoardFilters();
  const boardSearch = useBoardSearch();
  const { createFeature, updateFeature, deleteFeature, claimFeature, unclaimFeature } =
    usePlanningMutations(projectId);

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<PlanningFeatureBoardItem | null>(null);
  const [deleting, setDeleting] = useState<PlanningFeatureBoardItem | null>(null);
  const { data: editingFeature, error: editingError } = usePlanningFeature(
    projectId,
    editing?.slug ?? "",
  );

  useInfoPanel(
    projectId ? <PlanningInfoPanel projectId={projectId} featureSlug={featureSlug} /> : null,
  );

  const activeError =
    (createFeature.error as Error | null) ??
    (updateFeature.error as Error | null) ??
    (deleteFeature.error as Error | null) ??
    (claimFeature.error as Error | null) ??
    (unclaimFeature.error as Error | null) ??
    error;

  const handleClaim = (item: PlanningFeatureBoardItem) => {
    claimFeature.mutate({
      slug: item.slug,
      alias: "board",
      vendor: "human",
    });
  };

  const handleUnclaim = (item: PlanningFeatureBoardItem) => {
    unclaimFeature.mutate({ slug: item.slug });
  };

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const openEdit = (item: PlanningFeatureBoardItem) => {
    setEditing(item);
    setFormOpen(true);
  };

  const handleSubmit = (values: PlanningFeatureFormValues) => {
    if (editing) {
      updateFeature.mutate(
        { slug: editing.slug, values: { title: values.title, body: values.body, priority: values.priority } },
        { onSuccess: () => setFormOpen(false) },
      );
    } else {
      createFeature.mutate(values, { onSuccess: () => setFormOpen(false) });
    }
  };

  if (!projectId) {
    return (
      <div className="space-y-4">
        <PageHeader title="Dashboard" description={PLANNING_DESCRIPTION} />
        <p className="text-sm text-muted">
          Select a project first.{" "}
          <a href="/" className="text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40">
            Go to Projects
          </a>
        </p>
      </div>
    );
  }

  if (featureSlug) {
    return (
      <div className="space-y-4">
        <FeatureWorkspace projectId={projectId} slug={featureSlug} numbering={numbering} />
        <DriftPanel projectId={projectId} />
      </div>
    );
  }

  const searchedFeatures = boardSearch.isActive
    ? features.filter((item) => boardItemMatchesSearch(item, boardSearch.query))
    : features;
  const visibleFeatures = filterBoardItems(searchedFeatures, boardFilters.filters);
  const clearBoardRestrictions = () => {
    boardSearch.clear();
    boardFilters.clearFilters();
  };

  return (
    <div className="space-y-4">
      <TabHeader
        description={DASHBOARD_TAB_DESCRIPTION}
        actions={
          <>
            <a
              href="/"
              className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
            >
              ← All projects
            </a>
            <Button onClick={openCreate}>New feature</Button>
          </>
        }
      />

      <ErrorText error={activeError} />

      {loading ? (
        <LoadingState label="Loading features…" />
      ) : (
        <>
          <BoardFilterBar search={boardSearch} />
          {(boardFilters.isActive || boardSearch.isActive) && visibleFeatures.length === 0 ? (
            <EmptyState
              title="No features match"
              hint={
                boardSearch.isActive
                  ? "Nothing survives the search combined with the active filters."
                  : "Remove a filter or clear them all to see the whole board."
              }
              action={
                <Button variant="secondary" onClick={clearBoardRestrictions}>
                  {boardSearch.isActive ? "Clear search & filters" : "Clear filters"}
                </Button>
              }
            />
          ) : (
            <FeatureBoard
              projectId={projectId}
              numbering={numbering}
              features={visibleFeatures}
              onCreate={openCreate}
              onEdit={openEdit}
              onDelete={setDeleting}
              onClaim={handleClaim}
              onUnclaim={handleUnclaim}
              claimPending={claimFeature.isPending || unclaimFeature.isPending}
            />
          )}
        </>
      )}

      <Modal
        title={editing ? "Edit feature" : "New feature"}
        open={formOpen}
        onClose={() => setFormOpen(false)}
        size="xl"
      >
        {editing && editingFeature === null ? (
          editingError ? (
            <ErrorText error={editingError} />
          ) : (
            <LoadingState label="Loading feature…" />
          )
        ) : (
          <FeatureForm
            initial={editing ? (editingFeature ?? undefined) : undefined}
            pending={createFeature.isPending || updateFeature.isPending}
            error={(createFeature.error as Error | null) ?? (updateFeature.error as Error | null)}
            onSubmit={handleSubmit}
            onCancel={() => setFormOpen(false)}
          />
        )}
      </Modal>

      <Modal title="Delete feature" open={deleting !== null} onClose={() => setDeleting(null)}>
        {deleting ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              Delete feature &quot;{deleting.title}&quot;? Materialized files are removed with it.
              This cannot be undone.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setDeleting(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={deleteFeature.isPending}
                onClick={() => deleteFeature.mutate(deleting.slug, { onSuccess: () => setDeleting(null) })}
              >
                {deleteFeature.isPending ? "Deleting…" : "Delete"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

export function PlanningPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <PlanningPageContent />
    </Suspense>
  );
}

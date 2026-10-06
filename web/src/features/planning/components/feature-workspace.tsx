"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { Check, Pencil, Plus, Trash2, X } from "lucide-react";
import { Markdown } from "@/shared/lib/markdown";
import { cn } from "@/shared/lib/cn";
import { formatCalendarDate, formatRelativeTime } from "@/shared/lib/relative-time";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { Modal } from "@/shared/ui/modal";
import { PageHeader } from "@/shared/ui/page-header";
import { Select } from "@/shared/ui/select";
import { ArtifactDeclineDialog } from "@/features/planning/components/artifact-decline-dialog";
import { ArtifactEditor } from "@/features/planning/components/artifact-editor";
import { ArtifactLog } from "@/features/planning/components/artifact-log";
import { CeremonyStepper } from "@/features/planning/components/ceremony-stepper";
import { DeleteArtifactDialog } from "@/features/planning/components/delete-artifact-dialog";
import { FeatureDependenciesPanel } from "@/features/planning/components/feature-dependencies-panel";
import { FeatureForm } from "@/features/planning/components/feature-form";
import { FeaturePrioritySelect } from "@/features/planning/components/feature-priority-select";
import { TaskBoard } from "@/features/planning/components/task-board";
import { TaskDependenciesPanel } from "@/features/planning/components/task-dependencies-panel";
import { TaskEditor } from "@/features/planning/components/task-editor";
import { usePlanningFeature, usePlanningMutations } from "@/features/planning/lib/use-planning";
import { allowedTransitions, featureStatusOptions } from "@/features/planning/lib/feature-transitions";
import { artifactStatusTone } from "@/features/planning/lib/status-tone";
import {
  PLANNING_TABS,
  type PlanningArtifactResponse,
  type PlanningDocumentKind,
  type PlanningFeatureStatus,
  type PlanningTab,
} from "@/features/planning/types";
import type { ProjectNumbering } from "@/shared/lib/feature-number";

const TAB_LABELS: Record<PlanningTab, string> = {
  spec: "Spec",
  plan: "Plan",
  tasks: "Tasks",
  result: "Result",
};

const EMPTY_TAB_HINTS: Record<PlanningDocumentKind, string> = {
  spec: "What and why — written and approved before planning starts.",
  plan: "How the feature gets built, validated before execution.",
  result: "The recorded outcome once the feature ships.",
};

const focusRing =
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 focus-visible:ring-offset-2 focus-visible:ring-offset-background";

interface FeatureWorkspaceProps {
  projectId: string;
  slug: string;
  numbering: ProjectNumbering;
}

export function FeatureWorkspace({ projectId, slug, numbering }: FeatureWorkspaceProps) {
  const searchParams = useSearchParams();
  const requestedTab = searchParams?.get("tab") ?? null;
  const [tab, setTabState] = useState<PlanningTab>(
    PLANNING_TABS.find((candidate) => candidate === requestedTab) ?? "spec",
  );
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [documentModal, setDocumentModal] = useState<PlanningDocumentKind | null>(null);
  const [taskModal, setTaskModal] = useState<"new" | PlanningArtifactResponse | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<PlanningArtifactResponse | null>(null);
  const [declineTarget, setDeclineTarget] = useState<PlanningArtifactResponse | null>(null);

  const { data: feature, loading, error } = usePlanningFeature(projectId, slug);
  const {
    updateFeature,
    deleteFeature,
    createArtifact,
    updateArtifact,
    deleteArtifact,
    setArtifactStatus,
    appendArtifactLog,
    validateArtifact,
    declineArtifact,
  } = usePlanningMutations(projectId);

  const setTab = (next: PlanningTab) => {
    setTabState(next);
    if (!validateArtifact.isPending) {
      validateArtifact.reset();
    }
    window.history.replaceState(null, "", `/dashboard?project=${projectId}&feature=${slug}&tab=${next}`);
  };

  if (loading) {
    return <LoadingState label="Loading feature…" />;
  }

  if (feature === null) {
    return (
      <div className="space-y-4">
        <PageHeader
          title={slug}
          description="Feature workspace"
          actions={
            <a
              href={`/dashboard?project=${projectId}`}
              className={cn("text-xs text-muted underline-offset-2 hover:text-foreground hover:underline", focusRing)}
            >
              ← Dashboard
            </a>
          }
        />
        <ErrorText error={error ?? new Error(`Feature '${slug}' was not found.`)} />
      </div>
    );
  }

  const boardHref = `/dashboard?project=${projectId}`;
  const documentKind = documentModal ?? (tab === "tasks" ? null : tab);
  const documentArtifact =
    documentKind !== null
      ? feature.artifacts.find((artifact) => artifact.kind === documentKind)
      : undefined;
  const taskArtifact =
    taskModal !== null && taskModal !== "new"
      ? feature.artifacts.find((artifact) => artifact.id === taskModal.id) ?? taskModal
      : undefined;
  const tasks = feature.artifacts
    .filter((artifact) => artifact.kind === "task")
    .sort((left, right) => left.order - right.order);

  const openDocumentEditor = (kind: PlanningDocumentKind) => setDocumentModal(kind);

  const openArtifactDelete = (artifact: PlanningArtifactResponse) => {
    deleteArtifact.reset();
    setDeleteTarget(artifact);
  };

  const openArtifactDecline = (artifact: PlanningArtifactResponse) => {
    declineArtifact.reset();
    setDeclineTarget(artifact);
  };

  return (
    <div className="space-y-4">
      <header className="space-y-3">
        <a
          href={boardHref}
          className={cn("text-xs text-muted underline-offset-2 hover:text-foreground hover:underline", focusRing)}
        >
          ← Dashboard
        </a>
        <div>
          <h1 className="text-2xl font-semibold text-foreground">{feature.title}</h1>
          <p className="mt-1 text-sm text-muted">
            {feature.slug} · updated {formatRelativeTime(feature.updatedAt)}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Select
            aria-label="Feature status"
            value={feature.status}
            disabled={allowedTransitions(feature.status).length === 0}
            onChange={(event) =>
              updateFeature.mutate({
                slug,
                values: { status: event.target.value as PlanningFeatureStatus },
              })
            }
          >
            {featureStatusOptions(feature.status).map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </Select>
          <FeaturePrioritySelect
            projectId={projectId}
            slug={slug}
            title={feature.title}
            priority={feature.priority}
          />
          <Badge>v{feature.version}</Badge>
          <Button variant="secondary" className="px-2.5 py-1 text-xs" onClick={() => setEditOpen(true)}>
            <Pencil size={12} aria-hidden />
            Edit
          </Button>
          <Button
            variant="ghost"
            className="px-2.5 py-1 text-xs text-danger hover:bg-danger/10 hover:text-danger"
            onClick={() => setDeleteOpen(true)}
          >
            <Trash2 size={12} aria-hidden />
            Delete
          </Button>
        </div>
      </header>

      <ErrorText error={updateFeature.error as Error | null} />

      <FeatureDependenciesPanel projectId={projectId} feature={feature} />

      <TaskDependenciesPanel featureSlug={slug} numbering={numbering} tasks={tasks} />

      <CeremonyStepper feature={feature} activeTab={tab} onSelect={setTab} />

      {feature.body.trim().length > 0 ? (
        <div className="rounded-lg border border-border bg-surface p-4">
          <Markdown content={feature.body} />
        </div>
      ) : null}

      <div className="flex flex-wrap items-end gap-1 border-b border-border" role="tablist" aria-label="Feature artifacts">
        {PLANNING_TABS.map((candidate) => {
          const count = candidate === "tasks" ? tasks.length : undefined;
          return (
            <button
              key={candidate}
              type="button"
              role="tab"
              aria-selected={candidate === tab}
              onClick={() => setTab(candidate)}
              className={cn(
                "-mb-px border-b-2 px-3 py-2 text-sm font-medium",
                focusRing,
                candidate === tab
                  ? "border-primary text-foreground"
                  : "border-transparent text-muted hover:text-foreground",
              )}
            >
              {TAB_LABELS[candidate]}
              {count !== undefined ? <span className="ml-1 font-mono text-xs text-muted">{count}</span> : null}
            </button>
          );
        })}
        {tab === "tasks" ? (
          <Button variant="ghost" className="ml-auto px-2.5 py-1 text-xs" onClick={() => setTaskModal("new")}>
            <Plus size={12} aria-hidden />
            Add task
          </Button>
        ) : null}
      </div>

      {tab === "tasks" ? (
        tasks.length === 0 ? (
          <EmptyState
            title="No tasks yet"
            hint="Break the plan into ordered tasks with a # heading each — they land on the board below."
            action={
              <Button onClick={() => setTaskModal("new")}>
                <Plus size={12} aria-hidden />
                Create task
              </Button>
            }
          />
        ) : (
          <TaskBoard
            tasks={tasks}
            featureSlug={slug}
            numbering={numbering}
            pending={setArtifactStatus.isPending}
            error={setArtifactStatus.error as Error | null}
            onOpen={setTaskModal}
            onMove={({ artifact, status, logNote }) =>
              setArtifactStatus.mutate({ artifactId: artifact.id, slug, status, logNote })
            }
            onDelete={openArtifactDelete}
          />
        )
      ) : documentArtifact === undefined ? (
        <EmptyState
          title={`No ${tab} yet`}
          hint={EMPTY_TAB_HINTS[tab]}
          action={<Button onClick={() => openDocumentEditor(tab)}>Create {tab}</Button>}
        />
      ) : (
        <DocumentPanel
          artifact={documentArtifact}
          approvePending={validateArtifact.isPending}
          approveError={validateArtifact.error as Error | null}
          declinePending={declineArtifact.isPending}
          onEdit={() => openDocumentEditor(tab)}
          onApprove={() =>
            validateArtifact.mutate({ artifactId: documentArtifact.id, slug })
          }
          onDecline={() => openArtifactDecline(documentArtifact)}
          onDelete={() => openArtifactDelete(documentArtifact)}
        />
      )}

      <Modal
        title={documentModal !== null ? `${documentArtifact ? "Edit" : "New"} ${TAB_LABELS[documentModal].toLowerCase()}` : ""}
        open={documentModal !== null}
        onClose={() => setDocumentModal(null)}
        size="xl"
      >
        {documentModal !== null ? (
          <ArtifactEditor
            kind={documentModal}
            initial={documentArtifact}
            pending={createArtifact.isPending || updateArtifact.isPending}
            approvePending={validateArtifact.isPending}
            error={
              (createArtifact.error as Error | null) ??
              (updateArtifact.error as Error | null) ??
              (validateArtifact.error as Error | null)
            }
            onSubmit={({ body, logNote }) => {
              if (documentArtifact) {
                updateArtifact.mutate(
                  { artifactId: documentArtifact.id, slug, values: { body, logNote } },
                  { onSuccess: () => setDocumentModal(null) },
                );
              } else {
                createArtifact.mutate(
                  { slug, values: { kind: documentModal, body, logNote } },
                  { onSuccess: () => setDocumentModal(null) },
                );
              }
            }}
            onApprove={() => {
              if (documentArtifact) {
                validateArtifact.mutate(
                  { artifactId: documentArtifact.id, slug },
                  { onSuccess: () => setDocumentModal(null) },
                );
              }
            }}
            onCancel={() => setDocumentModal(null)}
          />
        ) : null}
      </Modal>

      <Modal
        title={taskArtifact ? `Task #${taskArtifact.order}` : "New task"}
        open={taskModal !== null}
        onClose={() => setTaskModal(null)}
        size="xl"
      >
        {taskModal !== null ? (
          <TaskEditor
            initial={taskArtifact}
            pending={createArtifact.isPending || updateArtifact.isPending}
            logPending={appendArtifactLog.isPending}
            error={
              (createArtifact.error as Error | null) ??
              (updateArtifact.error as Error | null) ??
              (appendArtifactLog.error as Error | null)
            }
            onSubmit={({ body, brief }) => {
              if (taskArtifact) {
                updateArtifact.mutate(
                  { artifactId: taskArtifact.id, slug, values: { body, brief } },
                  { onSuccess: () => setTaskModal(null) },
                );
              } else {
                createArtifact.mutate(
                  { slug, values: { kind: "task", body, brief } },
                  { onSuccess: () => setTaskModal(null) },
                );
              }
            }}
            onAppendLog={(note) => {
              if (taskArtifact) {
                appendArtifactLog.mutate({ artifactId: taskArtifact.id, slug, note });
              }
            }}
            onCancel={() => setTaskModal(null)}
          />
        ) : null}
      </Modal>

      <Modal
        title={declineTarget !== null ? `Decline ${declineTarget.kind}` : ""}
        open={declineTarget !== null}
        onClose={() => setDeclineTarget(null)}
      >
        {declineTarget !== null ? (
          <ArtifactDeclineDialog
            kind={declineTarget.kind}
            pending={declineArtifact.isPending}
            error={declineArtifact.error as Error | null}
            onClose={() => setDeclineTarget(null)}
            onConfirm={(reason) =>
              declineArtifact.mutate(
                { artifactId: declineTarget.id, slug, reason },
                { onSuccess: () => setDeclineTarget(null) },
              )
            }
          />
        ) : null}
      </Modal>

      <Modal title="Edit feature" open={editOpen} onClose={() => setEditOpen(false)} size="xl">
        <FeatureForm
          initial={feature}
          pending={updateFeature.isPending}
          error={updateFeature.error as Error | null}
          onSubmit={(values) =>
            updateFeature.mutate(
              { slug, values: { title: values.title, body: values.body, priority: values.priority } },
              { onSuccess: () => setEditOpen(false) },
            )
          }
          onCancel={() => setEditOpen(false)}
        />
      </Modal>

      <DeleteArtifactDialog
        artifact={deleteTarget}
        siblings={feature.artifacts}
        featureSlug={slug}
        numbering={numbering}
        pending={deleteArtifact.isPending}
        error={deleteArtifact.error as Error | null}
        onClose={() => setDeleteTarget(null)}
        onConfirm={(artifact) =>
          deleteArtifact.mutate(
            { artifactId: artifact.id, slug },
            { onSuccess: () => setDeleteTarget(null) },
          )
        }
      />

      <Modal title="Delete feature" open={deleteOpen} onClose={() => setDeleteOpen(false)}>
        <div className="space-y-4">
          <p className="text-sm text-foreground">
            Delete feature &quot;{feature.title}&quot;? Materialized files are removed with it.
            This cannot be undone.
          </p>
          <ErrorText error={deleteFeature.error as Error | null} />
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setDeleteOpen(false)}>
              Cancel
            </Button>
            <Button
              variant="danger"
              disabled={deleteFeature.isPending}
              onClick={() =>
                deleteFeature.mutate(slug, {
                  onSuccess: () => {
                    setDeleteOpen(false);
                    window.history.replaceState(null, "", boardHref);
                  },
                })
              }
            >
              {deleteFeature.isPending ? "Deleting…" : "Delete"}
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

function DocumentPanel({
  artifact,
  approvePending,
  approveError,
  declinePending,
  onEdit,
  onApprove,
  onDecline,
  onDelete,
}: {
  artifact: PlanningArtifactResponse;
  approvePending: boolean;
  approveError: Error | null;
  declinePending: boolean;
  onEdit: () => void;
  onApprove: () => void;
  onDecline: () => void;
  onDelete: () => void;
}) {
  const frozen = artifact.status === "validated";

  return (
    <div className="space-y-3 rounded-lg border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center gap-1.5">
        <Badge tone={artifactStatusTone(artifact.status)}>{artifact.status}</Badge>
        {artifact.frozenAt !== null ? (
          <Badge tone="success">validated {formatCalendarDate(artifact.frozenAt)}</Badge>
        ) : null}
        <Badge tone={artifact.review === "hard" ? "warning" : "neutral"}>{artifact.review}</Badge>
        <span className="ml-auto font-mono text-xs text-muted">
          v{artifact.version} · updated {formatRelativeTime(artifact.updatedAt)}
        </span>
        {!frozen ? (
          <>
            <Button
              variant="accent"
              className="px-2.5 py-1 text-xs"
              disabled={approvePending}
              onClick={onApprove}
            >
              <Check size={12} aria-hidden />
              {approvePending ? "Approving…" : "Approve"}
            </Button>
            <Button variant="secondary" className="px-2.5 py-1 text-xs" disabled={declinePending} onClick={onDecline}>
              <X size={12} aria-hidden />
              {declinePending ? "Declining…" : "Decline"}
            </Button>
          </>
        ) : null}
        <Button variant="secondary" className="px-2.5 py-1 text-xs" onClick={onEdit}>
          <Pencil size={12} aria-hidden />
          Edit
        </Button>
        <Button
          variant="ghost"
          className="px-2.5 py-1 text-xs text-danger hover:bg-danger/10 hover:text-danger"
          onClick={onDelete}
        >
          <Trash2 size={12} aria-hidden />
          Delete
        </Button>
      </div>
      <ErrorText error={approveError} />
      <div className="max-h-[55vh] overflow-y-auto">
        <Markdown content={artifact.body} />
      </div>
      {artifact.log.length > 0 ? (
        <div className="border-t border-border pt-3">
          <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Change log</h3>
          <ArtifactLog log={artifact.log} />
        </div>
      ) : null}
    </div>
  );
}

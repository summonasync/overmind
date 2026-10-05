"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { Modal } from "@/shared/ui/modal";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { useInfoPanel } from "@/widgets/app-shell";
import {
  ProjectDetail,
  ProjectForm,
  ProjectList,
  ProjectManifestSection,
  useProjectList,
  useProjectMutations,
} from "@/features/projects";
import type { ProjectFormValues, ProjectResponse } from "@/features/projects";
import { PlanningWorkflowSettings, BacklogImportSection } from "@/features/planning";
import { ProjectsInfoPanel } from "@/pages/projects-page/ui/projects-info-panel";

function ProjectsPageContent() {
  const searchParams = useSearchParams();
  const detailId = searchParams?.get("project") ?? "";

  const { data: projects, loading, error } = useProjectList();
  const { create, update, remove } = useProjectMutations();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<ProjectResponse | null>(null);
  const [deleting, setDeleting] = useState<ProjectResponse | null>(null);

  useInfoPanel(<ProjectsInfoPanel />);

  const detail = projects.find((item) => item.id === detailId) ?? null;

  const activeError =
    (create.error as Error | null) ??
    (update.error as Error | null) ??
    (remove.error as Error | null) ??
    error;

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };

  const closeDetail = () => {
    window.history.replaceState(null, "", "/");
  };

  const handleSubmit = (values: ProjectFormValues) => {
    if (editing) {
      update.mutate(
        { id: editing.id, values },
        { onSuccess: () => setFormOpen(false) },
      );
    } else {
      create.mutate(values, { onSuccess: () => setFormOpen(false) });
    }
  };

  return (
    <div className="space-y-4">
      <PageHeader
        title="Projects"
        description="Organize coding-agent context, from codebases to documentation, in one unified space."
        actions={<Button onClick={openCreate}>New project</Button>}
      />

      <ErrorText error={activeError} />

      {loading ? (
        <LoadingState label="Loading projects…" />
      ) : (
        <ProjectList
          projects={projects}
          onEdit={(project) => {
            setEditing(project);
            setFormOpen(true);
          }}
          onDelete={setDeleting}
        />
      )}

      <Modal
        title={detail ? detail.name : "Project"}
        open={detail !== null}
        onClose={closeDetail}
      >
        {detail ? (
          <>
            <ProjectDetail project={detail} />
            <div className="mt-4 space-y-3 border-t border-border pt-4">
              <h3 className="text-sm font-semibold text-foreground">Manifest</h3>
              <ProjectManifestSection project={detail} />
            </div>
            <div className="mt-4 space-y-3 border-t border-border pt-4">
              <h3 className="text-sm font-semibold text-foreground">Planning workflow</h3>
              <PlanningWorkflowSettings projectId={detail.id} />
            </div>
            <div className="mt-4 space-y-3 border-t border-border pt-4">
              <h3 className="text-sm font-semibold text-foreground">Backlog import</h3>
              <BacklogImportSection projectId={detail.id} />
            </div>
          </>
        ) : null}
      </Modal>

      <Modal
        title={editing ? "Edit project" : "New project"}
        open={formOpen}
        onClose={() => setFormOpen(false)}
      >
        <ProjectForm
          initial={editing ?? undefined}
          pending={create.isPending || update.isPending}
          error={(create.error as Error | null) ?? (update.error as Error | null)}
          onSubmit={handleSubmit}
          onCancel={() => setFormOpen(false)}
        />
      </Modal>

      <Modal title="Delete project" open={deleting !== null} onClose={() => setDeleting(null)}>
        {deleting ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              Delete project &quot;{deleting.name}&quot;? This cannot be undone.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setDeleting(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={remove.isPending}
                onClick={() =>
                  remove.mutate(deleting.id, { onSuccess: () => setDeleting(null) })
                }
              >
                {remove.isPending ? "Deleting…" : "Delete"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

export function ProjectsPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <ProjectsPageContent />
    </Suspense>
  );
}

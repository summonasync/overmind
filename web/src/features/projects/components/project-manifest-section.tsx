"use client";

import { useState } from "react";
import { FolderInput } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { MarkdownEditor } from "@/shared/ui/markdown-editor";
import { manifestImportedLine, manifestIsAbsent } from "@/features/projects/lib/manifest-text";
import {
  useProjectManifestImport,
  useProjectMutations,
} from "@/features/projects/lib/use-projects";
import type { ProjectResponse } from "@/features/projects/types";

interface ProjectManifestSectionProps {
  project: ProjectResponse;
}

export function ProjectManifestSection({ project }: ProjectManifestSectionProps) {
  const { update } = useProjectMutations();
  const manifestImport = useProjectManifestImport();

  const [draft, setDraft] = useState(project.manifest ?? "");
  const [syncedManifest, setSyncedManifest] = useState<string | null>(project.manifest);

  if (project.manifest !== syncedManifest) {
    setSyncedManifest(project.manifest);
    setDraft(project.manifest ?? "");
  }

  const importedFile = manifestImport.sourceFile;

  const handleImport = () => {
    manifestImport.importManifest(project.id, (imported) => setDraft(imported.manifest));
  };

  const handleSave = () => {
    update.mutate({
      id: project.id,
      values: {
        name: project.name,
        diskPath: project.diskPath,
        description: project.description,
        manifest: draft,
        setManifest: true,
      },
    });
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <p className="text-xs text-muted">
          Markdown notes any agent reads over MCP: what the project is, stack, build and test
          commands, conventions.
        </p>
        <Button
          variant="secondary"
          size="sm"
          disabled={manifestImport.pending}
          onClick={handleImport}
        >
          <FolderInput size={12} aria-hidden />
          {manifestImport.pending ? "Importing…" : "Import from agent file"}
        </Button>
      </div>

      {manifestIsAbsent(project.manifest) ? (
        <p className="text-xs text-muted" role="note">
          No manifest set — edit below or import from AGENTS.md, CLAUDE.md or QWEN.md in the
          project folder.
        </p>
      ) : null}

      {importedFile ? (
        <p className="text-xs text-success" role="status">
          {manifestImportedLine(importedFile)}
        </p>
      ) : null}

      <ErrorText error={manifestImport.error} />

      <div className="flex h-96 flex-col">
        <MarkdownEditor
          className="min-h-0 flex-1"
          value={draft}
          onValueChange={setDraft}
          placeholder={"# My project\n\nStack, commands and conventions agents should know."}
        />
      </div>

      <div className="flex items-center gap-3">
        <Button disabled={update.isPending} onClick={handleSave}>
          {update.isPending ? "Saving…" : "Save manifest"}
        </Button>
        {update.isSuccess ? <span className="text-xs text-success">Saved.</span> : null}
      </div>
      <ErrorText error={update.error as Error | null} />
    </div>
  );
}

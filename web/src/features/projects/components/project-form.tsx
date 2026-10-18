"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { ErrorText } from "@/shared/ui/error-text";
import { FolderPickerModal } from "@/features/projects/components/folder-picker-modal";
import type { ProjectFormValues, ProjectResponse } from "@/features/projects/types";

interface ProjectFormProps {
  initial?: ProjectResponse;
  initialPath?: string;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: ProjectFormValues) => void;
  onCancel: () => void;
}

export function ProjectForm({ initial, initialPath, pending, error, onSubmit, onCancel }: ProjectFormProps) {
  const [name, setName] = useState(initial?.name ?? "");
  const [diskPath, setDiskPath] = useState(initial?.diskPath ?? initialPath ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [pickerOpen, setPickerOpen] = useState(false);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({ name: name.trim(), diskPath: diskPath.trim(), description: description.trim() });
  };

  return (
    <>
      <form onSubmit={handleSubmit} className="space-y-3">
      <Field label="Name" htmlFor="project-name">
        <Input
          id="project-name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          required
          placeholder="My project"
        />
      </Field>
      <Field label="Disk path" htmlFor="project-path">
        <div className="flex gap-2">
          <Input
            id="project-path"
            value={diskPath}
            onChange={(event) => setDiskPath(event.target.value)}
            required
            placeholder="E:\\Projects\\my-project"
          />
          <Button
            variant="secondary"
            onClick={() => setPickerOpen(true)}
            className="shrink-0"
          >
            Browse
          </Button>
        </div>
      </Field>
      <Field label="Description" htmlFor="project-description">
        <Input
          id="project-description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          placeholder="Optional"
        />
      </Field>
      <ErrorText error={error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : initial ? "Save changes" : "Create project"}
        </Button>
      </div>
    </form>
      <FolderPickerModal
        title="Choose folder"
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(path) => {
          setPickerOpen(false);
          setDiskPath(path);
        }}
      />
    </>
  );
}

"use client";

import { useState, type FormEvent } from "react";
import { ENTRY_TYPES, type EntryType } from "@/shared/api/types";
import { Markdown } from "@/shared/lib/markdown";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { Textarea } from "@/shared/ui/textarea";
import { WritePreviewToggle } from "@/shared/ui/write-preview-toggle";
import { SkillFilesEditor } from "@/features/entries/components/skill-files-editor";
import type { EntryFormValues, EntryResponse, SkillFileDto } from "@/features/entries/types";

interface EntryEditorProps {
  initial?: EntryResponse;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: EntryFormValues) => void;
  onCancel: () => void;
}

export function EntryEditor({ initial, pending, error, onSubmit, onCancel }: EntryEditorProps) {
  const [type, setType] = useState<EntryType>(initial?.type ?? "note");
  const [title, setTitle] = useState(initial?.title ?? "");
  const [tagsInput, setTagsInput] = useState((initial?.tags ?? []).join(", "));
  const [content, setContent] = useState(initial?.content ?? "");
  const [skillFiles, setSkillFiles] = useState<SkillFileDto[]>(initial?.skillFiles ?? []);
  const [showPreview, setShowPreview] = useState(false);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({
      type,
      title: title.trim(),
      content,
      tags: tagsInput
        .split(",")
        .map((tag) => tag.trim())
        .filter((tag) => tag.length > 0),
      skillFiles,
    });
  };

  const isSkill = type === "skill";

  return (
    <form onSubmit={handleSubmit} className="flex h-full flex-col gap-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[160px_1fr]">
        <Field label="Type" htmlFor="entry-type">
          <Select
            id="entry-type"
            value={type}
            onChange={(event) => setType(event.target.value as EntryType)}
            disabled={initial !== undefined}
            className="w-full"
          >
            {ENTRY_TYPES.map((entryType) => (
              <option key={entryType} value={entryType}>
                {entryType}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Title" htmlFor="entry-title">
          <Input
            id="entry-title"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            required
            placeholder="Short, descriptive title"
          />
        </Field>
      </div>

      <Field label="Tags" htmlFor="entry-tags">
        <Input
          id="entry-tags"
          value={tagsInput}
          onChange={(event) => setTagsInput(event.target.value)}
          placeholder="comma, separated, tags"
        />
      </Field>

      <div className="flex min-h-0 flex-1 flex-col">
        <WritePreviewToggle showPreview={showPreview} onShowPreviewChange={setShowPreview} />
        {showPreview ? (
          <div className="min-h-32 flex-1 overflow-y-auto rounded-md border border-border bg-surface px-3 py-2">
            {content.trim() ? <Markdown content={content} /> : <p className="text-sm text-muted">Nothing to preview.</p>}
          </div>
        ) : (
          <Textarea
            rows={10}
            className="min-h-0 flex-1 resize-none"
            value={content}
            onChange={(event) => setContent(event.target.value)}
            placeholder="Markdown supported: # headings, **bold**, *italic*, `code`, - lists, [links](https://…)"
          />
        )}
      </div>

      {isSkill ? (
        <Field label="Skill files (per platform)">
          <SkillFilesEditor files={skillFiles} onChange={setSkillFiles} />
        </Field>
      ) : null}

      <ErrorText error={error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : initial ? "Save changes" : "Create entry"}
        </Button>
      </div>
    </form>
  );
}

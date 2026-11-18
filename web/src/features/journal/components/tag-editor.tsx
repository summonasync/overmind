"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { entryTagChipStyle } from "@/features/entries";

const TAG_LIST_ID = "journal-known-tags";

interface TagEditorProps {
  tags: readonly string[];
  colors: ReadonlyMap<string, string | null>;
  onAdd: (raw: string) => string | null;
  onRemove: (tag: string) => void;
}

// Passport §4 puts tags in the rail as an editable set. The colours come from the entries
// registry through `entryTagChipStyle`, the same function the inbox paints its chips with, so
// a tag coloured on either surface is coloured on both — and an uncoloured tag stays a plain
// chip rather than an invented default.
export function TagEditor({ tags, colors, onAdd, onRemove }: TagEditorProps) {
  const [draft, setDraft] = useState("");
  const [error, setError] = useState<string | null>(null);
  const known = [...colors.keys()].sort((a, b) => a.localeCompare(b));

  const submit = () => {
    const rejected = onAdd(draft);
    setError(rejected);
    if (rejected === null) {
      setDraft("");
    }
  };

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-1.5">
        {tags.length === 0 ? <p className="text-xs text-muted">No tags yet.</p> : null}
        {tags.map((tag) => (
          <Badge key={tag} size="xs" style={entryTagChipStyle(colors.get(tag) ?? null)}>
            {tag}
            <button
              type="button"
              onClick={() => {
                setError(null);
                onRemove(tag);
              }}
              aria-label={`Remove tag ${tag}`}
              className="rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              <X size={10} aria-hidden />
            </button>
          </Badge>
        ))}
      </div>
      <div className="flex items-start gap-1.5">
        <Input
          list={TAG_LIST_ID}
          aria-label="Add a tag"
          aria-invalid={error !== null}
          aria-describedby={error === null ? undefined : "journal-tag-error"}
          value={draft}
          onChange={(event) => {
            setDraft(event.target.value);
            if (error !== null) {
              setError(null);
            }
          }}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              // This input sits inside no form: Enter means "this tag", and swallowing the
              // default keeps it from reaching the note's own save path.
              event.preventDefault();
              submit();
            }
          }}
          placeholder="tag"
          className="min-w-0 flex-1"
        />
        <Button size="sm" variant="secondary" onClick={submit} disabled={draft.trim().length === 0}>
          Add
        </Button>
      </div>
      <datalist id={TAG_LIST_ID}>
        {known.map((tag) => (
          <option key={tag} value={tag} />
        ))}
      </datalist>
      {error !== null ? (
        <p id="journal-tag-error" role="alert" className="text-xs text-danger">
          {error}
        </p>
      ) : null}
    </div>
  );
}

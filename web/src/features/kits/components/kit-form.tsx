"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import type { KitFormValues, KitResponse } from "@/features/kits/types";

interface KitFormProps {
  initial?: KitResponse;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: KitFormValues) => void;
  onCancel: () => void;
}

export function KitForm({ initial, pending, error, onSubmit, onCancel }: KitFormProps) {
  const [name, setName] = useState(initial?.name ?? "");
  const [version, setVersion] = useState(initial?.version ?? 1);
  const [description, setDescription] = useState(initial?.description ?? "");

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit({ name: name.trim(), version, description: description.trim() });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_100px]">
        <Field label="Name" htmlFor="kit-name">
          <Input
            id="kit-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            placeholder="csharp-rules"
          />
        </Field>
        <Field label="Version" htmlFor="kit-version">
          <Input
            id="kit-version"
            type="number"
            min={1}
            step={1}
            value={version}
            onChange={(event) => setVersion(Number.parseInt(event.target.value, 10) || 1)}
            required
          />
        </Field>
      </div>
      <Field label="Description" htmlFor="kit-description">
        <Input
          id="kit-description"
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
          {pending ? "Saving…" : initial ? "Save changes" : "Create kit"}
        </Button>
      </div>
    </form>
  );
}

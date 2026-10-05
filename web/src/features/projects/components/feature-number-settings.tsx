"use client";

import { useState, type FormEvent } from "react";
import {
  FEATURE_NUMBER_STYLES,
  formatFeatureNumber,
  isValidFeatureNumberPrefix,
  type FeatureNumberStyle,
} from "@/shared/lib/feature-number";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { useProjectMutations } from "@/features/projects/lib/use-projects";
import type { ProjectResponse } from "@/features/projects/types";

interface FeatureNumberSettingsProps {
  project: ProjectResponse;
  previewSlug: string | null;
}

export function FeatureNumberSettings({ project, previewSlug }: FeatureNumberSettingsProps) {
  const { update } = useProjectMutations();
  const [syncedStyle, setSyncedStyle] = useState(project.numberStyle);
  const [syncedPrefix, setSyncedPrefix] = useState(project.numberPrefix);
  const [style, setStyle] = useState<FeatureNumberStyle>(project.numberStyle);
  const [prefix, setPrefix] = useState(project.numberPrefix ?? "");

  if (project.numberStyle !== syncedStyle || project.numberPrefix !== syncedPrefix) {
    setSyncedStyle(project.numberStyle);
    setSyncedPrefix(project.numberPrefix);
    setStyle(project.numberStyle);
    setPrefix(project.numberPrefix ?? "");
  }

  const trimmedPrefix = prefix.trim();
  const prefixReady = style !== "key" || isValidFeatureNumberPrefix(trimmedPrefix);
  const preview = previewSlug === null ? null : formatFeatureNumber(previewSlug, style, trimmedPrefix || null);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    update.mutate({
      id: project.id,
      values: {
        name: project.name,
        diskPath: project.diskPath,
        description: project.description,
        numberStyle: style,
        ...(style === "key" ? { numberPrefix: trimmedPrefix } : {}),
      },
    });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <p className="text-xs text-muted">
        Shapes the identifier on board cards, task rows and notifications. Plain shows the bare
        number, hash keeps the #NNN look, key puts a short project code in front.
      </p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Number style" htmlFor="feature-number-style">
          <Select
            id="feature-number-style"
            value={style}
            onChange={(event) => setStyle(event.target.value as FeatureNumberStyle)}
            className="w-full"
          >
            {FEATURE_NUMBER_STYLES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </Field>
        {style === "key" ? (
          <Field label="Prefix" htmlFor="feature-number-prefix">
            <Input
              id="feature-number-prefix"
              value={prefix}
              maxLength={16}
              placeholder="KEY"
              className="w-full font-mono"
              onChange={(event) => setPrefix(event.target.value.toUpperCase())}
            />
          </Field>
        ) : null}
      </div>
      {style === "key" && !prefixReady ? (
        <p className="text-xs text-warning" role="note">
          Prefix needs 2–16 characters: one uppercase letter, then uppercase letters, digits or
          underscores.
        </p>
      ) : null}
      {preview !== null ? (
        <p className="text-xs text-muted" role="status">
          Looks like{" "}
          <span className="font-mono font-semibold text-foreground">{preview}</span> on{" "}
          <span className="font-mono">{previewSlug}</span>
        </p>
      ) : null}
      <div className="flex items-center gap-3">
        <Button type="submit" disabled={update.isPending || !prefixReady}>
          {update.isPending ? "Saving…" : "Save numbers"}
        </Button>
        {update.isSuccess ? <span className="text-xs text-success">Saved.</span> : null}
      </div>
      <ErrorText error={update.error as Error | null} />
    </form>
  );
}

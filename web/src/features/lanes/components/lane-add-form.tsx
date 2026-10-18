"use client";

import { useState, type FormEvent } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { useLaneMutations } from "@/features/lanes/lib/use-lane-mutations";
import { validateLaneName } from "@/features/lanes/lib/lane-roster";
import type { LaneRow } from "@/features/lanes/types";

interface LaneAddFormProps {
  projectId: string;
  roster: LaneRow[];
}

export function LaneAddForm({ projectId, roster }: LaneAddFormProps) {
  const [name, setName] = useState("");
  const [contour, setContour] = useState("");
  const { createLane } = useLaneMutations(projectId);

  const validationError = validateLaneName(name, roster);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (validationError !== null) {
      return;
    }
    createLane.mutate(
      { name, contour: contour.trim().length > 0 ? contour : null, roster },
      {
        onSuccess: () => {
          setName("");
          setContour("");
        },
      },
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <Input
          value={name}
          onChange={(event) => setName(event.target.value)}
          placeholder="Lane name"
          aria-label="Lane name"
          maxLength={64}
          className="w-48"
        />
        <Input
          value={contour}
          onChange={(event) => setContour(event.target.value)}
          placeholder="Contour (optional)"
          aria-label="Lane contour"
          className="w-56"
        />
        <Button type="submit" disabled={validationError !== null || createLane.isPending}>
          <Plus size={14} aria-hidden />
          {createLane.isPending ? "Adding…" : "Add lane"}
        </Button>
      </div>
      {validationError !== null ? (
        <p className="text-xs text-danger" role="alert">
          {validationError}
        </p>
      ) : (
        <ErrorText error={createLane.error} />
      )}
    </form>
  );
}

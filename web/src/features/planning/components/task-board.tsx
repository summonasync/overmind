"use client";

import { useState, type ComponentType } from "react";
import { Ban, CheckCircle2, Eye, Play, Undo2 } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { Textarea } from "@/shared/ui/textarea";
import { taskHeading } from "@/features/planning/lib/task-heading";
import type { PlanningArtifactResponse, PlanningArtifactStatus } from "@/features/planning/types";

const COLUMNS: { status: PlanningArtifactStatus; label: string }[] = [
  { status: "todo", label: "To do" },
  { status: "inProgress", label: "In progress" },
  { status: "review", label: "In review" },
  { status: "done", label: "Done" },
  { status: "blocked", label: "Blocked" },
];

function columnLabel(status: PlanningArtifactStatus): string {
  return COLUMNS.find((column) => column.status === status)?.label ?? status;
}

function moveTargetIcon(status: PlanningArtifactStatus): ComponentType<{ size?: number }> {
  switch (status) {
    case "todo":
      return Undo2;
    case "inProgress":
      return Play;
    case "review":
      return Eye;
    case "done":
      return CheckCircle2;
    default:
      return Ban;
  }
}

interface TaskMoveInput {
  artifact: PlanningArtifactResponse;
  status: PlanningArtifactStatus;
  logNote: string;
}

interface TaskBoardProps {
  tasks: PlanningArtifactResponse[];
  pending: boolean;
  error: Error | null;
  onOpen: (artifact: PlanningArtifactResponse) => void;
  onMove: (input: TaskMoveInput) => void;
}

export function TaskBoard({ tasks, pending, error, onOpen, onMove }: TaskBoardProps) {
  const [moving, setMoving] = useState<{ artifact: PlanningArtifactResponse; status: PlanningArtifactStatus } | null>(
    null,
  );
  const [note, setNote] = useState("");

  const openMove = (artifact: PlanningArtifactResponse, status: PlanningArtifactStatus) => {
    setNote("");
    setMoving({ artifact, status });
  };

  const confirmMove = () => {
    if (moving === null || note.trim().length === 0) {
      return;
    }
    onMove({ ...moving, logNote: note.trim() });
    setMoving(null);
  };

  return (
    <div className="space-y-3">
      <ErrorText error={error} />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {COLUMNS.map((column) => {
          const columnTasks = tasks.filter((task) => task.status === column.status);
          return (
            <div key={column.status} className="rounded-lg border border-border bg-surface/50 p-2">
              <h3 className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-muted">
                {column.label}
                <span className="ml-1 font-mono">{columnTasks.length}</span>
              </h3>
              {columnTasks.length === 0 ? (
                <div className="min-h-16 rounded-md border border-dashed border-border" />
              ) : (
                <ul className="space-y-2">
                  {columnTasks.map((task) => (
                    <TaskCard key={task.id} task={task} onOpen={onOpen} onMove={openMove} />
                  ))}
                </ul>
              )}
            </div>
          );
        })}
      </div>

      <Modal title="Move task" open={moving !== null} onClose={() => setMoving(null)}>
        {moving ? (
          <div className="space-y-3">
            <p className="text-sm text-foreground">
              Move <span className="font-medium">{taskHeading(moving.artifact.body, moving.artifact.order)}</span>{" "}
              to <span className="font-medium">{columnLabel(moving.status)}</span>.
            </p>
            <Textarea
              rows={3}
              value={note}
              onChange={(event) => setNote(event.target.value)}
              placeholder="Log note (required) — what happened on this move?"
              autoFocus
            />
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setMoving(null)}>
                Cancel
              </Button>
              <Button disabled={note.trim().length === 0 || pending} onClick={confirmMove}>
                Move
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </div>
  );
}

interface TaskCardProps {
  task: PlanningArtifactResponse;
  onOpen: (artifact: PlanningArtifactResponse) => void;
  onMove: (artifact: PlanningArtifactResponse, status: PlanningArtifactStatus) => void;
}

function TaskCard({ task, onOpen, onMove }: TaskCardProps) {
  return (
    <li className="rounded-md border border-border bg-surface p-2.5">
      <button
        type="button"
        onClick={() => onOpen(task)}
        className="block w-full rounded-sm text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <span className="block text-sm font-medium text-foreground">
          {taskHeading(task.body, task.order)}
        </span>
        <span className="mt-1.5 flex items-center gap-1.5">
          <Badge>#{task.order}</Badge>
          <Badge tone={task.review === "hard" ? "warning" : "neutral"}>{task.review}</Badge>
        </span>
      </button>
      <div className="mt-2 flex flex-wrap gap-0.5 border-t border-border pt-2">
        {COLUMNS.filter((column) => column.status !== task.status).map((column) => {
          const Icon = moveTargetIcon(column.status);
          return (
            <button
              key={column.status}
              type="button"
              aria-label={`Move to ${column.label}`}
              title={`Move to ${column.label}`}
              onClick={() => onMove(task, column.status)}
              className="rounded p-1 text-muted hover:bg-surface-raised hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              <Icon size={13} aria-hidden />
            </button>
          );
        })}
      </div>
    </li>
  );
}

import { ChevronRight } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Badge, type BadgeTone } from "@/shared/ui/badge";
import { artifactStatusTone, featureStatusTone } from "@/features/planning/lib/status-tone";
import type {
  PlanningArtifactResponse,
  PlanningDocumentKind,
  PlanningFeatureResponse,
  PlanningFeatureStatus,
  PlanningTab,
} from "@/features/planning/types";

const TONE_DOTS: Record<BadgeTone, string> = {
  neutral: "bg-border-strong",
  info: "bg-info",
  warning: "bg-warning",
  success: "bg-success",
  danger: "bg-danger",
  skill: "bg-type-skill",
};

interface CeremonyStepperProps {
  feature: PlanningFeatureResponse;
  activeTab: PlanningTab;
  onSelect: (tab: PlanningTab) => void;
}

export function CeremonyStepper({ feature, activeTab, onSelect }: CeremonyStepperProps) {
  const tasks = feature.artifacts.filter((artifact) => artifact.kind === "task");
  const doneCount = tasks.filter((task) => task.status === "done").length;

  return (
    <nav
      aria-label="Ceremony progress"
      className="flex flex-wrap items-center gap-x-1 gap-y-2 rounded-lg border border-border bg-surface/50 px-3 py-2"
    >
      <FeatureAnchor title={feature.title} status={feature.status} />
      <StageSeparator />
      <DocumentStage
        kind="spec"
        artifact={feature.artifacts.find((candidate) => candidate.kind === "spec")}
        active={activeTab === "spec"}
        onSelect={onSelect}
      />
      <StageSeparator />
      <DocumentStage
        kind="plan"
        artifact={feature.artifacts.find((candidate) => candidate.kind === "plan")}
        active={activeTab === "plan"}
        onSelect={onSelect}
      />
      <StageSeparator />
      <TasksStage total={tasks.length} done={doneCount} active={activeTab === "tasks"} onSelect={onSelect} />
      <StageSeparator />
      <DocumentStage
        kind="result"
        artifact={feature.artifacts.find((candidate) => candidate.kind === "result")}
        active={activeTab === "result"}
        onSelect={onSelect}
      />
    </nav>
  );
}

function FeatureAnchor({ title, status }: { title: string; status: PlanningFeatureStatus }) {
  return (
    <span className="flex min-w-0 items-center gap-1.5 whitespace-nowrap px-2 py-1 text-sm">
      <ToneDot tone={featureStatusTone(status)} />
      <span className="max-w-64 truncate font-medium text-foreground">{title}</span>
    </span>
  );
}

interface DocumentStageProps {
  kind: PlanningDocumentKind;
  artifact: PlanningArtifactResponse | undefined;
  active: boolean;
  onSelect: (tab: PlanningTab) => void;
}

function DocumentStage({ kind, artifact, active, onSelect }: DocumentStageProps) {
  const tone = artifact === undefined ? "neutral" : artifactStatusTone(artifact.status);
  return (
    <button
      type="button"
      onClick={() => onSelect(kind)}
      aria-current={active ? "step" : undefined}
      className={stageClassName(active)}
    >
      <ToneDot tone={tone} />
      {kind}
      <Badge tone={tone}>{artifact === undefined ? "absent" : artifact.status}</Badge>
    </button>
  );
}

interface TasksStageProps {
  total: number;
  done: number;
  active: boolean;
  onSelect: (tab: PlanningTab) => void;
}

function TasksStage({ total, done, active, onSelect }: TasksStageProps) {
  const { label, tone } = tasksCounter(done, total);
  return (
    <button
      type="button"
      onClick={() => onSelect("tasks")}
      aria-current={active ? "step" : undefined}
      className={stageClassName(active)}
    >
      <ToneDot tone={tone} />
      tasks
      <Badge tone={tone}>{label}</Badge>
    </button>
  );
}

function tasksCounter(done: number, total: number): { label: string; tone: BadgeTone } {
  if (total === 0) {
    return { label: "todo", tone: artifactStatusTone("todo") };
  }
  if (done === total) {
    return { label: `done ${done}/${total}`, tone: artifactStatusTone("done") };
  }
  return { label: `${done}/${total}`, tone: "info" };
}

function stageClassName(active: boolean): string {
  return cn(
    "flex items-center gap-1.5 whitespace-nowrap rounded-md border px-2 py-1 text-sm font-medium",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
    active
      ? "border-primary/40 bg-primary/10 text-foreground"
      : "border-transparent text-muted hover:text-foreground",
  );
}

function ToneDot({ tone }: { tone: BadgeTone }) {
  return <span aria-hidden className={cn("h-1.5 w-1.5 shrink-0 rounded-full", TONE_DOTS[tone])} />;
}

function StageSeparator() {
  return <ChevronRight size={14} aria-hidden className="shrink-0 text-muted/50" />;
}

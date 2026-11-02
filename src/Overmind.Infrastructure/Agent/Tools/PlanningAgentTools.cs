using System.Text.Json;

namespace Overmind.Infrastructure.Agent.Tools;

public static class PlanningAgentTools
{
    private static string ProjectPath(Guid projectId) => $"/api/agent/projects/{projectId}/planning";

    public static IReadOnlyList<AgentToolSpec> Build(AgentLoopbackClient api)
    {
        return
        [
            BoardSpec(api),
            TaskQueueSpec(api),
            ReadFeatureSpec(api),
            CreateFeatureSpec(api),
            UpdateFeatureSpec(api),
            SetFeatureStatusSpec(api),
            SetFeaturePrioritySpec(api),
            MoveFeatureSpec(api),
            CurrentTaskSpec(api),
            ListArtifactsSpec(api),
            ReadArtifactSpec(api),
            ReadArtifactDiffSpec(api),
            UpsertArtifactSpec(api),
            UpsertTasksSpec(api),
            PatchArtifactSpec(api),
            SetArtifactStatusSpec(api),
            AppendLogSpec(api),
            EditLogSpec(api),
            DeleteArtifactSpec(api),
            ClaimSpec(api),
            UnclaimSpec(api),
            ListDependenciesSpec(api),
            AddDependencySpec(api),
            RemoveDependencySpec(api),
            PinCodeSpec(api),
            AppendDirectiveSpec(api),
            AckDirectiveSpec(api),
            MaterializeSpec(api),
            MaterializeStatusSpec(api),
            ImportFeatureSpec(api),
            ExportBacklogSpec(api),
            ValidateSpec(api)
        ];
    }

    private static AgentToolSpec BoardSpec(AgentLoopbackClient api) => new(
        "planning_list_features",
        "Reads the planning board of a project — feature slugs, statuses, priorities, spec/plan validation, task progress and claim state. Optional filters: status, priority, lane, claimedByAlias, unclaimedOnly.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"status":{"type":"string"},"priority":{"type":"string"},"lane":{"type":"string"},"claimedByAlias":{"type":"string"},"unclaimedOnly":{"type":"boolean"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery(ProjectPath(AgentToolArgs.RequiredProjectId(args)) + "/features", args, "status", "priority", "lane", "claimedByAlias", "unclaimedOnly"), ct));

    private static AgentToolSpec TaskQueueSpec(AgentLoopbackClient api) => new(
        "planning_task_queue",
        "Reads the planning queue of a project — what's next, sorted by priority then queue position. The work-order projection of the board.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(ProjectPath(AgentToolArgs.RequiredProjectId(args)) + "/task-queue", ct));

    private static AgentToolSpec ReadFeatureSpec(AgentLoopbackClient api) => new(
        "planning_read_feature",
        "Reads one feature in full: body plus every spec, plan, task and result artifact with statuses and logs. Pass includeArtifacts=false for the cheap body-plus-roster read.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"includeArtifacts":{"type":"boolean"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery($"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}", args, "includeArtifacts"), ct));

    private static AgentToolSpec CreateFeatureSpec(AgentLoopbackClient api) => new(
        "planning_create_feature",
        "Creates a new feature on the planning board: durable what/why in the body. Optional slug (auto-generated when omitted) and priority p0-p3. Follow with planning_upsert_artifact for the spec.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"title":{"type":"string"},"slug":{"type":"string"},"body":{"type":"string"},"priority":{"type":"string"}},"required":["projectId","title"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features",
            new AgentToolPayload()
                .Set("title", AgentToolArgs.RequiredString(args, "title"))
                .Set("slug", AgentToolArgs.OptionalString(args, "slug"))
                .Set("body", AgentToolArgs.OptionalString(args, "body"))
                .Set("priority", AgentToolArgs.OptionalString(args, "priority"))
                .Build(),
            ct));

    private static AgentToolSpec UpdateFeatureSpec(AgentLoopbackClient api) => new(
        "planning_update_feature",
        "Partial edit of a feature: title, body, priority, lane or codeSurfaces. logNote is mandatory and lands in the feature log — always say why. A priority change moves the feature to the tail of its band.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"title":{"type":"string"},"body":{"type":"string"},"priority":{"type":"string"},"lane":{"type":"string"},"codeSurfaces":{"type":"array","items":{"type":"string"}},"logNote":{"type":"string"}},"required":["projectId","slug","logNote"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/update",
            new AgentToolPayload()
                .Set("title", AgentToolArgs.OptionalString(args, "title"))
                .Set("body", AgentToolArgs.OptionalString(args, "body"))
                .Set("priority", AgentToolArgs.OptionalString(args, "priority"))
                .Set("lane", AgentToolArgs.OptionalString(args, "lane"))
                .Set("codeSurfaces", AgentToolArgs.OptionalRaw(args, "codeSurfaces"))
                .Set("logNote", AgentToolArgs.OptionalString(args, "logNote"))
                .Build(),
            ct));

    private static AgentToolSpec SetFeatureStatusSpec(AgentLoopbackClient api) => new(
        "planning_set_feature_status",
        "Moves a feature status: done closes the feature (needs a result artifact plus human approval — refused otherwise), inProgress rolls back from review. logNote is mandatory and carries the provenance.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"status":{"type":"string"},"logNote":{"type":"string"}},"required":["projectId","slug","status","logNote"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/status",
            new
            {
                status = AgentToolArgs.RequiredString(args, "status"),
                logNote = AgentToolArgs.RequiredString(args, "logNote")
            },
            ct));

    private static AgentToolSpec SetFeaturePrioritySpec(AgentLoopbackClient api) => new(
        "planning_set_feature_priority",
        "Changes a feature priority (p0-p3) — band crossing that re-enters at the tail of the new band. Only with the user's explicit approval in the conversation; never on your own initiative.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"priority":{"type":"string"}},"required":["projectId","slug","priority"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/priority",
            new { priority = AgentToolArgs.RequiredString(args, "priority") },
            ct));

    private static AgentToolSpec MoveFeatureSpec(AgentLoopbackClient api) => new(
        "planning_move_feature",
        "Moves a feature within its priority band on the queue: up, down, first, last or toPosition (1-based, then position is required).",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"move":{"type":"string"},"position":{"type":"integer"}},"required":["projectId","slug","move"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/queue-move",
            new AgentToolPayload()
                .Set("move", AgentToolArgs.OptionalString(args, "move"))
                .Set("position", AgentToolArgs.OptionalInt(args, "position"))
                .Build(),
            ct));

    private static AgentToolSpec CurrentTaskSpec(AgentLoopbackClient api) => new(
        "planning_current_task",
        "Reads the current task of a feature, or a stop/held/empty state whose reason leads the payload. Obey a STOP line — ask the user, never work around it.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.GetAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/current", ct));

    private static AgentToolSpec ListArtifactsSpec(AgentLoopbackClient api) => new(
        "planning_list_artifacts",
        "Metadata-only roster of one feature's artifacts — the cheapest way to see what a feature contains. Optional kind filter: spec, plan, task or result.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"kind":{"type":"string"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery($"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/artifacts", args, "kind"), ct));

    private static AgentToolSpec ReadArtifactSpec(AgentLoopbackClient api) => new(
        "planning_read_artifact",
        "Reads one artifact by id — full body, or pass sections for the cheap targeted read (e.g. sections=log,obstacles).",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"sections":{"type":"string"}},"required":["artifactId"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery($"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}", args, "sections"), ct));

    private static AgentToolSpec ReadArtifactDiffSpec(AgentLoopbackClient api) => new(
        "planning_read_artifact_diff",
        "Diffs two versions of one artifact as a unified diff; adjacent versions may be log-only bumps (empty diff is a valid answer).",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"fromVersion":{"type":"integer"},"toVersion":{"type":"integer"}},"required":["artifactId"]}
        """,
        async (args, ct) => await api.GetAsync(
            AgentToolArgs.BuildQuery($"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/diff", args, "fromVersion", "toVersion"), ct));

    private static AgentToolSpec UpsertArtifactSpec(AgentLoopbackClient api) => new(
        "planning_upsert_artifact",
        "Creates or updates one artifact: kind spec, plan, task or result. Tasks take order, review (light/hard), brief, blockedBy (sibling task orders) — gaps in order are legal, never renumber to close them. dryRun=true validates without persisting.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"kind":{"type":"string"},"body":{"type":"string"},"order":{"type":"integer"},"review":{"type":"string"},"brief":{"type":"string"},"blockedBy":{"type":"array","items":{"type":"integer"}},"logNote":{"type":"string"},"dryRun":{"type":"boolean"}},"required":["projectId","slug","kind","body"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/artifacts",
            new AgentToolPayload()
                .Set("kind", AgentToolArgs.OptionalString(args, "kind"))
                .Set("body", AgentToolArgs.OptionalString(args, "body"))
                .Set("order", AgentToolArgs.OptionalInt(args, "order"))
                .Set("review", AgentToolArgs.OptionalString(args, "review"))
                .Set("brief", AgentToolArgs.OptionalString(args, "brief"))
                .Set("blockedBy", AgentToolArgs.OptionalIntList(args, "blockedBy"))
                .Set("logNote", AgentToolArgs.OptionalString(args, "logNote"))
                .Set("dryRun", AgentToolArgs.OptionalBool(args, "dryRun"))
                .Build(),
            ct));

    private static AgentToolSpec UpsertTasksSpec(AgentLoopbackClient api) => new(
        "planning_upsert_tasks",
        "Creates or updates several task artifacts of a feature in one atomic batch — all-or-nothing. Each task: body (required), order, review, brief, blockedBy, logNote. dryRun=true validates the whole batch without persisting.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"tasks":{"type":"array","items":{"type":"object"}},"dryRun":{"type":"boolean"}},"required":["projectId","slug","tasks"]}
        """,
        async (args, ct) =>
        {
            var rawTasks = AgentToolArgs.OptionalRaw(args, "tasks");
            if (rawTasks is not { } tasks || tasks.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException("Argument 'tasks' must be an array of task objects.");
            }

            return await api.PostAsync(
                $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/artifacts/tasks",
                new AgentToolPayload()
                    .Set("tasks", tasks)
                    .Set("dryRun", AgentToolArgs.OptionalBool(args, "dryRun"))
                    .Build(),
                ct);
        });

    private static AgentToolSpec PatchArtifactSpec(AgentLoopbackClient api) => new(
        "planning_patch_artifact",
        "Corrects one artifact in place without resending the body: replace/insert one XML section or one '## Heading' block, tick or untick one acceptance item (acIndex or acText), set a task brief, append a result amendment (amend=true), or tombstone a heading (remove=true + reason). logNote is mandatory once the artifact is validated.",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"section":{"type":"string"},"content":{"type":"string"},"acIndex":{"type":"integer"},"acText":{"type":"string"},"untick":{"type":"boolean"},"brief":{"type":"string"},"amend":{"type":"boolean"},"remove":{"type":"boolean"},"reason":{"type":"string"},"logNote":{"type":"string"},"source":{"type":"string"},"alias":{"type":"string"},"projectId":{"type":"string"},"operationId":{"type":"string"}},"required":["artifactId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/patch",
            new AgentToolPayload()
                .Set("section", AgentToolArgs.OptionalString(args, "section"))
                .Set("content", AgentToolArgs.OptionalString(args, "content"))
                .Set("acIndex", AgentToolArgs.OptionalInt(args, "acIndex"))
                .Set("acText", AgentToolArgs.OptionalString(args, "acText"))
                .Set("untick", AgentToolArgs.OptionalBool(args, "untick"))
                .Set("brief", AgentToolArgs.OptionalString(args, "brief"))
                .Set("amend", AgentToolArgs.OptionalBool(args, "amend"))
                .Set("remove", AgentToolArgs.OptionalBool(args, "remove"))
                .Set("reason", AgentToolArgs.OptionalString(args, "reason"))
                .Set("logNote", AgentToolArgs.OptionalString(args, "logNote"))
                .Set("source", AgentToolArgs.OptionalString(args, "source"))
                .Set("alias", AgentToolArgs.OptionalString(args, "alias"))
                .Set("projectId", AgentToolArgs.OptionalGuid(args, "projectId"))
                .Set("operationId", AgentToolArgs.OptionalGuid(args, "operationId"))
                .Build(),
            ct));

    private static AgentToolSpec SetArtifactStatusSpec(AgentLoopbackClient api) => new(
        "planning_set_status",
        "Moves one artifact status with a mandatory logNote. Spec/plan: draft, validated (validated is human-gated — a draft spec or plan of yours waits for the human door). Task: todo, inProgress, review, done, blocked.",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"status":{"type":"string"},"logNote":{"type":"string"}},"required":["artifactId","status","logNote"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/status",
            new
            {
                status = AgentToolArgs.RequiredString(args, "status"),
                logNote = AgentToolArgs.RequiredString(args, "logNote")
            },
            ct));

    private static AgentToolSpec AppendLogSpec(AgentLoopbackClient api) => new(
        "planning_append_log",
        "Appends one dated log line to an artifact without touching its body — progress notes, obstacle mentions, AC evidence. Address the artifact by id, or by alias ('<slug>/T<order>', '<slug>/S') plus projectId.",
        """
        {"type":"object","properties":{"note":{"type":"string"},"artifactId":{"type":"string"},"alias":{"type":"string"},"projectId":{"type":"string"},"operationId":{"type":"string"}},"required":["note"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/log",
            new AgentToolPayload()
                .Set("note", AgentToolArgs.OptionalString(args, "note"))
                .Set("alias", AgentToolArgs.OptionalString(args, "alias"))
                .Set("projectId", AgentToolArgs.OptionalGuid(args, "projectId"))
                .Set("operationId", AgentToolArgs.OptionalGuid(args, "operationId"))
                .Build(),
            ct));

    private static AgentToolSpec EditLogSpec(AgentLoopbackClient api) => new(
        "planning_edit_log",
        "Corrects the newest log line on one artifact — single-line replacement; the date and actor marker are preserved, only the prose changes.",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"text":{"type":"string"},"alias":{"type":"string"},"projectId":{"type":"string"},"operationId":{"type":"string"}},"required":["artifactId","text"]}
        """,
        async (args, ct) => await api.PutAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/log",
            new AgentToolPayload()
                .Set("text", AgentToolArgs.OptionalString(args, "text"))
                .Set("alias", AgentToolArgs.OptionalString(args, "alias"))
                .Set("projectId", AgentToolArgs.OptionalGuid(args, "projectId"))
                .Set("operationId", AgentToolArgs.OptionalGuid(args, "operationId"))
                .Build(),
            ct));

    private static AgentToolSpec DeleteArtifactSpec(AgentLoopbackClient api) => new(
        "planning_delete_artifact",
        "Soft-deletes one artifact as a draft only; validated artifacts are the human's door. A task with dependents is refused until its edges are cleaned up. logNote carries the reason into the feature log.",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"logNote":{"type":"string"},"source":{"type":"string"},"alias":{"type":"string"},"projectId":{"type":"string"}},"required":["artifactId"]}
        """,
        async (args, ct) => await api.DeleteAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}",
            new AgentToolPayload()
                .Set("logNote", AgentToolArgs.OptionalString(args, "logNote"))
                .Set("source", AgentToolArgs.OptionalString(args, "source"))
                .Set("alias", AgentToolArgs.OptionalString(args, "alias"))
                .Set("projectId", AgentToolArgs.OptionalGuid(args, "projectId"))
                .Build(),
            ct));

    private static AgentToolSpec ClaimSpec(AgentLoopbackClient api) => new(
        "planning_claim_feature",
        "Claims a feature for the calling session — a hard mutation lock backed by session liveness. A live foreign claim 409s naming the holder; a stale or ended claim can be taken over. overrideLaneCheck=true is the explicit lane escape hatch.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"alias":{"type":"string"},"vendor":{"type":"string"},"overrideLaneCheck":{"type":"boolean"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/claim",
            new AgentToolPayload()
                .Set("alias", AgentToolArgs.OptionalString(args, "alias"))
                .Set("vendor", AgentToolArgs.OptionalString(args, "vendor"))
                .Set("overrideLaneCheck", AgentToolArgs.OptionalBool(args, "overrideLaneCheck"))
                .Build(),
            ct));

    private static AgentToolSpec UnclaimSpec(AgentLoopbackClient api) => new(
        "planning_unclaim_feature",
        "Releases the caller's claim on a feature — holder session only; releasing an unclaimed feature is a no-op.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/unclaim",
            new { },
            ct));

    private static AgentToolSpec ListDependenciesSpec(AgentLoopbackClient api) => new(
        "planning_list_feature_dependencies",
        "Reads a feature's dependency graph: blockedBy (what it waits for) and blocks (what waits on it); a dependency closes when its blocker reaches done or review.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.GetAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/dependencies", ct));

    private static AgentToolSpec AddDependencySpec(AgentLoopbackClient api) => new(
        "planning_add_feature_dependency",
        "Adds a blocker to a feature — the feature stops issuing tasks until the blocker closes. Same project only; cycles are refused with the chain named.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"blockedBySlug":{"type":"string"}},"required":["projectId","slug","blockedBySlug"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/dependencies",
            new { blockedBySlug = AgentToolArgs.RequiredString(args, "blockedBySlug") },
            ct));

    private static AgentToolSpec RemoveDependencySpec(AgentLoopbackClient api) => new(
        "planning_remove_feature_dependency",
        "Removes one blocker from a feature; removing a link that is not there is a no-op.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"blockedBySlug":{"type":"string"}},"required":["projectId","slug","blockedBySlug"]}
        """,
        async (args, ct) => await api.DeleteAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/dependencies/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "blockedBySlug"))}",
            ct));

    private static AgentToolSpec PinCodeSpec(AgentLoopbackClient api) => new(
        "planning_pin_feature_code",
        "Pins the explicit code location of a feature: a branch and/or worktree path stored on the feature. Upsert semantics — last write wins; send nothing to clear.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"branch":{"type":"string"},"worktreePath":{"type":"string"}},"required":["projectId","slug"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/pin-code",
            new AgentToolPayload()
                .Set("branch", AgentToolArgs.OptionalString(args, "branch"))
                .Set("worktreePath", AgentToolArgs.OptionalString(args, "worktreePath"))
                .Build(),
            ct));

    private static AgentToolSpec AppendDirectiveSpec(AgentLoopbackClient api) => new(
        "planning_append_directive",
        "Appends a coordination directive to a feature's open directive channel — the one write that crosses the claim lock. Any session may append; the holder sees and acknowledges it.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"text":{"type":"string"}},"required":["projectId","slug","text"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/directives",
            new { text = AgentToolArgs.RequiredString(args, "text") },
            ct));

    private static AgentToolSpec AckDirectiveSpec(AgentLoopbackClient api) => new(
        "planning_ack_directive",
        "Acknowledges one open directive on a feature — marks it seen; append-only, nothing else changes.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"slug":{"type":"string"},"directiveId":{"type":"string"}},"required":["projectId","slug","directiveId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/features/{Uri.EscapeDataString(AgentToolArgs.RequiredString(args, "slug"))}/directives/{AgentToolArgs.RequiredGuid(args, "directiveId")}/ack",
            new { },
            ct));

    private static AgentToolSpec MaterializeSpec(AgentLoopbackClient api) => new(
        "planning_materialize",
        "Triggers a materialization pass: the full plan is computed then written atomically to BACKLOG/; any guarded file aborts the whole run with a 409 listing every refusal. force=true adopts guarded files with warnings — transcribe hand edits to the board first.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"force":{"type":"boolean"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/projects/{AgentToolArgs.RequiredProjectId(args)}/planning/materialize",
            new { force = AgentToolArgs.OptionalBool(args, "force") },
            ct));

    private static AgentToolSpec MaterializeStatusSpec(AgentLoopbackClient api) => new(
        "planning_materialize_status",
        "Mirror diagnostics without filesystem access: last success vs last attempt, per-file report, tracked files, stale paths and strategy. Read-only.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"}},"required":["projectId"]}
        """,
        async (args, ct) => await api.GetAsync(
            ProjectPath(AgentToolArgs.RequiredProjectId(args)) + "/materialization-status", ct));

    private static AgentToolSpec ImportFeatureSpec(AgentLoopbackClient api) => new(
        "planning_import_feature",
        "Imports one materialized feature folder from disk in a single call — bodies never pass through the model. Path is the absolute folder containing feature.md, under the project root.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"path":{"type":"string"}},"required":["projectId","path"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/import",
            new { path = AgentToolArgs.RequiredString(args, "path") },
            ct));

    private static AgentToolSpec ExportBacklogSpec(AgentLoopbackClient api) => new(
        "planning_export_backlog",
        "Renders the full materialized mirror into an explicit empty target directory as greppable plain files — a snapshot, not a sync. The target must be absolute, empty and outside BACKLOG/ and ENTRIES/.",
        """
        {"type":"object","properties":{"projectId":{"type":"string"},"targetPath":{"type":"string"}},"required":["projectId","targetPath"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"{ProjectPath(AgentToolArgs.RequiredProjectId(args))}/export",
            new { targetPath = AgentToolArgs.RequiredString(args, "targetPath") },
            ct));

    private static AgentToolSpec ValidateSpec(AgentLoopbackClient api) => new(
        "planning_validate",
        "Validates a spec, plan or result artifact. Reserved for delegated approvals: the embedded loop must not call this until delegated approvals are enabled — the server still requires human proof and returns 409 without it.",
        """
        {"type":"object","properties":{"artifactId":{"type":"string"},"asMigrated":{"type":"boolean"},"logNote":{"type":"string"}},"required":["artifactId"]}
        """,
        async (args, ct) => await api.PostAsync(
            $"/api/agent/planning/artifacts/{AgentToolArgs.RequiredGuid(args, "artifactId")}/validate",
            new AgentToolPayload()
                .Set("asMigrated", AgentToolArgs.OptionalBool(args, "asMigrated"))
                .Set("logNote", AgentToolArgs.OptionalString(args, "logNote"))
                .Build(),
            ct),
        AgentToolCategory.HumanGated);

}

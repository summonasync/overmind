"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Globe, ShieldAlert } from "lucide-react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { LoadingState } from "@/shared/ui/loading-state";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { deployKitGlobal, removeKitGlobal } from "@/features/kits/api";
import { globalStateQueryOptions, kitKeys } from "@/features/kits/queries";
import type { GlobalKitStateItemResponse } from "@/features/kits/types";

const stateBadge: Record<
  GlobalKitStateItemResponse["state"],
  { label: string; tone: "primary" | "warning" | "neutral" }
> = {
  deployed: { label: "deployed", tone: "primary" },
  drifted: { label: "drifted", tone: "warning" },
  undeployed: { label: "not deployed", tone: "neutral" },
};

export function KitGlobalDeploySection() {
  const queryClient = useQueryClient();
  const { data: state, isPending, error } = useQuery(globalStateQueryOptions());
  const [pending, setPending] = useState<string | null>(null);
  const [deployError, setDeployError] = useState<Error | null>(null);
  const [removing, setRemoving] = useState<GlobalKitStateItemResponse | null>(null);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: globalStateQueryOptions().queryKey });
    void queryClient.invalidateQueries({ queryKey: kitKeys.lists() });
  };

  const deploy = useMutation({
    mutationFn: (pair: { kitId: string; agent: string }) => deployKitGlobal(pair.kitId, pair.agent, false),
    onSuccess: invalidate,
    onError: (err) => setDeployError(err as Error),
  });

  const remove = useMutation({
    mutationFn: (pair: { kitId: string; agent: string }) => removeKitGlobal(pair.kitId, pair.agent),
    onSuccess: invalidate,
    onError: (err) => setDeployError(err as Error),
  });

  const run = (pair: { kitId: string; agent: string }) => {
    setDeployError(null);
    setPending(`${pair.kitId}:${pair.agent}`);
    remove.mutate(pair, {
      onSettled: () => {
        setPending(null);
        setRemoving(null);
      },
    });
  };

  const groups = groupByKit(state ?? []);

  return (
    <section className="space-y-2 rounded-lg border p-4">
      <div className="flex items-center gap-2">
        <Globe className="size-4" />
        <h3 className="font-medium">Global deploy</h3>
      </div>
      <p className="text-sm text-muted-foreground">
        The mandatory kit materializes into global skills of every detected agent — no per-project activation, no
        opt-out. Prose-only kits weave into agent entry files between OVERMIND markers; hand edits between those
        markers are overwritten on the next deploy. If install.sh ran recently, marker blocks may show as drifted —
        redeploy restores them. Rows appear for every detected agent home — the home directory alone is enough, so
        a runtime you no longer use still lists here; leaving it undeployed is fine.
      </p>
      {isPending && <LoadingState label="Loading global state…" />}
      {error && <ErrorText error={error} />}
      {deployError && <ErrorText error={deployError} />}
      {groups.map(([kitId, rows]) => {
        const first = rows[0];
        return (
          <div key={kitId} className="space-y-1 rounded border px-3 py-2">
            <div className="flex items-center gap-2">
              <span className="font-medium">{first.kitName}</span>
              {first.mandatory && (
                <Badge tone="marshal" className="gap-1">
                  <ShieldAlert className="size-3" /> mandatory — global skills, no opt-out
                </Badge>
              )}
            </div>
            {rows.map((row) => {
              const badge = stateBadge[row.state];
              const rowPending = pending === `${row.kitId}:${row.agent}`;
              const handEdited = row.drifts.some((d) => d.kind === "modified");
              const canRemove = !first.mandatory && row.state !== "undeployed";
              return (
                <div key={row.agent} className="flex items-center gap-2 text-sm">
                  <span className="w-24">{row.agent}</span>
                  <Badge tone={badge.tone}>{badge.label}</Badge>
                  {row.state === "drifted" && (
                    <span className="text-muted-foreground">
                      {row.drifts.map((d) => d.kind).join(", ")}
                      {handEdited
                        ? " — hand-edited file, remove then deploy to reset"
                        : " — redeploy restores"}
                    </span>
                  )}
                  <div className="ml-auto flex gap-2">
                    <Button
                      size="sm"
                      variant="secondary"
                      disabled={rowPending}
                      onClick={() => {
                        setDeployError(null);
                        setPending(`${row.kitId}:${row.agent}`);
                        deploy.mutate(
                          { kitId: row.kitId, agent: row.agent },
                          { onSettled: () => setPending(null) },
                        );
                      }}
                    >
                      {rowPending ? "…" : row.state === "undeployed" ? "Deploy" : "Redeploy"}
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      disabled={rowPending || !canRemove}
                      onClick={() => setRemoving(row)}
                    >
                      Remove
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        );
      })}

      <Modal
        title={removing ? `Remove from ${removing.agent}?` : ""}
        open={removing !== null}
        onClose={() => setRemoving(null)}
      >
        {removing ? (
          <div className="space-y-4">
            <p className="text-sm text-foreground">
              Remove kit &quot;{removing.kitName}&quot; from the {removing.agent} home? Only files this door
              deployed are deleted; marker blocks are cut from the entry file; everything else stays untouched.
            </p>
            <div className="flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setRemoving(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                disabled={pending === `${removing.kitId}:${removing.agent}`}
                onClick={() => run({ kitId: removing.kitId, agent: removing.agent })}
              >
                {pending ? "Removing…" : "Remove"}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </section>
  );
}

function groupByKit(items: GlobalKitStateItemResponse[]): [string, GlobalKitStateItemResponse[]][] {
  const map = new Map<string, GlobalKitStateItemResponse[]>();
  for (const item of items) {
    const list = map.get(item.kitId) ?? [];
    list.push(item);
    map.set(item.kitId, list);
  }
  return [...map.entries()];
}

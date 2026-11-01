import type { ReactNode } from "react";
import { PageHintList } from "@/shared/ui/page-hint-list";

interface WikiSectionProps {
  id: string;
  title: string;
  hintPath?: string;
  children: ReactNode;
}

function WikiSection({ id, title, hintPath, children }: WikiSectionProps) {
  return (
    <section id={id} className="scroll-mt-20 rounded-lg border border-border bg-surface p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-semibold text-foreground">{title}</h2>
        {hintPath ? <PageHintList path={hintPath} /> : null}
      </div>
      <div className="mt-3 space-y-3 text-sm leading-relaxed text-muted">{children}</div>
    </section>
  );
}

function Token({ children }: { children: ReactNode }) {
  return (
    <code className="rounded bg-surface-raised px-1 py-0.5 font-mono text-xs text-foreground">
      {children}
    </code>
  );
}

export function WikiContent() {
  return (
    <div className="space-y-4">
      <WikiSection id="projects-and-numbering" title="Projects & numbering">
        <p>
          A project is a workspace folder registered with Overmind — entries, skill kits and the
          planning board all hang off it. Whatever the materializer writes to disk
          (<Token>BACKLOG/</Token> and <Token>ENTRIES/</Token>) is a read-only mirror: the database
          is the single source of truth, and a status read from a file is stale by definition.
        </p>
        <p>
          Feature slugs carry an NNN number prefix that is unique forever. Numbers compare
          numerically (7 ≡ 007), a taken number is refused with <Token>NumberCollision</Token>, and
          a retired number is never reused — not even after the feature is deleted from the board.
        </p>
      </WikiSection>

      <WikiSection id="planning-ceremony" title="Planning ceremony" hintPath="/planning">
        <p>
          Work runs as a ceremony, not a chat thread. An idea matures into a board feature, then
          walks spec → human validation → plan → human validation → task breakdown → execution →
          review → result artifact → done. Every status move carries a log note, so the board
          doubles as the audit trail.
        </p>
        <p>
          Validated specs and plans are frozen: agents may only append dated log lines or patch a
          single section with a change note. The human door (this UI) and the agent door (MCP) hit
          the same aggregate, but validation approvals always belong to the human.
        </p>
        <p>
          Dependencies between tasks are explicit <Token>blockedBy</Token> edges on the artifact —
          never prose references. An edge closes when the blocker task reaches done or review, and
          blocked tasks are skipped when the next actionable task is picked.
        </p>
      </WikiSection>

      <WikiSection id="lanes-and-claims" title="Lanes & claims" hintPath="/lanes">
        <p>
          A lane is a named code contour owned by one agent at a time. Claiming a feature inside a
          lane held by someone else is refused; unclaimed open features never block.
        </p>
        <p>
          A claim is a hard mutation lock backed by session liveness. An alive claim can only
          receive directives — it is never stolen mid-flight; a stale or ended claim can be taken
          over by the next session.
        </p>
        <p>
          Features declare their <Token>codeSurfaces</Token> (repo file globs), so coordinators see
          potential overlaps between in-flight features before they turn into merge conflicts.
        </p>
      </WikiSection>

      <WikiSection id="wave-protocol" title="Wave protocol" hintPath="/wave-chat">
        <p>
          Wave chat is signal-only: blockers, handoffs, critical spots, broadcasts. No chatter —
          and a message that @mentions you is one you are expected to read.
        </p>
        <p>
          Workers execute their features; the wave marshal reads the whole board across all lanes
          and issues move or wait decisions for the wave.
        </p>
        <p>
          Directives (<Token>HOLD</Token> / <Token>SYNC</Token> / <Token>YIELD</Token>) ride an
          append-only channel that crosses any claim lock. The holder sees open directives on the
          next read and acknowledges them; a HOLD means sync before your next push.
        </p>
      </WikiSection>

      <WikiSection id="attention" title="Attention">
        <p>
          Attention is the needs-a-human lane: replies to agent questions, validation and approval
          requests, and cross-project notifications land in the notification center behind the
          bell.
        </p>
        <p>
          The same signal surfaces as the bell badge, the browser tab title and desktop
          notifications; toasts, chirp and permissions are tuned in Settings.
        </p>
      </WikiSection>

      <WikiSection id="tombstones-and-board-states" title="Tombstones & board states">
        <p>
          Nothing is silently deleted. A requirement removed from a document is marked{" "}
          <Token>&lt;removed date — reason&gt;</Token>, a deleted entry becomes a tombstone entry
          that stays searchable, and planning artifacts are soft-deleted with an audit line in the
          feature log.
        </p>
        <p>
          The board vocabulary: features move through{" "}
          <Token>draft | ready | inProgress | review | done</Token>, documents (spec, plan, result)
          through <Token>draft | validated</Token>, and tasks through{" "}
          <Token>todo | inProgress | review | done | blocked</Token>.
        </p>
      </WikiSection>

      <WikiSection id="system-health" title="System health">
        <p>
          The status chip in the topbar opens the health popup: Qdrant vector store (up/down and
          endpoint), the embedding provider (provider · model · dimensions) and the app version.
        </p>
        <p>
          Degraded is honest, not fatal: when the vector store is unreachable, semantic search is
          disabled and a banner says so, while full-text search keeps working. The degraded popup
          links to Settings where the fix lives.
        </p>
      </WikiSection>

      <WikiSection id="cli-and-mcp-pointers" title="CLI & MCP pointers">
        <p>
          Agents drive the board over MCP: the <Token>overmind</Token> adapter proxies to the local
          host and carries the <Token>planning_*</Token> tool family plus entries, wave chat and
          issues tools. <Token>overmind_onboard</Token> is the first call of any session.
        </p>
        <p>
          The Host ships a bulk importer CLI (<Token>--import-backlog &lt;repoRoot|BACKLOG&gt;</Token>)
          and a planning CLI door as a fallback transport when MCP is unavailable.
        </p>
        <p>
          Humans use this web UI. Both doors hit the same aggregate, and the board — not any file
          on disk — is the only live state copy.
        </p>
      </WikiSection>
    </div>
  );
}

"use client";

import { useCallback, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { PageBackLink } from "@/shared/ui/page-back-link";
import { PromoteNote } from "@/features/journal/components/promote-note";
import { BacklinksRail, RailDayRow } from "@/features/journal/components/backlinks-rail";
import { NoteEditor } from "@/features/journal/components/note-editor";
import { OutgoingRail } from "@/features/journal/components/outgoing-rail";
import { RelatedRail } from "@/features/journal/components/related-rail";
import { TagEditor } from "@/features/journal/components/tag-editor";
import { UnresolvedList } from "@/features/journal/components/unresolved-list";
import {
  addTag,
  draftFromNote,
  isDraftDirty,
  removeTag,
  saveBlocker,
  toFormValues,
  UNNAMED_NOTE,
  type NoteDraft,
} from "@/features/journal/lib/note-form";
import {
  authorLine,
  backLinkLabelFrom,
  linkedEntryIds,
  noteHref,
  relatedQuery,
  streamHrefFrom,
} from "@/features/journal/lib/note-rails";
import { useLinkCandidates, useRelatedNotes } from "@/features/journal/lib/use-note-links";
import {
  useJournalContainer,
  useJournalMutations,
  useJournalNote,
  useTagColorMap,
} from "@/features/journal/lib/use-journal";
import { formatJournalStamp } from "@/features/journal/lib/stream-filters";
import { journalKeys } from "@/features/journal/queries";
import type { JournalNote } from "@/features/journal/types";

interface NoteViewProps {
  noteId: string;
  search: string;
}

// One string for both platforms. Sniffing `navigator` here would print "Ctrl+↵" into the static
// export and "⌘↵" on the owner's Mac at hydration — the capture overlay can afford the sniff
// because a modal renders nothing until it is opened.
const SAVE_HINT = "Ctrl / ⌘ + Enter saves";

export function NoteView({ noteId, search }: NoteViewProps) {
  const note = useJournalNote(noteId);
  const loaded = note.data;

  if (note.isPending) {
    return <NoteViewSkeleton />;
  }
  if (note.error !== null || loaded === undefined) {
    return (
      <div className="space-y-3">
        <PageBackLink href={streamHrefFrom(search)} label={backLinkLabelFrom(search)} />
        <ErrorText error={note.error ?? new Error("The note is not there any more.")} />
        <Button variant="secondary" size="sm" onClick={() => void note.refetch()}>
          Retry
        </Button>
      </div>
    );
  }

  // Keyed by the note, not by the route: opening another note remounts the surface and its
  // draft starts from the new payload, which is the whole reason no effect has to sync loaded
  // data into state here (and this lint rule set rejects that sync — react-hooks/
  // set-state-in-effect).
  return <NoteSurface key={loaded.id} note={loaded} search={search} />;
}

function NoteSurface({ note, search }: { note: JournalNote; search: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { containerId } = useJournalContainer();
  const tagColors = useTagColorMap();
  const mutations = useJournalMutations(containerId);

  const [draft, setDraft] = useState<NoteDraft>(() => draftFromNote(note));
  const [saved, setSaved] = useState(false);
  const [relatedNeedle, setRelatedNeedle] = useState("");
  const [creatingTarget, setCreatingTarget] = useState<string | null>(null);

  const candidates = useLinkCandidates(containerId);
  const linked = useMemo(
    () => linkedEntryIds(note.incoming, note.outgoing),
    [note.incoming, note.outgoing],
  );
  const related = useRelatedNotes(relatedNeedle, note.id, linked);

  const invalidateRails = useCallback(() => {
    // Cross-note staleness, the case task 5's layer cannot see: a save here rewrites the
    // rails of *other* notes — the target that just gained a backlink, the source that lost
    // one, and any note whose unresolved marker became a real edge. Precise targeting does not
    // close: the notes newly linked by this very edit are exactly the ones the pre-save payload
    // could not name, and naming them costs a round trip. So the whole detail shelf goes
    // stale, which is cheap by construction — invalidation refetches only what is mounted and
    // marks the rest to reload on open. No setQueryData anywhere: a synthesised rail is how a
    // stale rail comes back looking fresh.
    queryClient.invalidateQueries({ queryKey: journalKeys.detailsRoot() });
  }, [queryClient]);

  const save = useCallback(() => {
    const values = toFormValues(draft);
    if (values === null) {
      return;
    }
    mutations.update.mutate(
      { id: note.id, values },
      {
        onSuccess: (savedNote) => {
          // The Host owns the stored spelling — an empty title comes back carrying the first
          // line it derived — so the draft takes the answer rather than its own input, and the
          // editor does not go on claiming unsaved work after a clean save.
          setDraft(draftFromNote(savedNote));
          setSaved(true);
          invalidateRails();
        },
      },
    );
  }, [draft, invalidateRails, mutations, note.id]);

  const askRelated = () => setRelatedNeedle(relatedQuery(note.title, note.content));

  const createFromDeadEnd = (target: string, title: string) => {
    setCreatingTarget(target);
    mutations.create.mutate(
      {
        title,
        // The body must not be empty — `CreateEntryRequestValidator` pins Content NotEmpty —
        // and it must not invent an edge either: the scaffold says where the note came from in
        // prose, so the new note starts clean and the dead end in the source resolves the
        // moment the titles match.
        content: "Created from a dead-end link. Write the thought here.",
        tags: [],
      },
      {
        onSuccess: (created) => {
          invalidateRails();
          router.push(noteHref(created.id));
        },
        onSettled: () => setCreatingTarget(null),
      },
    );
  };

  const navigate = useCallback((href: string) => router.push(href), [router]);

  const author = authorLine(note.authorAlias);
  const blocker = saveBlocker(draft);
  const dirty = isDraftDirty(draft, draftFromNote(note));
  const saving = mutations.update.isPending;

  return (
    // The topbar is `sticky top-0 h-14` (app-topbar.tsx:43), so a control scrolled to the top
    // of the viewport lands under it. scroll-margin is per element, so the note view sets it
    // for every focusable in its subtree from the root instead of repeating a class on each
    // primitive.
    <div className="space-y-3 [&_a]:scroll-mt-16 [&_button]:scroll-mt-16 [&_input]:scroll-mt-16 [&_textarea]:scroll-mt-16">
      <PageBackLink href={streamHrefFrom(search)} label={backLinkLabelFrom(search)} />

      <Input
        aria-label="Note title"
        value={draft.title}
        onChange={(event) => {
          setDraft({ ...draft, title: event.target.value });
          setSaved(false);
        }}
        placeholder={UNNAMED_NOTE}
        className="min-w-0 font-semibold"
        // The primitive carries `text-sm` in its own class list and web's cn() concatenates,
        // so a `text-2xl` class here is a coin toss decided by stylesheet order — the capture
        // field hit the same wall and solved it inline. Fallback literal for the case where the
        // theme does not emit that step at all.
        style={{ fontSize: "var(--text-2xl, 1.5rem)" }}
      />

      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
        <span className="font-mono">{formatJournalStamp(note.createdAt)}</span>
        <span aria-hidden>·</span>
        <span className="font-mono">edited {formatJournalStamp(note.updatedAt)}</span>
        <span aria-hidden>·</span>
        <RailDayRow updatedAt={note.updatedAt} onNavigate={navigate} />
        <span aria-hidden>·</span>
        <span>
          author:{" "}
          {author.agent ? (
            // The boundary ruling made visible: an agent write carries its alias, and the alias
            // renders in mono because it is an identifier, not a name to read.
            <span className="font-mono text-foreground">{author.label}</span>
          ) : (
            <span className="text-foreground">{author.label}</span>
          )}
        </span>
        <span aria-hidden>·</span>
        <span className="font-mono">v{note.currentVersion}</span>
        {note.state !== "active" ? (
          <>
            <span aria-hidden>·</span>
            <span className="text-warning">{note.state}</span>
          </>
        ) : null}
      </div>

      <div className="flex flex-wrap items-start gap-6">
        <div className="min-w-0 max-w-prose flex-1 space-y-3">
          <NoteEditor
            value={draft.content}
            onValueChange={(content) => {
              setDraft({ ...draft, content });
              setSaved(false);
            }}
            candidates={candidates.candidates}
            currentNoteId={note.id}
            onSaveShortcut={save}
          />
          <div className="flex flex-wrap items-center gap-2">
            <Button onClick={save} disabled={saving || blocker !== null || !dirty}>
              {saving ? "Saving…" : "Save"}
            </Button>
            <span className="text-xs text-muted">{SAVE_HINT}</span>
            {blocker !== null ? (
              <span className="text-xs text-danger">{blocker}</span>
            ) : dirty ? (
              <span className="text-xs text-warning">Unsaved changes</span>
            ) : null}
            <ErrorText error={mutations.update.error} />
          </div>
        </div>

        <aside
          aria-label="Note context"
          className="w-full shrink-0 space-y-4 rounded-lg border border-border bg-surface p-3 xl:w-80"
        >
          {/* Triage sits at the head of the rails, not the foot: it is the act that ends the note's
              life in the pile, and the provenance row it writes lands in the rails a few pixels
              below it — the act and its record in one sightline. The graph filters are not threaded
              in here because no graph field is mounted on this route, which is precisely the case
              `promotionInvalidations` widens to the graph family for. */}
          <PromoteNote entryId={note.id} noteTitle={note.title} />
          <BacklinksRail
            backlinks={note.incoming}
            askRelated={askRelated}
            askingRelated={relatedNeedle.length > 0 && related.loading}
            onNavigate={navigate}
          />
          <OutgoingRail links={note.outgoing} onNavigate={navigate} />
          <UnresolvedList
            targets={note.unresolved}
            creating={creatingTarget}
            onCreate={createFromDeadEnd}
          />
          <section aria-label="Tags">
            <h2 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted">
              Tags ({draft.tags.length})
            </h2>
            <TagEditor
              tags={draft.tags}
              colors={tagColors}
              onAdd={(raw) => {
                const edit = addTag(draft, raw);
                setDraft(edit.draft);
                setSaved(false);
                return edit.error;
              }}
              onRemove={(tag) => {
                setDraft(removeTag(draft, tag));
                setSaved(false);
              }}
            />
          </section>
          <RelatedRail
            rows={related.rows}
            asked={relatedNeedle.length > 0}
            loading={related.loading}
            failed={related.error !== null}
            onAsk={askRelated}
            onNavigate={navigate}
          />
        </aside>
      </div>

      {/* Outside any dialog and behind role="status": this surface mounts no modal of its own —
          the tag field and the link picker are inline — so there is no aria-modal subtree to
          prune it, and a save confirmation has to outlive the keystroke that triggered it. */}
      <div role="status" aria-live="polite" className="sr-only">
        {saved ? "Note saved. The links on this page were re-read." : ""}
      </div>
    </div>
  );
}

function NoteViewSkeleton() {
  return (
    <div className="space-y-4" aria-hidden>
      <div className="h-3 w-24 animate-pulse rounded bg-surface-raised" />
      <div className="h-7 w-2/3 animate-pulse rounded bg-surface-raised" />
      <div className="h-3 w-1/2 animate-pulse rounded bg-surface-raised" />
      <div className="flex flex-wrap items-start gap-6">
        <div className="min-w-0 max-w-prose flex-1 space-y-2">
          <div className="h-64 animate-pulse rounded-lg bg-surface-raised" />
          <div className="h-7 w-24 animate-pulse rounded bg-surface-raised" />
        </div>
        <div className="w-full shrink-0 space-y-2 xl:w-80">
          {[0, 1, 2].map((row) => (
            <div key={row} className="h-16 animate-pulse rounded bg-surface-raised" />
          ))}
        </div>
      </div>
    </div>
  );
}

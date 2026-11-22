"use client";

import {
  type KeyboardEvent as ReactKeyboardEvent,
  type MouseEvent as ReactMouseEvent,
  type PointerEvent as ReactPointerEvent,
  type WheelEvent as ReactWheelEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { Button } from "@/shared/ui/button";
import {
  clusteredNotice,
  computingAnchorsNotice,
  computingBloomNotice,
  computingLayoutNotice,
  GRAPH_CONTROL_LABELS,
  graphAriaLabel,
  precomputedNotice,
  reducedMotionMessage,
  settleFailedNotice,
  tierName,
  unsupportedRowsNotice,
} from "@/features/journal/lib/graph-copy";
import {
  anchorSetField,
  buildClusters,
  clusterSubField,
  composeClusterView,
  projectedLinks,
  UNSORTED_CLUSTER_ID,
  type ClusterModel,
} from "@/features/journal/lib/graph-cluster";
import {
  buildNeighbourhoods,
  layoutModeFor,
  pinNode,
  reheat,
  releasePinnedNodes,
  seedLayoutLinks,
  seedPositions,
  settleWithinBudget,
  PRECOMPUTE_CEILING,
  SIM_CEILING,
  startForceLayout,
  type LayoutHandle,
  type LayoutLink,
  type LayoutNode,
  type Neighbourhood,
} from "@/features/journal/lib/graph-layout";
import { precomputeLayout, preserveHeldNodes } from "@/features/journal/lib/graph-precompute";
import { anchorRadius, nodeRadius } from "@/features/journal/lib/graph-palette";
import { drawScene } from "@/features/journal/lib/graph-paint";
import { graphNodeStates, nodeKey } from "@/features/journal/lib/graph-payload";
import { useGraphTheme } from "@/features/journal/lib/use-graph";
import {
  fitView,
  nextNodeIndex,
  panBy,
  pickNode,
  scalePercent,
  toWorldX,
  toWorldY,
  zoomAt,
  type ViewTransform,
} from "@/features/journal/lib/graph-view";
import type { GraphField, GraphNode } from "@/features/journal/types";

interface GraphCanvasProps {
  field: GraphField;
  selectedKey: string | null;
  reducedMotion: boolean;
  onSelect: (key: string | null) => void;
  onOpenNote: (id: string) => void;
  onPin?: (position: { kind: GraphNode["kind"]; id: string; x: number; y: number }) => void;
  onUnpinAll?: () => void;
}

interface CanvasSize {
  width: number;
  height: number;
}

const DRAG_THRESHOLD = 3;
// A restart caused by a new payload nudge-settles rather than re-scatters; see the frame clock.
const REHEAT_ALPHA = 0.35;
const DRAG_REHEAT = 0.2;
const RELEASE_REHEAT = 0.6;
// How long the tier-2 kick waits for the field to stop changing. An edge drag re-fires the
// ResizeObserver nearly every frame, and settling on each rounded-pixel change would kick a
// settle per frame; the seed sketch on screen the whole time is why the wait is honest.
const SETTLE_KICK_DELAY_MS = 200;

// The app's first client-only heavy island (R3). Everything that can be measured without a
// browser lives in `lib/graph-*.ts`; what is left here is element lifecycle, pointer plumbing
// and the frame clock. The route shell stays a server component, and the static export keeps
// this code out of every other route's bundle.
export function GraphCanvas({
  field,
  selectedKey,
  reducedMotion,
  onSelect,
  onOpenNote,
  onPin,
  onUnpinAll,
}: GraphCanvasProps) {
  const wrapRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const nodesRef = useRef<LayoutNode[]>([]);
  const linksRef = useRef<LayoutLink[]>([]);
  const neighbourhoodsRef = useRef<Map<string, Neighbourhood>>(new Map());
  // Tier 3's own ledger: the grouping derived from the payload, which clusters the owner has
  // open (view-local by contract — a reload opens the field collapsed), the settled anchor
  // frame, and the collapsed-anchor map the painter and the pick share.
  const clustersRef = useRef<ClusterModel | null>(null);
  const expandedRef = useRef<Set<string>>(new Set());
  const anchorPosRef = useRef<Map<string, { x: number; y: number }>>(new Map());
  const hiddenRef = useRef<Map<string, number>>(new Map());
  // The anchor key-set whose frame actually LANDED — not the one a kick was scheduled for. A
  // kick is worth scheduling only for a set that has not landed yet, so a payload beat that
  // abandons an in-flight settle re-kicks it instead of stranding the field on the seed spiral
  // (review-271-03 MAJOR-1b, the tier-2 kick's lesson in its clustered shape).
  const settledAnchorSetRef = useRef("");
  const settlesRef = useRef(0);
  // The expansion settle's generation: anything that changes the visible composition — a
  // collapse, another expansion, a payload beat — bumps it and the slices stop at the next
  // boundary, the same contract the tier-2 kick's abandon token runs on.
  const expansionGenRef = useRef(0);
  // The bloom is launched from an event handler, so it has no effect cleanup to die with the
  // canvas — this flag is its unmount guard, symmetric with the kick's `live`.
  const mountedRef = useRef(true);
  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);
  const frameRef = useRef<number | null>(null);
  const simRef = useRef<LayoutHandle | null>(null);
  const dragRef = useRef<{ key: string | null; panning: boolean; startX: number; startY: number; moved: boolean }>(
    { key: null, panning: false, startX: 0, startY: 0, moved: false },
  );
  // Pan and zoom are properties of the drawing surface, not of React: the transform lives in a
  // ref, every interaction ends in one `draw()` call, and the readout is written off the same
  // number that painted the frame, so what the toolbar shows cannot disagree with the canvas.
  const viewRef = useRef<ViewTransform | null>(null);
  const scaleRef = useRef<HTMLOutputElement | null>(null);

  const [size, setSize] = useState<CanvasSize>({ width: 0, height: 0 });
  const [hoverKey, setHoverKey] = useState<string | null>(null);
  const [precomputing, setPrecomputing] = useState(false);
  const [settleFailed, setSettleFailed] = useState(false);
  const [layoutCount, setLayoutCount] = useState(0);
  const [layoutWhat, setLayoutWhat] = useState<"anchors" | "bloom">("anchors");
  const theme = useGraphTheme();

  // The ladder rung this field stands on. Tier 2's settle and tier 3's anchor frame hang off
  // it; tier 1 keeps the frame clock below untouched.
  const tier = layoutModeFor(field.nodes.length);
  const states = useMemo(() => graphNodeStates(field.nodes, field.edges), [field]);
  const clusters = useMemo(() => (tier === "clustered" ? buildClusters(field) : null), [field, tier]);

  useEffect(() => {
    const wrap = wrapRef.current;
    if (wrap === null || typeof ResizeObserver === "undefined") {
      return;
    }
    const observer = new ResizeObserver((entries) => {
      const box = entries[0]?.contentRect;
      if (box !== undefined) {
        setSize({ width: Math.round(box.width), height: Math.round(box.height) });
      }
    });
    observer.observe(wrap);
    return () => observer.disconnect();
  }, []);

  // Fitted on the first frame that has something to fit; an explicit "Fit field" resets it.
  const currentView = useCallback((): ViewTransform => {
    if (viewRef.current === null) {
      viewRef.current = fitView(nodesRef.current, size.width, size.height);
    }
    return viewRef.current;
  }, [size]);

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const colors = theme.palette.colors;
    if (
      canvas === null ||
      theme.pending ||
      size.width === 0 ||
      size.height === 0
    ) {
      return;
    }
    const context = canvas.getContext("2d");
    if (context === null) {
      return;
    }

    // The buffer is sized in device pixels and the transform scales the scene back to CSS
    // pixels; leaving the buffer at CSS size is how a canvas turns blurry on a hidpi panel.
    const dpr = window.devicePixelRatio > 1 ? window.devicePixelRatio : 1;
    const bufferWidth = Math.round(size.width * dpr);
    const bufferHeight = Math.round(size.height * dpr);
    if (canvas.width !== bufferWidth || canvas.height !== bufferHeight) {
      canvas.width = bufferWidth;
      canvas.height = bufferHeight;
    }

    const view = currentView();

    context.setTransform(dpr, 0, 0, dpr, 0, 0);
    drawScene(context, {
      nodes: nodesRef.current,
      links: linksRef.current,
      view,
      colors,
      states,
      hoverKey,
      selectedKey,
      neighbours: hoverKey === null ? null : neighbourhoodsRef.current.get(hoverKey)?.keys ?? null,
      width: size.width,
      height: size.height,
      font: `12px ${theme.font}`,
      tier,
      anchors: tier === "clustered" ? hiddenRef.current : undefined,
    });

    if (scaleRef.current !== null) {
      scaleRef.current.textContent = scalePercent(view.scale);
    }
  }, [currentView, hoverKey, selectedKey, size, states, theme, tier]);

  // The tier-2 settle lands from outside the render cycle, so it redraws through this ref
  // instead of listing draw in its effect's dependencies: draw changes identity on every hover,
  // and a hover must never restart a settle that takes hundreds of milliseconds of slices.
  const drawRef = useRef<() => void>(() => {});
  useEffect(() => {
    drawRef.current = draw;
  }, [draw]);

  // Tier 3 recomposes the visible field from the grouping plus whatever the screen already
  // holds: anchors at their settled coordinates, open clusters' members around the anchor they
  // bloomed from. Every flow — a payload beat, the anchor settle landing, an expansion, a
  // collapse — funnels through this one door, which is what keeps the two settle transports
  // from disagreeing about what is on the field.
  const recomposeClusterField = useCallback(() => {
    const model = clustersRef.current;
    if (model === null || size.width === 0 || size.height === 0) {
      return;
    }
    const standing = new Map(nodesRef.current.map((node) => [nodeKey(node.kind, node.id), node]));
    const composed = composeClusterView(
      field,
      model,
      expandedRef.current,
      anchorPosRef.current,
      standing,
      size.width,
      size.height,
    );
    nodesRef.current = composed.nodes;
    hiddenRef.current = composed.hidden;
    linksRef.current = seedLayoutLinks(projectedLinks(field, model, expandedRef.current), composed.nodes);
    neighbourhoodsRef.current = buildNeighbourhoods(linksRef.current);
  }, [field, size]);

  // Both tier-3 settles share one notice slot, so the slot counts the settles in flight rather
  // than flipping a boolean an overlapping settle would clear early. The kind rides along: a
  // notice that says "nodes" over a 51-anchor settle would read as the whole field.
  const beginSettle = useCallback((count: number, what: "anchors" | "bloom") => {
    settlesRef.current += 1;
    setLayoutCount(count);
    setLayoutWhat(what);
    setPrecomputing(true);
  }, []);

  const endSettle = useCallback(() => {
    settlesRef.current = Math.max(0, settlesRef.current - 1);
    setPrecomputing(settlesRef.current > 0);
  }, []);

  // Expand and collapse are one view-local Set. Flipping it recomposes immediately — the bloom
  // seeds around its anchor the same frame — and only then does the sliced settle refine the
  // members' positions, reusing the tier-2 transport with its own generation: a collapse, a
  // second expansion or a payload beat bumps the generation, the slices stop at the next
  // boundary, and the seeded sketch the flip already painted stays a usable frame.
  const toggleCluster = (groupKey: string) => {
    const model = clustersRef.current;
    if (model === null) {
      return;
    }
    const group = model.groups.find((candidate) => candidate.key === groupKey);
    if (group === undefined) {
      return;
    }
    const next = new Set(expandedRef.current);
    if (next.has(groupKey)) {
      next.delete(groupKey);
      expandedRef.current = next;
      expansionGenRef.current += 1;
      recomposeClusterField();
      draw();
      return;
    }
    next.add(groupKey);
    expandedRef.current = next;
    recomposeClusterField();
    draw();

    const gen = expansionGenRef.current + 1;
    expansionGenRef.current = gen;
    const anchorCoord =
      anchorPosRef.current.get(group.anchorKey) ?? { x: size.width / 2, y: size.height / 2 };
    // The bloom's forces centre on the anchor it grew out of — an edge-of-screen cluster must
    // not migrate toward the middle of the canvas just because the settle's centring has to
    // point somewhere.
    const forces = { width: size.width, height: size.height, centreX: anchorCoord.x, centreY: anchorCoord.y };
    const abandoned = () => !mountedRef.current || expansionGenRef.current !== gen;
    beginSettle(group.members.length, "bloom");
    void precomputeLayout(clusterSubField(field, group), forces, abandoned, anchorCoord)
      // The decrement leads every exit: a settle that was abandoned mid-flight is still a
      // settle that finished as far as the notice slot is concerned — one that returns before
      // decrementing leaves the slot held open forever (review-271-03 MAJOR-1a).
      .then((landed) => {
        endSettle();
        if (abandoned()) {
          return;
        }
        const landedCoords = new Map(landed.map((node) => [nodeKey(node.kind, node.id), node]));
        nodesRef.current = nodesRef.current.map((node) => {
          const settled = landedCoords.get(nodeKey(node.kind, node.id));
          if (settled === undefined || node.fx != null) {
            return node;
          }
          return { ...node, x: settled.x, y: settled.y };
        });
        recomposeClusterField();
        drawRef.current();
      })
      .catch(() => {
        endSettle();
        if (abandoned()) {
          return;
        }
        setSettleFailed(true);
      });
  };

  // The anchor door. A collapsed cluster dot opens its cluster and an open one closes it — the
  // anchor is the cluster's handle, not a row of its own: the project node inside is reachable
  // as a member once the cluster is open, and the list below reaches it at any time. A project
  // dot hiding nothing is not a door, so it selects like any other dot.
  const clusterDoor = (key: string | null): string | null => {
    const model = clustersRef.current;
    if (key === null || model === null) {
      return null;
    }
    const group = model.groups.find((candidate) => candidate.anchorKey === key);
    if (group === undefined || (group.hiddenCount === 0 && !expandedRef.current.has(group.key))) {
      return null;
    }
    return group.key;
  };

  // Rebuilding the field keeps whatever coordinates the owner already sees, so a refetch on the
  // live-sync beat does not re-scatter the map under their cursor. Server pins still win.
  useEffect(() => {
    if (size.width === 0 || size.height === 0) {
      return;
    }

    if (clusters !== null) {
      clustersRef.current = clusters;
      recomposeClusterField();
      draw();
      return;
    }

    const previous = new Map<string, LayoutNode>();
    for (const node of nodesRef.current) {
      previous.set(nodeKey(node.kind, node.id), node);
    }

    const seeded = seedPositions(field.nodes, field.positions, size.width, size.height);
    const pinned = new Set(field.positions.map((position) => nodeKey(position.kind, position.id)));
    for (const node of seeded) {
      const before = previous.get(nodeKey(node.kind, node.id));
      // `fx == null` keeps a just-unpinned node at the spot its simulation left it, so "unpin
      // returns the node to the simulation" means it drifts from where it stands, not that it
      // teleports back to the seed spiral. A server pin wins below and reseeds it deliberately.
      if (before !== undefined && !pinned.has(nodeKey(node.kind, node.id)) && before.fx == null) {
        node.x = before.x;
        node.y = before.y;
      }
    }

    nodesRef.current = seeded;
    linksRef.current = seedLayoutLinks(field.edges, seeded);
    neighbourhoodsRef.current = buildNeighbourhoods(linksRef.current);

    draw();
  }, [clusters, draw, field, recomposeClusterField, size]);

  // The anchor frame settles once per anchor-set that has not landed yet, not per payload beat:
  // a new note regroups nothing, so the settled coordinates stand while the field around them
  // moves. This kick is its own effect on purpose — draw changes identity on every hover, and a
  // kick hosted in an effect that hover restarts would abandon its own settle and never run
  // again (review-271-03 MAJOR-1b); the landed draw goes through the ref, the way tier 2's
  // kick already does. The guard reads the last LANDED set, so a payload beat that aborts an
  // in-flight settle re-kicks it instead of stranding the seed sketch.
  useEffect(() => {
    if (clusters === null || size.width === 0 || size.height === 0) {
      return;
    }

    const anchorSet = clusters.groups.map((group) => group.anchorKey).join("|");
    if (anchorSet === settledAnchorSetRef.current) {
      return;
    }

    const forces = { width: size.width, height: size.height };
    let live = true;
    const kick = setTimeout(() => {
      if (!live) {
        return;
      }
      beginSettle(clusters.groups.length, "anchors");
      void precomputeLayout(anchorSetField(field, clusters), forces, () => !live)
        .then((landed) => {
          endSettle();
          if (!live) {
            return;
          }
          for (const node of landed) {
            anchorPosRef.current.set(nodeKey(node.kind, node.id), { x: node.x, y: node.y });
          }
          settledAnchorSetRef.current = anchorSet;
          recomposeClusterField();
          drawRef.current();
        })
        .catch(() => {
          endSettle();
          if (!live) {
            return;
          }
          setSettleFailed(true);
        });
    }, SETTLE_KICK_DELAY_MS);

    return () => {
      live = false;
      clearTimeout(kick);
    };
  }, [beginSettle, clusters, endSettle, field, recomposeClusterField, size]);

  // The tier-1 frame clock, in its two shapes: a single synchronous settle under reduced motion,
  // and one tick per frame otherwise. Each one stops itself. The ladder's other rungs live
  // elsewhere on purpose — tier 2's settle is the effect below it, and past the precompute
  // ceiling the seed frame the rebuild effect painted is the whole show.
  //
  // The field object restarts this on purpose: the live-sync refetch hands over a new identity on
  // a beat, and a fresh layout is what makes the new rows settle. REHEAT_ALPHA is why that settle
  // nudges the field instead of shaking it under the owner's cursor.
  useEffect(() => {
    if (size.width === 0 || tier !== "simulate") {
      return;
    }

    const forces = { width: size.width, height: size.height };

    if (reducedMotion) {
      settleWithinBudget(nodesRef.current, linksRef.current, forces);
      draw();
      return;
    }

    const handle = startForceLayout(nodesRef.current, linksRef.current, forces);
    reheat(handle.simulation, REHEAT_ALPHA);
    simRef.current = handle;

    const step = () => {
      handle.simulation.tick();
      draw();
      if (handle.simulation.alpha() > handle.simulation.alphaMin()) {
        frameRef.current = requestAnimationFrame(step);
      } else {
        frameRef.current = null;
      }
    };
    frameRef.current = requestAnimationFrame(step);

    return () => {
      handle.stop();
      if (simRef.current === handle) {
        simRef.current = null;
      }
      if (frameRef.current !== null) {
        cancelAnimationFrame(frameRef.current);
        frameRef.current = null;
      }
    };
  }, [draw, field, reducedMotion, size, tier]);

  // Tier 2 has no clock: the seed frame is already on the canvas, this kicks the one-shot settle
  // for the field's identity and swaps the landed map in whenever the slices finish. Nothing
  // animates in between — every other frame is an event-driven redraw. The kick deliberately
  // reads draw through the ref instead of listing it here: draw changes identity on hover, and
  // restarting a multi-hundred-millisecond settle because the owner moved the cursor across a
  // dot would be a self-inflicted freeze. A drag that started on the seed survives the swap via
  // preserveHeldNodes.
  //
  // The kick waits out the noise first: an edge drag re-fires the size state nearly every frame,
  // and each size event used to kick a settle whose first slice runs synchronously — review-271-02
  // measured that shape as dozens of concurrent settles behind a two-second drag. The seed sketch
  // is what the owner is looking at during the drag anyway, so the settle runs once, a beat after
  // the field stops changing. A superseded kick dies in its timer; an in-flight settle stops at
  // its next slice boundary through the token handed to precomputeLayout, so a dropped identity
  // never finishes its ticks.
  useEffect(() => {
    if (size.width === 0 || size.height === 0 || tier !== "precomputed") {
      return;
    }

    const forces = { width: size.width, height: size.height };
    let live = true;
    const kick = setTimeout(() => {
      if (!live) {
        return;
      }
      setPrecomputing(true);
      setSettleFailed(false);
      void precomputeLayout(field, forces, () => !live)
        .then((landed) => {
          if (!live) {
            return;
          }
          preserveHeldNodes(landed, nodesRef.current);
          nodesRef.current = landed;
          linksRef.current = seedLayoutLinks(field.edges, landed);
          setPrecomputing(false);
          drawRef.current();
        })
        .catch(() => {
          if (live) {
            // The seed sketch stays on the canvas — a usable field beats a blank one — and the
            // notice below must not describe a swap that never happened.
            setPrecomputing(false);
            setSettleFailed(true);
          }
        });
    }, SETTLE_KICK_DELAY_MS);

    return () => {
      live = false;
      clearTimeout(kick);
    };
  }, [field, size, tier]);

  useEffect(() => {
    draw();
  }, [draw]);

  const pointFromEvent = (
    event: ReactPointerEvent<HTMLCanvasElement> | ReactMouseEvent<HTMLCanvasElement>,
  ) => {
    const rect = event.currentTarget.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const pickAt = useCallback(
    (screenX: number, screenY: number): string | null => {
      const view = currentView();
      return pickNode(
        nodesRef.current.map((node) => {
          const hidden = hiddenRef.current.get(nodeKey(node.kind, node.id));
          return {
            key: nodeKey(node.kind, node.id),
            x: node.x,
            y: node.y,
            radius: hidden === undefined ? nodeRadius(node) : anchorRadius(hidden),
          };
        }),
        toWorldX(screenX, view),
        toWorldY(screenY, view),
        view.scale,
      );
    },
    [currentView],
  );

  const nodeByKey = useCallback(
    (key: string | null): LayoutNode | null => {
      if (key === null) {
        return null;
      }
      return nodesRef.current.find((node) => nodeKey(node.kind, node.id) === key) ?? null;
    },
    [],
  );

  const onPointerDown = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    if (event.button !== 0) {
      return;
    }
    const { x, y } = pointFromEvent(event);
    const key = pickAt(x, y);
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = { key, panning: key === null, startX: x, startY: y, moved: false };
  };

  const onPointerMove = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const { x, y } = pointFromEvent(event);
    const drag = dragRef.current;

    if (drag.key !== null) {
      if (Math.hypot(x - drag.startX, y - drag.startY) > DRAG_THRESHOLD) {
        drag.moved = true;
      }
      if (drag.moved) {
        const node = nodeByKey(drag.key);
        if (node !== null) {
          // The live simulation reads the same datum, so the field reacts while dragging without
          // a second layout; the row is stored when the drag settles, in onPointerUp.
          pinNode(node, toWorldX(x, currentView()), toWorldY(y, currentView()));
          if (tier !== "simulate") {
            // A tier with no simulation has no tick to snap the datum onto its hold, so the
            // drag moves x/y itself — otherwise the dot would ignore the pointer until release.
            node.x = node.fx ?? node.x;
            node.y = node.fy ?? node.y;
          }
          if (simRef.current !== null) {
            reheat(simRef.current.simulation, DRAG_REHEAT);
          }
          draw();
        }
      }
      return;
    }

    if (drag.panning) {
      if (Math.hypot(x - drag.startX, y - drag.startY) > DRAG_THRESHOLD) {
        drag.moved = true;
        viewRef.current = panBy(currentView(), x - drag.startX, y - drag.startY);
        dragRef.current = { ...drag, startX: x, startY: y };
        draw();
      }
      return;
    }

    const key = pickAt(x, y);
    if (key !== hoverKey) {
      setHoverKey(key);
    }
  };

  const onPointerUp = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const { x, y } = pointFromEvent(event);
    const drag = dragRef.current;
    dragRef.current = { key: null, panning: false, startX: 0, startY: 0, moved: false };
    event.currentTarget.releasePointerCapture(event.pointerId);

    if (drag.moved) {
      if (drag.key !== null) {
        const node = nodeByKey(drag.key);
        if (node !== null) {
          // The unsorted anchor is this view's own device — no row on the Host stands behind
          // it, so a drag moves the dot locally and writes nothing.
          if (node.id !== UNSORTED_CLUSTER_ID) {
            onPin?.({ kind: node.kind, id: node.id, x: node.x, y: node.y });
          }
        }
      }
      return;
    }
    const door = tier === "clustered" ? clusterDoor(pickAt(x, y)) : null;
    if (door !== null) {
      toggleCluster(door);
      return;
    }
    onSelect(pickAt(x, y));
  };

  const onDoubleClick = (event: ReactMouseEvent<HTMLCanvasElement>) => {
    const { x, y } = pointFromEvent(event);
    const key = pickAt(x, y);
    const node = nodeByKey(key);
    if (node !== null && node.kind === "entry") {
      onOpenNote(node.id);
    }
  };

  const onWheel = (event: ReactWheelEvent<HTMLCanvasElement>) => {
    const { x, y } = pointFromEvent(event);
    const factor = event.deltaY < 0 ? 1.15 : 1 / 1.15;
    viewRef.current = zoomAt(currentView(), factor, x, y);
    draw();
  };

  const onKeyDown = (event: ReactKeyboardEvent<HTMLCanvasElement>) => {
    const step =
      event.key === "ArrowRight" || event.key === "ArrowDown"
        ? 1
        : event.key === "ArrowLeft" || event.key === "ArrowUp"
          ? -1
          : 0;

    if (step !== 0) {
      event.preventDefault();
      // The walk covers what is on the field, and in tier 3 that is the visible set — anchors
      // plus open clusters' members — not the payload's full node list.
      const visible = tier === "clustered" ? nodesRef.current : field.nodes;
      const keys = visible.map((node) => nodeKey(node.kind, node.id));
      const current = selectedKey === null ? null : keys.indexOf(selectedKey);
      const next = nextNodeIndex(current === -1 ? null : current, step, keys.length);
      if (next !== null) {
        onSelect(keys[next]);
      }
      return;
    }

    if (event.key === "Enter" && tier === "clustered") {
      const door = clusterDoor(selectedKey);
      if (door !== null) {
        event.preventDefault();
        toggleCluster(door);
      }
      return;
    }

    if (event.key === "Escape") {
      onSelect(null);
    }
  };

  const releasePins = () => {
    releasePinnedNodes(nodesRef.current);
    if (field.positions.length > 0) {
      onUnpinAll?.();
    }
    if (simRef.current !== null) {
      reheat(simRef.current.simulation, RELEASE_REHEAT);
    }
    draw();
  };

  if (!theme.pending && theme.palette.unresolved.length > 0) {
    // A token that no longer exists is a build problem, and the honest answer is a field that
    // says so — not dots painted in an invented colour and not a blank canvas that looks empty.
    return (
      <p className="text-sm text-warning" role="status">
        The graph palette could not resolve {theme.palette.unresolved.join(", ")} from the shipped
        theme, so the field is not drawn. The list below holds the same nodes.
      </p>
    );
  }

  const notice = unsupportedRowsNotice(field.unsupported.nodes, field.unsupported.edges);

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-2">
        <Button variant="secondary" size="sm" onClick={() => {
          viewRef.current = zoomAt(currentView(), 1.25, size.width / 2, size.height / 2);
          draw();
        }}>
          {GRAPH_CONTROL_LABELS.zoomIn}
        </Button>
        <Button variant="secondary" size="sm" onClick={() => {
          viewRef.current = zoomAt(currentView(), 1 / 1.25, size.width / 2, size.height / 2);
          draw();
        }}>
          {GRAPH_CONTROL_LABELS.zoomOut}
        </Button>
        <Button variant="secondary" size="sm" onClick={() => {
          viewRef.current = fitView(nodesRef.current, size.width, size.height);
          draw();
        }}>
          {GRAPH_CONTROL_LABELS.fit}
        </Button>
        <Button variant="ghost" size="sm" onClick={releasePins}>
          {GRAPH_CONTROL_LABELS.reset}
        </Button>
        <output ref={scaleRef} className="text-xs text-muted" aria-live="off">
          {scalePercent(1)}
        </output>
      </div>

      {tier === "clustered" ? (
        <p className="text-xs text-muted" role="status">
          {precomputing
            ? layoutWhat === "anchors"
              ? computingAnchorsNotice(layoutCount)
              : computingBloomNotice(layoutCount)
            : settleFailed
              ? settleFailedNotice(field.nodes.length)
              : clusteredNotice(field.nodes.length, PRECOMPUTE_CEILING)}
        </p>
      ) : null}

      {tier === "precomputed" ? (
        <p className="text-xs text-muted" role="status">
          {precomputing
            ? computingLayoutNotice(field.nodes.length)
            : settleFailed
              ? settleFailedNotice(field.nodes.length)
              : precomputedNotice(field.nodes.length, SIM_CEILING)}
        </p>
      ) : null}

      {reducedMotion && tier === "simulate" ? (
        <p className="text-xs text-muted" role="status">
          {reducedMotionMessage(field.nodes.length)}
        </p>
      ) : null}

      {notice.length > 0 ? (
        <p className="text-xs text-muted" role="status">
          {notice}
        </p>
      ) : null}

      {/* The frame has a height of its own because a canvas has no intrinsic one: the wrapper
          measures it and the buffer follows, so the island stays the size the route chose. */}
      <div ref={wrapRef} className="h-[520px] w-full overflow-hidden rounded-md border border-border bg-background">
        <canvas
          ref={canvasRef}
          role="img"
          tabIndex={0}
          aria-label={graphAriaLabel(
            field.counts,
            tierName(tier),
            clusters === null ? undefined : clusters.groups.length,
          )}
          className="block h-full w-full cursor-crosshair touch-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          onPointerDown={onPointerDown}
          onPointerMove={onPointerMove}
          onPointerUp={onPointerUp}
          onPointerLeave={() => setHoverKey(null)}
          onDoubleClick={onDoubleClick}
          onWheel={onWheel}
          onKeyDown={onKeyDown}
        />
      </div>

      <p className="text-xs text-muted">
        Click a node for its context, double-click a note to open it, drag to move and keep it,
        scroll to zoom. Arrow keys walk the field while the canvas has focus, Escape clears the
        selection. Pins are stored on the Host, so every browser and every reload opens the same
        field;{" "}
        {tier === "simulate"
          ? "the release control sends them back to the simulation everywhere."
          : "the release control clears them here and on the Host, and this tier leaves every node where it stands."}
        {tier === "clustered"
          ? " In clustered mode a project dot is its cluster's door: click or press Enter to open the group it hides and again to close it, and which groups are open lives only in this view — a reload starts with every cluster closed."
          : null}
      </p>
    </div>
  );
}

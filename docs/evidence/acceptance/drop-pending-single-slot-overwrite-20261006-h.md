# Acceptance record — A same-frame second drop overwrites the pending report of the first

- Ticket: `drop-pending-single-slot-overwrite` — verdict: **back to `todo/`** (status field
  `- Status: Todo — Rejected (…)`): the re-scoped row's machine half passes 2/2 — two departures register in
  ONE frame and BOTH commit — and its world half fails 2/2: only one of the two children ever gets a
  standing world object
- Batch: `20261006-h` — ticket `drop-pending-single-slot-overwrite` (scope below)
- Commit: `0e86bfd1` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+0e86bfd1d2638880443f6481574f6e568d742a48`
- Run: 2026-10-06 22:33 → 22:42 local · Host: physical machine (the operator) · Guest: sandbox (the owner) ·
  Third peer: the alternate sandbox
- Dependencies: the eleven the preflight reported present (`RESULT: OK - a full two-client run is possible;
  the alternate third client is configured`)
- Artifacts: `20261006-h/` in the directory named by `acceptance-artifacts-dir`: the fixture trees
  (`h1`–`h5`), the operator's list probes (`s1`–`s6`), the release probes (`r1`–`r3`), the host table reads
  (`w1`, `w2`), the log marks (`marks-session.txt`, `marks-g1`–`g3-before.txt`) and the two capacity probes
  (`p1`–`p3`)

## Batch scope

This run served one ticket. It is the only ticket in `review/` whose acceptance row was re-opened in this
cycle, and its setup — a three-client world in which a carried container holds an EMPTY nested container —
is the world this session could stage. The other 65 tickets in `review/` are not served by this run: 49 of
them carry no acceptance record at all and each needs its own world, save or fixture plan, and the
remaining 16 carry earlier records whose open rows were not re-planned here. Nothing in this run changes
their state.

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The gesture is reachable: a remote release of a carried container onto a NESTED container's panel button is captured as ONE `MoveContainerChildren` intent | machine | **pass** | `r3-host-release-repeat.json` — the cast resolves to the nested container (`castItem: "plasticbag"`, `castItemId: 35262760597`, `castItemProxy: true`, `castIsContainer: true`, `castOverlaps: true`), the dragged proxy carries `childCount: 2` with `canHold: true` on both children, `calls: "none"` (no exception inside the native release body) and `dragAfter: "none"`; the operator's window: `[RemoteIntent] MoveContainerChildren captured for item 18082891413 of 76561198863287957 (container 35262760597, slot -1, body 0, limb -1, target item 0, trader none).` 22:40:41.195 |
| 2 | R5's expansion onto a target that refuses every admitted child registers TWO departures out of the same frame and BOTH commit | machine | **pass (2/2)** | Owner window: `[ItemTrace] op=24 begin item=39557727893 origin=OnItemUnloadedFromContainer event=Unload` and `op=25 … item=43852695189 …`, both at 22:40:41.221, each with its `[ContainerUnload] … left its container into the world from the CarriedInventory side — the drop report waits one frame`, plus the applier's own account `container expansion of item duffelbag into container 35262760597: 0 of 2 direct child item(s) entered the container; 2 did not (the native guard, or a native load refusal).`; then `[ItemDropped] dogfood (id 43852695189) at (3.1,490.3)` → `[ItemTrace] op=25 … origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` and `[ItemDropped] dogfood (id 39557727893) …` → `op=24 … FlushPendingDrop result=Committed(1) events=[Drop, Flush]`, all at 22:40:41.247. The first run of the same gesture on the first fixture reads the same shape at 22:39:24.609/.650 (`op=17`/`op=18`) |
| 3 | BOTH children reach the host's world table | machine | **fail (2/2)** | `w2-host-tables-after-repeat.json` (the host's authoritative read after the gesture): the world table holds `43852695189` and **not** `39557727893`, which the same read lists in `terminal` (`terminalCount: 2`, `worldCount: 242`, `tableCount: 3`). Run 1 the same: `w1-host-tables.json` holds `22377858709` in the world and `26672826005` in `terminal` |
| 4 | BOTH children are found by the third peer | machine | **fail (2/2)** | Third peer window: `[ItemSpawn] materializing dogfood (id 43852695189) at (3.1,490.3)` 22:40:41.277, and for its sibling `[ItemDrop] dogfood (id 39557727893) not present — requesting materialization at (3.1,490.3), container 0, parentPos (0.0,0.0)` 22:40:41.282 → `[ItemBind] bound existing dogfood at (3.1, 490.3) to id 39557727893 (no materialization)` 22:40:41.285 → `[ItemTrace] op=54 item=39557727893 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]` 22:40:41.295 → `Kernel command rejected by host for item 39557727893: InvalidTransition` 22:40:41.334. Run 1 reproduced it on the same pair of ids (`26672826005`: `[ItemBind] bound existing dogfood …` 22:39:24.691, `OnItemDestroyed` 22:39:24.724, the kernel refusal 22:39:24.740) |
| 5 | The operator's and the third peer's clone-fact monitor stays at zero across the gesture and a quiet cycle | machine | **pass (2/2)** | Both gesture windows (`marks-g2-before.txt`, `marks-g3-before.txt`) filtered for `divergence` read `NO MATCHING LINES` on the operator and on the third peer. The warnings those windows do carry are the kernel's `InvalidTransition` refusals that rows 3/4's own destroy produced. The FIXTURE STAGING before the marks carries its own divergences (`a new carried item the fact table never saw — a pickup without an event sync` at 22:36:22 and 22:40:28), which is the known staging shape of `item-provide mode=create` + `container-fill`, not this gesture |

## What the run read about the row's premise

- **The gesture the ticket names is reachable, and the refusal route fires.** The operator's remote panel of
  the owner's `trashbag` showed the nested container as its own button; the release with the game's own
  `expanddesc` bind held (`heldAtEnd: true` before the invoke, `ringScaleForced` not needed) captured ONE
  `MoveContainerChildren` intent, and the owner's own applier account says `0 of 2 direct child item(s)
  entered the container; 2 did not` — every admitted child was refused by `Container.LoadItem` AFTER
  `CanHoldItem` admitted it, which is the route (b) shape (`this.mItem.TryGetParentContainer`) the ticket's
  producer section predicted.
- **The machine half is what passes, for the first time on the real shape.** The single-slot machine this
  ticket replaced could not have settled both: two entries registered inside one frame (22:40:41.221) each
  kept their own op and both committed on the next frame (22:40:41.247). This is the reading the ticket was
  re-scoped to obtain, and it stands 2/2.
- **The world half fails on the SECOND child, and it is not the pending machine.** Neither viewer created a
  second world object. On both, the second report found no local object for its id, asked for
  materialization, bound an id-less same-prefab world copy at the reported position instead
  (`RemoteItemSceneOps.FindExistingAt`, `AdoptTolerance` 1.5 units, which excludes any object already
  carrying `ItemInstanceId`) and then that copy was destroyed ~10–30 ms later (`OnItemDestroyed …
  events=[Destroyed]`), leaving the id `terminal` in the host's kernel and the guest's own `ItemDestroy`
  command refused as `InvalidTransition (terminal item … cannot be destroyed again)`. Net: one reported,
  committed departure per gesture produced no standing object, on the operator's and on the third peer's
  side alike.
- **The fixture needed two corrections the run measured rather than guessed.** `Container.CanHoldItem` is
  asked BEFORE the refusal route, so a target that cannot hold the child at all (`pouch`, weight ceilings
  2.5/1.0 against a 1.5 dogfood) leaves the native loop with `flag == false` and the gesture falls through
  to the world path — the first attempt dropped the whole source bag (`[DragFlow] release fell through to
  the WORLD path (no UI target hit)`), which is a staging failure and not this ticket's shape. And
  `container-fill` cannot address a container by type when two carried containers share that type: the
  second bag was created only to be nested and the fill bound the wrong pair
  (`LoadItem left trashbag outside the container`). The published fixture uses distinct definitions
  (`trashbag` outer, `plasticbag` target, `duffelbag` source) with the capacities read off the prefabs
  (`p2-container-capacity.json`, `p3-item-weights.json`).
- **The failing half is filed, not fixed here.** `todo/second-drop-report-loses-its-world-object.md` carries
  this reading, its reproduction and the acceptance a fix owes; a batch run does not change code.

## Residuals for the user

None: every row of this ticket is a machine row, and the two that failed name their evidence.

## Limits

- **One owner direction only.** The owner was the sandbox guest (`76561198863287957`) and the operator the
  physical-machine host in every row; the mirrored direction (a guest operating the host's inventory) was
  not driven.
- **Two runs, one gesture shape, one target definition.** The gesture was driven twice on independently
  built items (first fixture, then fresh light items) and reproduced the same failure both times; nothing in
  this run says how often a third-party view finds an id-less same-prefab copy within the adopt tolerance,
  which is the shape the failure needs.
- **The destroy after the adoption is a reading, not an attribution.** The run shows the adopted copy
  destroyed on both viewers and the kernel's terminal transition; it does not name which code path
  destroyed it, and the ticket asks for that attribution before any fix.
- **The run did not stage a multi-child expansion whose children land apart.** The native loop unloads every
  child out of one source container, so the two reports carry the same position by construction; a variant
  with different landing positions was not produced.
- **The world table was read on the host only**; the two sandboxes' own copies were read through their own
  logs (`[ItemSpawn]`, `[ItemBind]`, `[ItemTrace]`), not as authoritative tables.
- **No wire, save or feel check.** The run read no messages and no saves, and judged no hand-feel.

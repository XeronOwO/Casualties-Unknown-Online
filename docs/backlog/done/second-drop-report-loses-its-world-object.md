# A second drop report at the same position loses its world object

- Status: Done (batch `20261007-b`: the adopt tie-break is a named rule and the runtime row that failed in
  batch `20261006-h` now passes 2/2 — re-running that batch's fixture, the operator's one release of the
  owner's source bag onto the nested target's panel button materializes BOTH same-frame children as their own
  world objects on the operator and on the third peer, BOTH ids stand in the host's world table (`worldCount`
  259 → 261 → 263, `terminalCount` 0), and the whole session carries no `[ItemBind]` for any of the four
  children, no `OnItemDestroyed`, no `Kernel command rejected` and no `InvalidTransition`. The second child's
  line shape changed exactly as predicted: its own `[ItemSpawn] materializing …` instead of a bind onto the
  proxy the renderer was retiring. Pins re-run in the same batch: `AdoptTargetRuleTests` 9 passed / 0 failed
  and `AdoptTargetGateTests` 26 passed / 0 failed, against
  `0.1.0+2675221cc96cbe1e8bc39a84e5ac1dfbeab21671`. Record
  `docs/evidence/acceptance/second-drop-report-loses-its-world-object-20261007-b.md`.)
- Priority: Medium
- Category: Item sync / world materialization (the drop report's adopt path)
- Source: acceptance batch `20261006-h` (2026-10-06), rows 3 and 4 of
  `done/drop-pending-single-slot-overwrite.md` — the run that finally produced two same-frame departures
  read this on both of them. Filed by the repository's own rule: a batch run does not change code, and a
  finding a run turns up is filed with its evidence.
- Related: `done/drop-pending-single-slot-overwrite.md` (its producer: R5's container expansion refusing
  every admitted child, which is what makes two drop reports share one position in one frame),
  `todo/nested-container-clone-proxy-leaks-as-world-item.md` (the same clone-proxy family one level down: a
  proxy the renderer cannot load at all stays a root object and enters the item domain at its own `Start` —
  source reading, not yet observed at runtime),
  `review/runtime-entity-markerless-bind-absorption.md` (the runtime-ENTITY sibling of this pattern — a
  record bound to a markerless same-prefab copy by position; this ticket is the item-domain instance),
  `done/guest-container-contents-ghost-drops-on-host.md` (the display-proxy family this path escaped, and the
  ticket that first named the proxy skips as the live defence),
  `done/container-move-snapshot-only-sync.md` (the drop carrier and the position it reports)
- Acceptance record: `docs/evidence/acceptance/second-drop-report-loses-its-world-object-20261007-b.md`

## Problem (evidence)

Batch `20261006-h` drove the gesture `done/drop-pending-single-slot-overwrite.md` asks for — the owner
carries a `duffelbag` holding two `dogfood`s, a `plasticbag` target sits inside the owner's carried
`trashbag`, and the operator releases the source bag's proxy onto the nested target's panel button with the
game's own `expanddesc` bind held. The owner's own client is correct: both children's departures register in
one frame and both commit, and both of its own objects stay in the world (`[ItemDrop] … present — re-placing
at (3.1,490.3)` for both, `Destroy=0` in its own `[ItemTraffic]` lines).

The two VIEWERS lose the second child. On the operator and on the third peer alike, in two independent runs
(the first fixture, then fresh light items):

```text
[ItemDrop] dogfood (id 43852695189) not present — requesting materialization at (3.1,490.3), container 0, parentPos (0.0,0.0).
[ItemSpawn] materializing dogfood (id 43852695189) at (3.1,490.3), vel (0.0,0.0), container 0.
[ItemDrop] dogfood (id 39557727893) not present — requesting materialization at (3.1,490.3), container 0, parentPos (0.0,0.0).
[ItemBind] bound existing dogfood at (3.1, 490.3) to id 39557727893 (no materialization).
[ItemTrace] op=54 item=39557727893 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
Kernel command rejected by host for item 39557727893: InvalidTransition.
```

and the host's authoritative read afterwards (`w2-host-tables-after-repeat.json`) holds the first child in the
world table and the second in `terminal` (`worldCount: 242`, `terminalCount: 2` — the other terminal entry is
the first run's second child, `26672826005`). So a report that was SENT and COMMITTED leaves its item with no
standing object on either peer, and the guest's own `ItemDestroy` command for it is refused as
`InvalidTransition (terminal item … cannot be destroyed again)`.

## What the run established, and what it did not

- **Established.** The drop reports themselves are correct and both commit on the owner. The failure is on
  the receiving side, in the report's materialization step: the second report found no local object, asked
  for materialization, and instead ADOPTED an id-less same-prefab world copy within
  `RemoteItemSceneOps.FindExistingAt`'s tolerance (`AdoptTolerance = 1.5f`, which by its own filter excludes
  every object already carrying `ItemInstanceId`, excludes remote clone proxies, and requires
  `ItemWorldSync.IsWorldItem`). That adopted copy was destroyed ~10–30 ms later with its id still set
  (`origin=OnItemDestroyed` names the id), which is NOT the shape of the adapter's own remote kill
  (`KillRemoteItem` zeroes the ids of the whole subtree BEFORE destroying, precisely so a remote deletion
  does not echo back as a local destroy report).
- **Established, and it is why the two children differ.** The first report of the pair materialized a NEW
  object and the second adopted an existing one, 8 ms apart, at the same reported position. The adopt
  target therefore appeared between the two reports, at a position where nothing id-less was found for the
  first.
- **Not established at the time — this ticket's first job, answered in `## Root cause` below.** Which code
  path destroyed the adopted copy, and where the id-less copy it adopted came from. Candidates the evidence
  did not separate then, all with their pointers: the materialization bookkeeping that remembers the frame an
  object was materialized in (`RemoteItemSceneOps._materializedFrame`) and the deferred destroy next to it;
  the state application the adopt path runs over a pre-existing object (`BindExistingItem` →
  `ApplyAuthoritativeState`, which restores condition and contents, where the adapter's fresh-object path
  stamps an id instead — a condition the game destroys at zero would behave differently on the two paths);
  and CUO's own item-destroy application for the same id arriving after the adopt. The fix cycle attributes
  it before changing anything: the destroy is the renderer's own deferred `Object.Destroy` of a proxy it had
  just retired, and both of the other candidates are excluded by the batch's own sibling reading.
- **Why it matters beyond this row.** Any drop report whose position coincides with an id-less same-prefab
  world copy can take this path. The same-frame pair is what makes it DETERMINISTIC here (every R5
  expansion unloads all of its children out of one source container, so they all report the same position),
  not what makes it possible.

## Root cause (attributed 2026-10-06, from this batch's own logs and the game's own code)

The destroy is Unity's DEFERRED `Object.Destroy`, queued by the clone renderer in the same frame — the object
the scan adopted had ALREADY been retired, and the two markers that exist to identify a display proxy both
stopped answering for it:

1. **The id-less copy was the owner's clone proxy for the FIRST child, retired between the two reports.** The
   first departure removes that child from the owner's clone snapshot
   (`[CarriedSync] removed 43852695189 from …'s snapshot contents — re-rendering the clone.`, 22:40:41.262 on
   the operator / .278 on the third peer), which re-renders the carried `duffelbag` proxy's contents through
   `CloneInventoryRenderer.RestoreRemoteContents`. The stale child proxy is retired with
   `container.UnloadItem(old)` — and the game's `Container.UnloadItem` is `transform.SetParent(null)` plus
   `transform.position += Vector3.up * 1.5f` (`Container.cs:161-163`), so the object stops being a container
   child (`ItemWorldSync.IsWorldItem` turns TRUE) and lands exactly on this scan's own tolerance
   (`AdoptTolerance` = 1.5) — immediately followed by `old.gameObject.SetActive(false)` and
   `Object.Destroy(old.gameObject)`, which Unity defers to the end of the frame. Until that destroy lands the
   object is still in `Item.allItems` (`Item.Start` adds, `Item.OnDestroy` removes — `Item.cs:96,118`).
2. **The scan's only proxy test cannot see a deactivated object.** `GetComponentInParent<RemoteCloneRender>()`
   defaults to `includeInactive: false`, and the renderer had just deactivated the object — the one lookup
   that exists to refuse a display proxy stops answering for exactly the proxy that has just become a
   plausible world item. Everything else the scan asked passed: same definition, no `ItemInstanceId`, a
   detached parent chain, and a position within tolerance. So `[ItemBind] bound existing dogfood at
   (3.1, 490.3) to id 39557727893 (no materialization).` stamped an authority id onto it.
3. **The queued destroy then carried the stamped id.** 12 ms later (`.280` on the operator, `.295` on the
   third peer) the deferred destroy landed with the id set, so the local `Item.OnDestroy` hook reported a real
   destroy: on the host that report commits into its own kernel (id → `terminal`), and the third peer's
   `ItemDestroy` for the same id was refused `InvalidTransition (terminal item … cannot be destroyed again)`.
   `RemoteItemSceneOps.KillRemoteItem` zeroes a subtree's ids before destroying precisely so a remote deletion
   is silent — this destroy came from the scene, which is why it was not. **The report had a second door, and
   it was the same blind lookup**: `ItemWorldSync.OnItemDestroyed`'s own display-proxy guard asked
   `GetComponentInParent<RemoteCloneRender>()` too, so even the destroy of a proxy would have been reported.
   Both doors are the one predicate now (`ItemWorldSync.IsDisplayProxy`), which is why this cycle is a family
   change rather than a one-line guard at the scan.

The ticket's other two candidates are excluded by the same reading:

- **The state application over a pre-existing object** (`BindExistingItem` → `ApplyAuthoritativeState`) is not
  it: the sibling report ran the same captured state through `MaterializeWorldItem`
  (`item.condition = w.Item.Condition`) and that object survived, and the game's own self-destroy needs
  `condition <= 0f` with `destroyAtZeroCondition` set (`Item.cs:157-178`) — the batch's dog foods were
  fixture-fresh and the first child kept the same value.
- **CUO's own item-destroy application for the same id** is not it either: the trace names
  `origin=OnItemDestroyed`, the local `Item.OnDestroy` hook, and a remote application would have gone through
  `KillRemoteItem`, which zeroes the id first and reports nothing.

## What landed (2026-10-06)

- The adopt scan's tie-break is a named, pure rule: `AdoptTargetRule.Allows(sameDefinition, alreadySynced,
  displayProxy, retired, worldItem, tutorialProp, withinTolerance)`. `RemoteItemSceneOps.FindExistingAt`
  gathers the seven facts from the candidate and routes EVERY candidate through it, so a refused candidate
  falls through to `MaterializeWorldItem` — the row gets its own object instead of an id stamped onto a
  corpse.
- The proxy fact comes from one shared predicate, `ItemWorldSync.IsDisplayProxy(item)`: the proxy's OWN
  marker (`RemoteInventoryItemId`, written onto the proxy itself by the clone renderer and unaffected by the
  detach) OR the clone-tree marker (`RemoteCloneRender`) read with `includeInactive: true`, which is the half
  the defect turned on.
- The same predicate now guards every classifier that may take authority over "an id-less standalone world
  item": the adopt scan, `ItemReconcile`'s late-local sweep, `GeneratedItemAuthority.Publish` (which would
  otherwise allocate an id to the doomed proxy) and `GeneratedItemReconcile.Apply`'s leftover sweep.
- And the domain paths that must never ADDRESS a display proxy ask it too, because they carried the same blind
  lookup: `ItemWorldSync.OnItemDestroyed` (step 3 above — the door the batch's destroy was reported through),
  the three lookups of `RemoteItemSceneOps.FindWorldItem` (the "live defence" of
  `done/guest-container-contents-ghost-drops-on-host.md`), and both candidate loops of
  `RemoteItemSceneOps.BindToContainer`, whose position scan would otherwise stamp an originator's container id
  onto a carried container's clone proxy. The input-path guards (`RemoteCloneContainerGuard`,
  `RemoteDragProxyQuery`, the drag query predicates) keep their own marker tests deliberately: they run on a
  click or a drag, and an inactive object cannot be clicked, so the inactive half of the hole cannot open
  there. That scope decision is recorded in `AdoptTargetGateTests`' `DomainPaths` comment.
- Pins: `AdoptTargetGateTests` (the scan's rule call plus both new facts, the rule's clause set, the
  predicate's two markers and its inactive coverage, the four classifiers' proxy test with a census floor of
  four, the six domain-path tests with their own floor, every matcher pinned by positive and negative samples)
  and `AdoptTargetRuleTests` (the truth table, each clause able to refuse on its own, plus this batch's exact
  object read clause by clause). The gate was written first and read RED against the unfixed tree — its four
  shape facts failed (the missing rule call, the missing clause set, the missing predicate, and a
  display-proxy test in none of the four classifiers), which is a session measurement rather than a figure the
  frozen tree can reproduce, since the gate grew its clause samples after that run. The mutation check dropped
  `!retired` from the rule and failed the gate's clause check, the per-clause case and the batch-shape case,
  then passed again on restore.

## Goal

A drop report that names an item must end with that item standing as its own world object on every
receiver, or with an explicit, reported refusal — never with the reported item destroyed and no object.

## Acceptance

- **Unit / pinned behaviour.** The adopt path's tie-break is pinned: an id-less same-prefab world copy at
  the reported position is either adopted and SURVIVES, or is refused and the row materializes beside it.
  Whatever the fix chooses, the choice is a named rule with a regression case, and a mutation shows the case
  fails without it. **Decided and landed:** the rule bounds the CANDIDATE, not the position — a live,
  non-proxy, still-unsynced world object is adopted as before (and survives: the clone renderer owns only the
  proxies it created), while a display proxy or an object the scene has already retired is refused and the row
  materializes its own object beside it. Cases are `AdoptTargetGateTests` (the wiring and the census) and
  `AdoptTargetRuleTests` (the truth table and this batch's own object); the mutation result is in
  `## What landed`.
- **Runtime (three clients).** Re-run batch `20261006-h`'s fixture (`docs/evidence/acceptance/
  drop-pending-single-slot-overwrite-20261006-h.md`): one gesture, two same-frame children, and BOTH ids in
  the host's world table, BOTH materialized on the third peer (an `[ItemSpawn]` or an explicit bind line
  each), no `terminal` entry for either, and no `InvalidTransition` for either — while the owner's own
  reading stays as this batch recorded it (two departures registered in one frame, both committed). The
  second child's line is expected to change shape: an `[ItemSpawn] materializing …` of its own, and no
  `[ItemBind] …` for the proxy the renderer is retiring in the same frame.
- **The producer's own row** (`done/drop-pending-single-slot-overwrite.md`) is judged against this fix: its
  world half was the row that failed here, and its machine half already passes 2/2.

## Acceptance readings (batch `20261007-b`, 2026-10-07)

The batch re-drove the fixture above on the deployed `0.1.0+2675221c…`, twice, on the same three roles.
Full reading and evidence pointers:
`docs/evidence/acceptance/second-drop-report-loses-its-world-object-20261007-b.md`.

1. **The runtime row passes 2/2 and the failing line shape is gone.** On all four children of the two
   gestures the operator and the third peer write their own `[ItemSpawn] materializing dogfood (id …)`, with
   no `[ItemBind] … bound existing …` for any of them, no destroy and no kernel refusal anywhere in the
   session; the host's world table holds every one of them with `terminalCount: 0`, and the owner's own
   reading is unchanged (both of its objects re-placed, two departures registered out of one frame and both
   committed).
2. **The rule still adopts, which is what bounds it.** The `[ItemBind]` lines the session carries are the two
   guests' world-entry shape for six world-generated items at far positions, and the host has none at all —
   so the tie-break refuses a retired or proxy candidate without turning the scan into "never adopt".
3. **The pins ran in this batch.** `AdoptTargetRuleTests` 9 passed / 0 failed and `AdoptTargetGateTests` 26
   passed / 0 failed under the `FullyQualifiedName~AdoptTarget` filter; the mutation that reads the rule's
   `!retired` clause red stays the fix cycle's process record (see the record's `Limits`).

# Retired Display Proxy vs The Adopt Scan — Self-Check (2026-10-06)

Delivery fact sheet for `docs/backlog/review/second-drop-report-loses-its-world-object.md`, the defect batch
`20261006-h` filed on rows 3 and 4 of the producer ticket: on the operator and on the third peer alike, the
second of two same-frame child departures was bound to an id-less same-prefab world copy
(`[ItemBind] bound existing dogfood at (3.1, 490.3) to id 39557727893 (no materialization).`) and that copy
was destroyed 10–30 ms later WITH the id set, so the host's kernel moved the item to `terminal` and the third
peer's own `ItemDestroy` was refused `InvalidTransition`. Record:
`docs/evidence/acceptance/drop-pending-single-slot-overwrite-20261006-h.md`. The reading below is this
cycle's attribution of that destroy plus the fix; the run itself is the batch's.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The drop report's materialization path is the adopt scan: a report whose item is not present locally calls `RemoteItemSceneOps.SpawnWorldItem`, which binds a positional candidate (`FindExistingAt` → `BindExistingItem`) or materializes a fresh object (`MaterializeWorldItem`). | `ItemApplication.OnRemoteItemDropped` (`[ItemDrop] … not present — requesting materialization`), `RemoteItemSceneOps.SpawnWorldItem`; the batch's own lines, third peer 22:40:41.276/.277/.282/.285 |
| 2 | The id-less copy was a clone display proxy the renderer had just RETIRED, between the two reports: the first child's departure removed it from the owner's clone snapshot and re-rendered the carried bag's contents. | `[CarriedSync] removed 43852695189 from …'s snapshot contents — re-rendering the clone.` (operator 22:40:41.262, third peer .278), `CloneInventoryRenderer.RestoreRemoteContents` (the `previous`/`used` removal pass) |
| 3 | The retire step is what turns the proxy into a plausible world item: `Container.UnloadItem` detaches it (`transform.SetParent(null)`, so `ItemWorldSync.IsWorldItem` answers true) and moves it `Vector3.up * 1.5f` — exactly `RemoteItemSceneOps.AdoptTolerance` — and the renderer then deactivates it and queues `Object.Destroy`. | `reversing/Assembly-CSharp/Assembly-CSharp/Container.cs:154-169` (`UnloadItem`), `RemoteItemSceneOps.AdoptTolerance = 1.5f`, `CloneInventoryRenderer.RestoreRemoteContents` (`container.UnloadItem(old)` → `SetActive(false)` → `Object.Destroy`) |
| 4 | Until that deferred destroy lands the object is still enumerable, which is the whole window the scan ran in. | `reversing/…/Item.cs:94-96` (`OnDestroy` → `allItems.Remove`), `:112-118` (`Start` → `allItems.Add`) |
| 5 | The scan's only proxy test could not see it: an ancestor lookup of the tree marker, whose default skips INACTIVE objects, on an object the renderer had just deactivated. Every other clause passed (same definition, no `ItemInstanceId`, detached parent chain, position within tolerance). | `RemoteItemSceneOps.FindExistingAt` (pre-fix), the third-peer line at `.285` |
| 6 | The destroy was REPORTED because the report path's own proxy guard asked the same inactive-blind lookup; the adapter's remote kill instead zeroes the whole subtree's ids before destroying, which is why a remote deletion never echoes. | `ItemWorldSync.OnItemDestroyed` (pre-fix guard), `RemoteItemSceneOps.KillRemoteItem`/`KillNow`; `[ItemTrace] op=54 … origin=OnItemDestroyed result=Committed(1) events=[Destroyed]` (third peer .295), `op=36` (operator .280) |
| 7 | The kernel consequence: the host's own report commits into its own authority (id → `terminal`), and the guest's command for the same id is then refused. | host `Item kernel authority wire-command rejected: InvalidTransition (terminal item 39557727893 cannot be destroyed again)` .310, third peer `Kernel command rejected by host … InvalidTransition` .334; `w2-host-tables-after-repeat.json` (`worldCount: 242`, `terminalCount: 2`, the id in `terminal`) |
| 8 | The ticket's two other candidates are excluded by the same reading: the state application cannot be it (the sibling report ran the same captured condition through `MaterializeWorldItem` and survived, and the game's self-destroy needs `condition <= 0f` with `destroyAtZeroCondition`), and CUO's own destroy application cannot be it (the trace names the local `Item.OnDestroy` hook, and a remote application goes through `KillRemoteItem`). | `Item.cs:157-178` (`Update`'s destroy-at-zero), `ItemWorldSync.OnItemDestroyed`/`KillRemoteItem`, the sibling's `[ItemSpawn] materializing 43852695189` |
| 9 | The family this defect sits in: four classifiers decide over "an id-less standalone world item" without ever knowing whether the object is a display proxy (the adopt scan, the reconcile's late-local sweep, the generation publish, the restored-cut leftover sweep), and six domain-path tests exist so a proxy is never ADDRESSED (three in `FindWorldItem`, one in `OnItemDestroyed`, two in `BindToContainer`). | `RemoteItemSceneOps.FindExistingAt`/`FindWorldItem`/`BindToContainer`, `ItemReconcile.OnRemoteItemSnapshot`, `GeneratedItemAuthority.Publish`, `GeneratedItemReconcile.Apply` |
| 10 | The input-path guards (`RemoteCloneContainerGuard`, `RemoteDragProxyQuery`, the drag/backpack query predicates) keep their own marker tests, deliberately: they run on a click or a drag, and an inactive object cannot be clicked, so the inactive half of the hole cannot open there. | `Patches/RemoteCloneContainerGuard.cs`, `Patches/RemoteDragProxyQuery.cs`; the scope decision is written into `AdoptTargetGateTests`' `DomainPaths` comment |
| 11 | The family's fifth classifier — `ItemWorldSync.OnItemInstantiated`, which would take a root proxy that was never loaded into the item domain at its own `Start` — is NOT fixed here: its reachability needs a nested container whose content load the native stacking rule refuses, which no batch has staged. It is filed with its reading and its fixture. | `docs/backlog/todo/nested-container-clone-proxy-leaks-as-world-item.md`, `CloneInventoryRenderer.RestoreRemoteContent` (`container.LoadItem(child)`), `Container.cs:116-151` |

## 2. The change

- The adopt scan's tie-break is a named, pure rule — `AdoptTargetRule.Allows(sameDefinition, alreadySynced,
  displayProxy, retired, worldItem, tutorialProp, withinTolerance)` — and `RemoteItemSceneOps.FindExistingAt`
  gathers the seven facts from each candidate and routes every one through it, so a refused candidate falls
  through to `MaterializeWorldItem`: the row gets its own object instead of an id stamped onto a corpse. Each
  argument is the fact as OBSERVED; the negation lives in the rule's body only.
- `ItemWorldSync.IsDisplayProxy(item)` is the one proxy test: the proxy's OWN `RemoteInventoryItemId` marker
  (written onto the proxy itself, unaffected by the detach) OR the clone-tree `RemoteCloneRender` marker read
  with `includeInactive: true` — the half the defect turned on.
- The same predicate now guards the four classifiers and the six domain-path tests listed in row 9. The
  destroy-report guard (row 6) is the one that explains why the batch's destroy reached the kernel at all.
- Deliberately NOT changed: the renderer's retire sequence (it is the presentation domain's own lifecycle, and
  the invariant is that the item domain never takes its objects); the input-path guards (row 10);
  `OnItemInstantiated` (row 11, filed). No wire and no save shape is touched, so `ProtocolVersion.Current` is
  unchanged.
- **This cycle's independent review is folded in**: it found the destroy-report guard and the other domain
  paths still carrying the blind lookup (rows 6 and 9 are its finding, and the change above is the remedy),
  the rule's doc comment documenting four of its seven arguments with the negated meaning, and a
  session-measured count quoted as if it were reproducible — all three are fixed here rather than deferred.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The rule's truth table, clause by clause | `tests/CasualtiesUnknownOnline.Tests/Items/AdoptTargetRuleTests.cs` (reflective, Integration) — one case per clause refusing on its own, one accepting a live generation-time object, and one reading this batch's exact object clause by clause (every pre-existing clause admits it; only the two new ones refuse it). 9/9 on the frozen tree |
| The scan is WIRED to the rule, the predicate reads both markers with inactive coverage, the four classifiers and six domain paths all ask it, and no matcher can pass on a mention | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/AdoptTargetGateTests.cs` — four shape facts plus two census facts (classifier floor 4, domain-path floor 6) and four matcher theories (proxy test, adopt scan, proxy predicate, clause identifier), each with positive and negative samples; 26/26 on the frozen tree |
| The pre-fix RED | the gate was written first and run against the unfixed tree: its four shape facts failed — the missing rule call, the missing clause set, the missing predicate, and a display-proxy test in none of the four classifiers. **Session measurement**: the gate grew its clause-identifier theory after that run, so the denominator is not reproducible from the tree and is not claimed; the failure NAMES are |
| The mutation | dropping `!retired` from `AdoptTargetRule.Allows` failed three cases — the gate's clause check, the rule's per-clause case, and the batch-shape case — and all three passed again once the clause was restored |
| The runtime row | the next three-client batch re-drives batch `20261006-h`'s fixture: one gesture, two same-frame children, BOTH ids in the host's world table, BOTH materialized on the third peer, no `terminal` entry for either, no `InvalidTransition` for either — the second child's line expected as its own `[ItemSpawn] materializing …` and no `[ItemBind] …` for the proxy being retired in that frame |

## 4. Limits

- **No session ran in this cycle.** The reading is the batch's own logs (still on disk, all quoted timestamps
  verbatim), the batch's artifact (`w2-host-tables-after-repeat.json`) and `reversing/`; the fix's runtime
  proof is the next batch's re-drive, and this cycle claims no green runtime row.
- **Unity's inactive-object lookup semantics are corroborated, not executed here.** That
  `GetComponentInParent<T>()` skips an inactive object is the reading that closes the chain; the batch's own
  `.280`/`.295` destroy report is the independent evidence that the guard did not answer, and no unit host can
  run Unity to demonstrate it directly.
- **The gate pins WHERE the predicate is asked, not that the predicate recognises every proxy.** A proxy with
  no marker at all (an id-0 content whose load was refused) is outside both markers — that is exactly row 11's
  filed shape, and the reason it is a separate ticket rather than a clause here.
- **A legitimately INACTIVE generation-time object is now refused.** No reachable case was constructed (a
  world-gen ground item is active; the inactive item prefabs are templates that are destroyed immediately),
  and the consequence would be a duplicate materialized beside it rather than a lost item — but the clause is
  a behaviour change and is recorded as such.
- **`BindToContainer`'s proxy skip is argued, not measured.** No batch has observed the position stamp land on
  a proxy; the change is strictly narrowing (a display proxy is never the generation-time container the stamp
  is for).
- **The pre-fix red is this cycle's own gate run, not a batch.** The failing rows are the batch's; re-deriving
  the failure at runtime would mean re-deploying a pre-fix build, which this cycle did not do.

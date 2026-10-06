# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **back to `todo/`** (`- Status: Todo`): every row
  this run could drive passes, including the drop row that rejected batch `20261005-e` and the wearable
  half no batch had ever driven; row A1g is still unjudged, and it is the only thing left — the same day
  it was found to be ordinary work rather than an impossibility (see that row's cell)
- Batch: `20261006-a` — tickets `container-move-snapshot-only-sync` (see Limits: two further rows were
  planned for this session and NOT staged, one of them on `remote-inventory-native-parity-rework`)
- Commit: `ad6f73ee` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+ad6f73ee142c6a8ca850a20a915cce400589e3ba` (`deploy.ps1` exit 0; `verify-deploy.ps1` exit 0,
  "Deployment matches this tree's build output", 34 of 35 deployed files matched)
- Run: 2026-10-06 09:41 → 09:56 (+08:00) · Host: physical machine, launched through Steam (evaluator
  `18590`, SteamId `76561198281246659`) · Guest: primary sandbox box (evaluator `18591`, SteamId
  `76561198863287957`) · Third peer: alternate sandbox box (evaluator `18592`, SteamId
  `76561199526807662`)
- Dependencies: preflight `11 present`, exit `0`; the machine gate before launch read `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`; neither sandbox carried a plugin shadow, so
  both read the physical deployment
- Artifacts: the batch directory under `acceptance-artifacts-dir` — the gesture recipe's own answers
  (`c*.json`), the fixture and probe JSON (`f*`, `wear-a.json`), the third peer's world reads, and the
  byte-marked log windows (`marks-a1.txt`, `marks-quiet.txt`) cited by name below. The independent
  adversarial review of this cycle is `review-20261006-a.md` in the same directory (not committed).

## The rows as planned before the run

The plan is the batch's own `scope.md`, written before the first gesture. `A1a`–`A1e` are the container
kinds the batch re-reads; `A1d`/`A1d′` and `A1h` are the rows this run exists for; `A1z` is the
zero-warning window that has to cover them all. `A1f` (battery unload) and `A1g` are in the plan too and
carry their own verdicts below.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1a | Insert (`MoveIntoContainer`), host owner / guest operator | machine | **pass** | Guest `[RemoteIntent] MoveIntoContainer captured for item 1190026921923 of 76561198281246659 (container 1185731954627 …)` 09:54:11.487 → host `[ItemTrace] op=15 … origin=OnItemLoadedIntoContainer result=Committed events=[ContainerContent]` + `[ContainerLoad] dogfood (id 1190026921923) moved inside body container trashbag — root content event up to trashbag (id 1185731954627)` + `replayed native MoveIntoContainer` 09:54:11.528/.529 → third peer `[CarriedSync] applied trashbag (id 1185731954627) to 76561198281246659's snapshot — re-rendering the clone.` 09:54:11.542. No divergence: the census is the `marks-a1.txt` window, not the gesture JSON (`c2-guest-list.json`, `c3-guest-insert-dogfood.json`) |
| A1b | Take-out (`TakeOutOfContainer`), host owner / guest operator | machine | **pass** | Guest `TakeOutOfContainer captured for item 1190026921923 …` 09:54:20.485 → host `op=16 … origin=OnItemUnloadedFromContainer result=Committed(1) events=[Unload]` + `replayed native TakeOutOfContainer` 09:54:20.499/.500; both viewers then only materialize the item (`[ItemDrop] dogfood … not present — requesting materialization at (-0.9,485.6)`); no divergence (`c4-guest-takeout-dogfood.json`) |
| A1c | Slot release (`PickUpToSlot`) onto the owner's EMPTY ring slot 5 | machine | **pass** | Guest `PickUpToSlot captured for item 1194321889219 … slot 5` 09:54:28.009 → host `[SlotMoved] waterbottle (id 1194321889219) → slot 5 (Drag) — host fact broadcast.` + `[PickUpResult] waterbottle → slot (slot 5)` + `item waterbottle now holds the owner's slot 5.` + `replayed native PickUpToSlot` 09:54:28.035; the release's own drop-then-pickup pair stayed silent on both viewers (`c5-guest-slot-release.json`) |
| A1d | **Drop (`DropItem`) after decision 236, BOTH owner directions** | machine | **pass** | Host owner 09:54:36: guest `DropItem captured for item 1198616856515`, host `op=18 begin … origin=OnItemDropped event=Drop` → **`[RemoteIntent] replayed native DropItem on item 1198616856515 — a drop report is pending and announces it on the next frame, so no immediate re-report.`** → `[ItemDropped] scrapmetal (id 1198616856515) at (-2.2,485.3), vel (0.0,-1.1)` → `op=18 … origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]` (09:54:36.579 → .605). Guest owner 09:55:53: host `DropItem captured for item 18082891413`, guest `replayed native DropItem … so no immediate re-report.` → `[ItemDropped] waterbottle … at (-1.3,484.7)` → `FlushPendingDrop result=Committed(1)`; operator 09:55:53.495, third peer `[ItemSpawn] materializing waterbottle (id 18082891413) at (-1.3,484.7)` 09:55:53.569. **No `[CharSync] divergence` on any client in the window** |
| A1d′ | The dropped item is found in the world by the client that is neither owner nor operator | machine | **pass** | The third peer's own `item-world-read` at cell `(509, 983)` radius 15 returns every dropped id: `bikehelmet id=22377858709` (guest-owned, dropped by the host), `waterbottle id=18082891413` (guest-owned), `bikehelmet id=1202911823811` (host-owned, dropped by the guest) and `scrapmetal id=1198616856515` (host-owned) — `c24-alt-world-__509___983________15.json`, six items (the cell was aimed by the drop positions, because this world's cell offset is not the one an earlier batch recorded — see Limits) |
| A1e | Guest as owner: insert **and** take-out with the host operating | machine | **pass** | Host `MoveIntoContainer captured for item 13787924117 of 76561198863287957 (container 9492956821 …)` 09:55:42.924 → guest (owner) `[ContainerLoad] dogfood … root content event up to trashbag (id 9492956821)` + `replayed native MoveIntoContainer` 09:55:42.941; then host `TakeOutOfContainer captured` 09:55:45.388 → guest `op=30 … OnItemUnloadedFromContainer result=Committed(1) events=[Unload]` + `replayed native TakeOutOfContainer` 09:55:45.397; no divergence (`c20-`, `c21-`) |
| A1f | Battery unload (`UnloadBattery`) | machine | **unproven** | **Not staged.** The run's fixture set carried no installed-battery receiver; `20261005-e`'s own lesson is that a `Utils.Create`d copy is refused by the native guard (`item.battery.hasBattery == false`), so the row needs a world-generated carrier this session did not bring. `20261005-e` read it `pass` and decision 236 does not touch the battery branch, but this record does not claim it |
| A1g | Container expansion (`MoveContainerChildren`) | machine | **blocked** | The native branch that produces the kind is gated by `Input.GetKey(KeyBinds.GetBind("expanddesc"))` in the game's own `TryPerformInventoryAction` (`reversing/…/PlayerCamera.cs`, the `expanddesc` arm of the container branch), read live as `expandBind: "LeftShift"`, `expandHeld: false` by `20261005-e`. While this run was open no in-process route to a HELD key was known, and the `input` dependency's own rule forbids reaching for one with OS input ("never do it", enforced by `AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput`), so the kind stayed unjudged and was never guessed from the other five. The one thing the run tried in order to remove the blocker — the game's own radial-centre gesture as a way to dress a body in process — is reported in Limits; it did not land, and it does not touch this kind either way. **Superseded the same day, after this run closed (2026-10-06):** a held key IS reachable in process — posting the client's own window a `WM_KEYDOWN` carrying the key's scan code makes `Input.GetKey(KeyCode.LeftShift)` read true on the next frame, and a `WM_KEYUP` carrying the scan code and the transition bits clears it, with `GetAsyncKeyState` at 0 throughout (no OS-level key state, no focus or foreground change). The mechanism and its measurement are in `docs/acceptance/lessons.md`; building it into the committed
driver is what row A1g now waits on, so the row is ordinary work rather than an impossibility. **Read
again 2026-10-06 (decision 237, batch `20261006-b`):** the hold IS committed — the `window-key` eval
declaration plus `recipes/key-hold.cs`, smoked in a running client — so what row A1g waits on is its own
three-client reading and nothing else |
| A1z | Zero warnings on both viewers across the whole gesture window, over at least one full periodic cycle | machine | **pass** | `[CharSync] divergence` count over `marks-a1.txt` = **0 on all three clients** (host, guest, alt), and the closing window (`marks-quiet.txt`) holds **no `[CharSync]` line and no divergence line** on any of the three — its only carried-fact lines are the host's two information-level starting-supply merges (`[CarriedSync] merged 2 starting supplies …` 09:56:36.204, `… merged 1 …` 09:56:36.240), which are not monitor warnings. The polarity is visible in the same window: for the container and slot kinds the owner still logs `[CloneRender] inventory changed — immediate re-report.` (09:54:11.528, 09:54:20.499, 09:54:28.035, 09:55:42.941, 09:55:45.397), and for the four drops it logs the held-back line instead |

## The two rows that had never been read before

`20261005-e` rejected this ticket on the drop kind and left the wearable half untouched. Both are now read
on a deployed artifact, in both owner directions, from the owner's own ordering lines and from the two
peers' monitors:

1. **The order is now the fix's own log line, not an inference.** Every remote-driven drop — `DropItem`
   and `DropWearable`, host-owned and guest-owned — produces
   `a drop report is pending and announces it on the next frame, so no immediate re-report.` and then,
   22–30 ms later, the committed `origin=FlushPendingDrop result=Committed(1) events=[Drop, Flush]`. The
   snapshot that omits the item is therefore sent *after* the report that explains its absence, and the
   peers materialize the drop instead of warning about it. In `20261005-e` the same two directions
   warned on both viewers with `left the inventory without an event sync`.
2. **The wearable half really does travel the patch-layer entry point.** The fixture is what makes it
   reachable: `PlayerCamera.TryPerformWorldActions` calls `Body.DropWearable` only when
   `this.body.GetWearable(dragItem.id)` answers non-null, and `RemoteDragPredicatePatches` answers that
   query from the body the ring shows. With each direction's owner wearing the wearable type its
   operator drags, every release produced one `DropWearable` capture; both owners' dressing is
   independently corroborated by the operators' own ring lists, which show the helmet as a proxy
   parented to `Head` (`c2-guest-list.json`, `c19-host-list-guest5.json`).

## What else the window held

One `[ERR]` and one refusal sit inside the window, both at the A1h gesture's second, and neither is a
divergence or a row failure — they are the repository's designed creation-before-operation invariant
doing its job (`done/item-creation-registration-first.md`, decision 137, pinned by
`CreationBeforeOperationTests`):

- host 09:54:46.974 — `[ERR] Protocol violation: 76561198863287957 reported ItemDestroy on item
  26672826005 whose creation this host has never judged. Refused at once — a sender must report an
  item's creation before any operation on it (creation-before-operation).`
- the operator had already logged its own half: `[ItemTrace] op=26 begin item=26672826005
  origin=OnItemDropped event=Drop` 09:54:46.873, then `op=28 … OnItemDestroyed result=Committed(1)
  events=[Destroyed]` 09:54:46.965, then `[ItemCommand] ItemDestroy on item 26672826005 (operation
  3801889743155760907) was refused by the host — it leaves the window after 0 re-report(s)` and
  `Kernel command rejected by host for item 26672826005: UnknownAggregate.` 09:54:46.997.

The refused item is a guest-local item the host never judged; the refusal is the invariant, it leaves no
divergence behind, and it is recorded here because a reader of this record is entitled to see every
`[ERR]` the window carried rather than only the lines that flatter the fix.

## Residuals for the user

None: every row above was judged from this run's own machine evidence (log excerpts, recipe and probe
JSON), and no row of this ticket is a visual or feel judgement. Row A1g's blocker is a driver capability
rather than a missing dependency, so no question to the user was raised for it: the dependency table's
degradation ladder is what asks, and `input` is `present` — the rule is that a row needing a capability
the run cannot execute stays `blocked`, which is what this one does.

## Limits

- **Row A1g stays `blocked`, with no substitute, and the attempt to remove its blocker is reported as
  what it was.** The one hypothesis `20261005-e` left open was that the game's own radial-centre gesture
  could dress a body in process. This run staged it with `remote-gesture mode=hover`
  (`f4-host-hover-helmet.json`: `radialOpen: true`, `pointerDistanceOverScale: 0`) and read it with
  `mode=probe` (`f5-host-probe-radial.json`: `leniency 0.95`, `uiScale 0.667`, `menuScale 0.01`,
  `radialCircleRadius 135`, `centerButtonCount 0`). The ring did not stay open across the game's own
  next frame — `HandleWhileDragging` re-closes it when
  `|radialMenu.position.x − Input.mousePosition.x|` exceeds `600 × uiScale`, and the run may not move the
  OS pointer. **That is not a claim that the gesture is undrivable.** The committed release path
  (`remote-gesture mode=release cast=-2`) forces `radialMenu.localScale` and repositions the ring inside
  the same call — which is how batch `20261005-d` drove its radial row — and this run never invoked it;
  the numbers a hover-time probe printed are session-only and are not restorable from the tree. Either
  way it does not touch A1g, whose gate is a HELD key and not the radial ring.
- **The A1h fixture's provenance is recorded rather than implied.** The committed recipe's first host
  invocation failed inside the evaluator (`f6-host-wear-helmet.json`: `eval 'recipe:item-wear' failed:
  Unexpected character \0022`); the host was dressed by `-Recipe wear-a` (`wear-a.json`), a temporary
  file whose body is exactly the body `tools/acceptance/recipes/item-wear.cs` now carries, and the guest
  was dressed under the committed name once that body was in place (`f5-guest-wear-helmet.json`,
  `worn=true`). The committed file was rewritten after the run only to normalise its line endings. The
  failure mode itself is unexplained and is recorded as an observation, not as a rule.
- **Two further planned rows were not staged.** `A1f` (battery unload) and
  `remote-inventory-native-parity-rework`'s row 8 battery-LOAD half are `unproven` here: the fixture set
  carried no installed-battery receiver. The same ticket's row 14 monitor half was in the plan
  ("driven only if the fixtures exist, else named as this run's limit") and is named here as that limit:
  no row of `remote-inventory-native-parity-rework` is judged by this batch.
- **One session, one reading.** The drop rows reproduced 2/2 in each kind and each direction; that is
  consistent, not a claim about rarity.
- The world's cell offset differed from the earlier batch's (the body stands at world `(0, 486.6)`, and
  the drops were found at cell `(509, 983)`), so the third peer's world read was aimed by the drop
  positions rather than by a previously recorded cell.
- An observation with no bearing on any row: `remote-gesture mode=list` reports the LOCAL member as
  `"inWorld": false` (`c2-guest-list.json`) while that same client's own `state` says `inWorld: true`
  (`s7-guest-state.json`). That array is only read by `mode=open`'s `auto` resolution, which this run
  never used (every gesture named its owner explicitly). Whether the field is wrong, or merely means
  something narrower than the recipe's name suggests, was not established here.

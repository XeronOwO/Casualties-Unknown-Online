# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **back to `todo/`**
  (`- Status: Todo — Rejected (…)`), row A1 fails on the drop kind
- Batch: `20261005-e` — tickets `container-move-snapshot-only-sync`,
  `remote-inventory-native-parity-rework`
- Commit: `cdd93044` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+cdd93044b027327fd2ec2b4269b90e43b56999db` (`deploy.ps1` and `verify-deploy.ps1` exit 0)
- Run: 2026-10-05 22:44 → 23:09 (+08:00) · Host: physical machine (evaluator `18590`, PID 46552,
  SteamId `76561198281246659`) · Guest: primary sandbox `Steam1` (evaluator `18591`,
  SteamId `76561198863287957`) · Third peer: alternate sandbox `Steam2` (evaluator `18592`,
  SteamId `76561199526807662`)
- Dependencies: preflight `11 present`, exit `0`; the machine gate before launch read `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`; neither sandbox carried a plugin shadow, so
  both read the physical deployment
- Artifacts: the batch directory under `acceptance-artifacts-dir` — the gesture recipe's own answers,
  the fixture and probe JSON, and the byte-marked log excerpts cited by name below

## The rows as planned before the run

The ticket states its expectation in prose ("insert, take-out, slot release, drop, container
expansion, battery load/unload … the operator's and the third peer's monitor at ZERO over at least one
full periodic cycle"). The table below is the written-before form of it, split by kind so a kind the
run cannot drive stays visible instead of hiding inside one verdict. `A1a`–`A1f` are remote-driven
kinds; `A1e` is the guest-as-owner container direction this fix's own ticket adds.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1a | Insert (`MoveIntoContainer`), host owner / guest operator | machine | **pass** | Guest `[RemoteIntent] MoveIntoContainer captured for item 1172847052739 of 76561198281246659 (container 1168552085443 …)` → host `[ItemTrace] op=12 … origin=OnItemLoadedIntoContainer result=Committed events=[ContainerContent]` + `[ContainerLoad] dogfood (id 1172847052739) moved inside body container trashbag — root content event up to trashbag` + `replayed native MoveIntoContainer`; the operator logs `[CarriedSync] applied trashbag (id 1168552085443) to 76561198281246659's snapshot — re-rendering the clone.` and the third peer the same, and **neither** logs a `[CharSync] divergence` for the gesture or for the rest of the cycle (`cm3-guest-insert-dogfood.json`, `marks-fix.txt`) |
| A1b | Take-out (`TakeOutOfContainer`), host owner / guest operator | machine | **pass** | The operator opens the bag's own contents window (`container-panel mode=remote` → `contentsCount: 1`, `cm8-guest-open-bag2.json`), then releases the contained child over empty space (`itemParentContainer: true` in `cm10-guest-takeout-world.json`): guest `TakeOutOfContainer captured for item 1172847052739 …` → host `[ItemTrace] op=17 … origin=OnItemUnloadedFromContainer result=Committed(1) events=[Unload]` + `replayed native TakeOutOfContainer`; both viewers silent |
| A1c | Slot release (`PickUpToSlot`) | machine | **pass** | Guest released the host's dog food onto the owner's empty slot 3 (`cm6-guest-takeout.json`, `castSlot: 3`): guest `PickUpToSlot captured for item 1172847052739 …`, host `[PickUpResult] dogfood → slot (slot 3)` + `item dogfood now holds the owner's slot 3.` + `replayed native PickUpToSlot`; both viewers log only the information line `[CarriedSync] dogfood (id …) not in …'s snapshot and slot unknown — the 1 Hz snapshot will carry the change.` and **no** divergence |
| A1d | Drop (`DropItem`), **both owner directions**, plus the third peer's own world read | machine | **fail** | Host-owned metal scrap dropped by the guest at 22:50:37: owner `[ItemTrace] op=18 begin … origin=OnItemDropped event=Drop` → `[CloneRender] inventory changed — immediate re-report.` → `[ItemDropped] scrapmetal (id 1185731954627) at (1.1,483.2) …` (committed 22:50:37.189); the **operator warns at 22:50:37.189 and the third peer at 22:50:37.182**: `[CharSync] divergence for 76561198281246659's scrapmetal (id 1185731954627): left the inventory without an event sync (the 1 Hz snapshot carried it).` Guest-owned water bottle dropped by the host reproduces it in the reverse direction (owner 22:53:21.505/`.532`, operator warns 22:53:21.526, third peer 22:53:21.574). The materialization half does pass: both peers log `[ItemDrop] scrapmetal … not present — requesting materialization at (1.1,483.2)` and the third peer's own world read returns the dropped id at the drop cell (`cm17-alt-world-after-drop.json`: `scrapmetal id 1185731954627 x=1.108 y=482.381`, beside the guest-owned `waterbottle id 18082891413 x=0 y=482.768`) |
| A1e | Guest as owner: insert **and** take-out with the host operating | machine | **pass** | Host `MoveIntoContainer captured for item 13787924117 of 76561198863287957 (container 9492956821 …)` → owner `[ContainerLoad] dogfood … root content event up to trashbag (id 9492956821)` + `replayed native MoveIntoContainer`, operator and third peer `applied trashbag …` and no divergence; then the take-out of the same child (`cm14-host-takeout-guestdogfood.json`) → owner `OnItemUnloadedFromContainer … events=[Unload]` + `replayed native TakeOutOfContainer`, both viewers silent (`marks-cm-reverse.txt`) |
| A1f | Battery unload (`UnloadBattery`) | machine | **pass** | Fixture: a world-generated `aed` was brought to the owner's body (a `Utils.Create`d copy carries the battery component but `hasBattery == false`, so the native guard refused the first two attempts — `probe-battery-bring2.cs`, `f7`/`f8`). Guest `UnloadBattery captured for item 1026818164675` → host `[PickUpResult] mediumbattery → slot (slot 1)` + `the native unload ejected item aed's battery onto the owner's body.` + `replayed native UnloadBattery`; both viewers log `[CarriedSync] added mediumbattery (id 1207206791107) to …'s snapshot — re-rendering the clone.` and no divergence |
| A1g | Container expansion (`MoveContainerChildren`) | machine | **blocked** | The native branch that produces this kind is gated by `Input.GetKey(KeyBinds.GetBind("expanddesc"))` in the game's own `TryPerformInventoryAction` (`reversing/…/PlayerCamera.cs`, the `expanddesc` arm of the container branch). The run's live reading of that same guard is `expandBind: "LeftShift"`, `expandHeld: false` (`probe-expand-key.cs`), and this run's driver is in-process only — by its own contract it never synthesises OS-level key state (`drive-in-process.ps1 -ListActions`: "in-process only; never OS-level keyboard or mouse"). No in-process route to the guard exists, so the kind stays unjudged: **blocked on that driver capability**, not guessed from the other kinds. **Superseded 2026-10-06 (after `20261006-a` closed):** an in-process route DOES exist — a window message carrying the key's scan code, posted to the client's own window, moves `Input.GetKey` while `GetAsyncKeyState` stays 0 — so what the row waits on became ordinary work rather than an impossibility, and **the capability is committed the same day (decision 237, batch `20261006-b`)** as `tools/acceptance/driver/eval-declarations/window-key.cs` plus `tools/acceptance/recipes/key-hold.cs`; the row now waits on its own three-client reading (`docs/acceptance/lessons.md`, `container-move-snapshot-only-sync-20261006-a.md`). This row's verdict stands as what was known at the time and is not a `pass` |

## The zero-warning half over a full cycle

Both viewers were read from one byte mark per client taken before the first gesture, and again over a
deliberately quiet window at the end of the run:

- Every divergence line this run produced, in full: **three** on the operator (the two drops of a
  host-owned item plus the two world-driven drops below) and **four** on the third peer (the same two
  drops in both directions plus those two), all of them the single wording
  `left the inventory without an event sync`. Owner-side, the host logged exactly one, for the
  guest-owned drop — a client never warns about its own items.
- The quiet window (16 s ≈ 16 snapshot periods, `marks-quiet.txt`) is empty on both viewers: no
  divergence and no carried-sync line at all.

## The drop failure, read at its site

The warning is not the missing carrier the batch `20261005-d` rejection described; the drop report now
flows and the peers materialize the item from it. What is left is an **ordering race inside the
owner's own client**, and the run shows both halves of it:

1. `RemoteIntentApplier` ends every applied, non-continuous intent with
   `domains.CharacterDataSync.ReportInventoryChanged(body)` — "The owner's own scene changed: the
   immediate re-report makes every clone … converge now." For a `DropItem` intent that re-report is
   sent at the moment of the apply (owner log 22:50:37.159) and already omits the item.
2. The drop's own item report is committed later, by the pending-drop flush (owner log
   22:50:37.188/.189 — `[ItemDropped] …` then `origin=FlushPendingDrop result=Committed(1)
   events=[Drop, Flush]`), i.e. **~30 ms after** the character snapshot the peer has already applied.
3. The peers therefore compare their fact table against a snapshot that lost an item whose event is
   still in flight and warn — exactly what `CloneFactTable.WarnOnDivergence` documents as intended
   ("A change whose event is still in flight trips the warning too").

Two controls in this run pin the attribution:

- **A local drop does not warn.** The host dropped its own lantern with its own gesture
  (`cm16-host-local-drop.json`, owner log 22:53:37.225 `origin=OnItemDropped event=Drop` →
  `[ItemDropped] lantern …`); the same two viewers logged **no** divergence (`marks-cm-localdrop.txt`),
  and the owner logged no `inventory changed — immediate re-report` either. The warning belongs to the
  remote-intent apply path, not to drops in general.
- **A world-driven multi-drop warns exactly for the drops whose report had not been committed yet.**
  At 22:56:12 the world itself dropped three of the host's items inside 2 ms (a jump-pad launch and a
  block-break burst — `[TrapEvent] kind=JumpPadLaunched`, a run of `OnBlockDamaged`/`OnBlockSet`
  lines). The `aed` whose report was flushed at 22:56:12.448 was **not** warned about; the trash bag
  and the water bottle, still pending, were (`22:56:13.113`/`.130` on both viewers). This is an
  observation, not a driven row: the run did not stage it.

## What this run did not prove

- The expansion kind (`A1g`) — blocked on the driver capability named in the table; it is the one kind
  of the ticket's six the run could not put in front of the monitor.
- Battery **load** (`LoadBattery`) — the unload half passes; the load half needs a battery item in the
  ring and a receiver with a free slot, which this session did not stage.
- The multi-item insert series (four items in one bag) was not re-driven; one insert was read twice
  (before the take-out and as its re-insert) and the ticket's sibling record carries the series.
- A run of one session proves what it observed: the drop race reproduced 2/2 driven drops and 2/2
  world-driven pending drops, which is consistent but is not a claim about rarity.

## Residuals for the user

None: every row above was judged from this run's own machine evidence (log excerpts, probe JSON), and
no row of this ticket is a visual or feel judgement.

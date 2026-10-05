# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **back to `todo/`** (`- Status: Todo — Rejected (…)`),
  row A1 failed
- Batch: `20261005-d` — tickets `container-move-snapshot-only-sync`,
  `remote-inventory-native-parity-rework`
- Commit: `77139b17` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+77139b1704d90977d25aa60ec3af2b35b48d9963` (`deploy.ps1` and `verify-deploy.ps1` exit 0)
- Run: 2026-10-05 21:28 → 21:50 (+08:00) · Host: physical machine (evaluator `18590`, PID 43024,
  SteamId `76561198281246659`) · Guest: primary sandbox `Steam1` (evaluator `18591`, PID 48084,
  `76561198863287957`) · Third peer: alternate sandbox `Steam2` (evaluator `18592`, PID 20612,
  `76561199526807662`)
- Dependencies: preflight `11 present`, exit `0`; machine gate before launch `active=cuo`,
  `game-running=false`, `swap-needed=false`, `launch=ok`; neither sandbox carried a plugin shadow, so
  both read the physical deployment
- Artifacts: the batch directory under `acceptance-artifacts-dir` — probe JSON, the gesture recipe's
  answers and byte-marked log excerpts, cited by name below

## The rows as planned before the run

This ticket states its expectations in prose; the table below is the written-before form of them, and the
run judged exactly these four rows.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1 | Every remote-driven kind reports its own fact: insert, take-out, slot release, drop, container expansion, battery load/unload — and the operator's and the third peer's clone-fact monitor stay at ZERO over at least one full periodic cycle | machine | **fail** | After `MoveIntoContainer` (guest → host's trashbag), the operator logged `[CarriedSync] applied trashbag (id 1207206791107) to 76561198281246659's snapshot — re-rendering the clone.` and **one millisecond later** `[CharSync] divergence … nested container contents changed without an event sync` plus `[CharSync] divergence … dogfood … left the inventory without an event sync` (`cm13-guest-insert-dogfood.json`, `marks-cm1.txt`). The third peer shows the same pair (`applied` 21:39:24.703, two divergences 21:39:24.704). The same shape repeated for `scrapmetal` (`cm18`, 21:41:51.846) and for the drop (`cm19`/`marks-cm4.txt`, lantern `left the inventory without an event sync`). The event arrives — the warning does not go away |
| A2 | A dropped item is found in the world by a client that is neither the owner nor the operator — **host as owner** | machine | **pass** | Guest dropped the host's lantern: `DropItem captured for item 1202911823811` → host `replayed native DropItem on item 1202911823811` (`cm19`); the third peer's own world read at cell (510,941) returned `id=1202911823811 type=lantern hasId=true x=1.514 y=443.594` (`cm21-alt-world.json`) and the host's read returned the same id at `(1.514, 443.64)` (`cm22-host-world.json`) |
| A3 | Same, **guest as owner** (reverse direction) | machine | **pass** | Host dropped the guest's dogfood (`9492956821`): owner log `replayed native DropItem on item 9492956821` (`marks-cm5.txt`); the third peer's world read then returned `id=9492956821 type=dogfood x=2.734 y=441.734` beside the lantern (`cm26-alt-world.json`) |
| A4 | `PickUpToSlot`'s drop-then-pickup pair does not warn the operator's monitor | machine | **pass** | Guest released the host's bandage onto the owner's slot 2: `PickUpToSlot captured for item 1190026921923 of 76561198281246659 (container 0, slot 2…)` → host `item bandage now holds the owner's slot 2.` + `replayed native PickUpToSlot` (`marks-cm3.txt`); the operator's `[CarriedSync] applied bandage (id 1190026921923) …` at 21:41:49.468 is followed by **no** divergence line for that gesture |

Container expansion (`MoveContainerChildren`) and battery load/unload were not driven in this run; A1
failed on the kinds that were, which is the row's verdict.

## What the failure is, and what it is not

The fix under test did what it set out to do on the owner's side: the owner's own hook now produces the
fact (`[ItemTrace] op=6 item=1198616856515 origin=OnItemLoadedIntoContainer result=Committed
events=[ContainerContent]`, `[ContainerLoad] dogfood … moved inside body container trashbag — root content
event up to trashbag`), and the operator and the third peer both **apply** it
(`[CarriedSync] applied trashbag …`). What still warns is the monitor's own comparison.

Read at the failure site: `CloneFactTable.WarnOnDivergence` compares the PREVIOUS SNAPSHOT with the
incoming one (`if (_cloneData.TryGetValue(owner, out var prev)) { WarnOnDivergence(owner, prev, data); }`),
and `ApplyCarriedSync` for a container move replaces the ROOT container's contents
(`TryReplaceNested` branch, the `applied {Type} … to {Owner}'s snapshot contents` line this run read)
without removing the moved child from the top-level list and without the root's contents matching the
next snapshot field for field. One report carrier moving an item INTO a container therefore still leaves
two facts for the next snapshot to carry uncovered: the child's departure from the top level, and the
container's new contents. The slot-release path (`PickUpToSlot`) carries the item itself and writes its
slot, which is why the same monitor stayed silent for A4 — that contrast is the run's sharpest reading.

That is the run's reading plus the code location it points at, not a fix: the ticket returns to `todo/`
with the failing row, the two divergences and this contrast named.

## Limits

- The monitor is deliberately unchanged, so the zero-warning row is the only thing A1 could have passed
  on; it did not.
- The world coordinates the run staged: the three bodies were placed at world (0, 445) so the operator
  and the owner satisfy the local line-of-sight gate (the first insert attempt, before the move, was
  refused with `76561198863287957 cannot see 76561198281246659 on this client`).
- The gesture is driven in process with staged inputs (the committed recipe `remote-gesture`); for the
  radial-centre rows the ring's scale is written to one when the game's own frames have not opened it
  (`ringScaleForced` in the recipe's answer says which happened).
- One session cannot disprove a rare race; every verdict above is a reading from this run against the
  deployed artifact.

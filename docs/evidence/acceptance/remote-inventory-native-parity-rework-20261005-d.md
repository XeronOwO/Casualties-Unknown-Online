# Acceptance record — Remote inventory operations: run the native path end to end

- Ticket: `remote-inventory-native-parity-rework` — verdict: **stays in `review/`** — rows 3, 7, 9, 13 and
  14 gained their verdicts in this batch; rows 4 and 8 remain `unproven`
- Batch: `20261005-d` — tickets `container-move-snapshot-only-sync`,
  `remote-inventory-native-parity-rework`
- Commit: `77139b17` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+77139b1704d90977d25aa60ec3af2b35b48d9963` (`deploy.ps1` and `verify-deploy.ps1` exit 0)
- Run: 2026-10-05 21:28 → 21:50 (+08:00) · Host: physical machine (evaluator `18590`, PID 43024,
  `76561198281246659`) · Guest: primary sandbox `Steam1` (evaluator `18591`, PID 48084,
  `76561198863287957`) · Third peer: alternate sandbox `Steam2` (evaluator `18592`, PID 20612,
  `76561199526807662`)
- Dependencies: preflight `11 present`, exit `0`; machine gate `active=cuo`, `game-running=false`,
  `swap-needed=false`, `launch=ok`; no plugin shadow in either sandbox
- Artifacts: the batch directory under `acceptance-artifacts-dir`, cited by name below

## Row verdicts (only the rows this batch judged)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 3 | Drag a carried item of the owner into his trash bag (remote) | machine | **pass** | Guest released the host's dogfood onto the bag's button: owner `item dogfood entered container 1207206791107 through the native guard` + `replayed native MoveIntoContainer` (`marks-cm1.txt`); the viewer's own render carries it nested under `trashbag(Clone)` (`cm29-guest-list.json`) |
| 7 | Use / wear / combine / battery load-unload / favourite | machine | **pass** for the use and wear halves (see Limits) | Radial CENTRE drives, the mechanism batch `20261005-c` could not stage: use — guest `UseItem captured for item 1215796725699` → owner `[ItemUsed] bread (id 1215796725699) — host fact broadcast.` + `replayed native UseItem` (`cm31`, `marks-cm6.txt`); wear — guest `WearItem captured for item 1220091692995` → owner `[SlotMoved] autopump (id 1220091692995) → slot -3 (Wear) — host fact broadcast.` + `replayed native WearItem` (`cm33`, `marks-cm7.txt`) |
| 9 | A gesture the native rules refuse | machine | **pass** (the container-guard shape) | Guest released the host's lantern onto the owner's bag: owner `[RemoteIntent] move refused by the native container guard: item lantern did not enter container 1207206791107 (weight, tag restriction or distance).` — a named refusal, not a silent no-op (`marks-cm2.txt`) |
| 13 | Owner's item at 75 % condition seen by the viewer | machine + visual | **pass** | The owner's soup was written to `condition = 0.75` through the game's own field (`host-set-condition.json`, `before 1 → after 0.75`); the viewer's own proxy read then returned `{"type":"soup","id":1224386660291,"owner":76561198281246659,"condition":0.75}` (`guest-proxy-conditions2.json`) |
| 14 | Water bottle, dog food, lantern, metal scrap into the remote trash bag | machine | **pass** | Three of the four entered through the native guard (`[ContainerLoad] waterbottle …`, `dogfood …`, `scrapmetal … moved inside body container trashbag`, each with its `replayed native MoveIntoContainer`) and all three render nested under `trashbag(Clone)` (`cm29-guest-list.json`); the lantern is the native REFUSAL half (row 9's line) — the row asks for the native allow/refuse outcome per item, and every item produced one |
| 4 | Take that item back out | machine | **unproven** | Not driven: the take-out needs the container window's own drag-out and this session did not stage it. `20261005-c` judged this row pass on the empty-space release; no weaker check was substituted |
| 8 | A held remote item used from the medical panel with the backpack closed | machine | **unproven** | Not driven: the row needs a limb fixture the person's own panel can treat plus the wound-view staging; the session did not build it |

Rows 1, 2, 5, 6, 10, 11, 12 and 15 keep the verdicts `20261005-c` recorded; this batch re-read rows 3, 4
and 14 beside the carrier fix's own evidence, as that ticket requires.

## What the run added to the reusable set

The radial-centre drive the previous batch named as its gap now exists in the committed recipe
`tools/acceptance/recipes/remote-gesture.cs`: `mode=probe` reports the guard's own inputs
(`inventoryUseLeniency` 0.95, `uiScale` 0.667, the centre circle's tag `RadialCenter` and radius 135),
`mode=hover` stages the drag and returns so the game's own frames open the ring, and `mode=release
cast=-2` casts the centre target. The guard is the game's own; the run only stages the scene the pointer
cannot be moved into. Both centre releases this run drove are recorded with the recipe's own
`ringScaleForced` field: the use needed the scale written to one (`0.01 → 1`), the wear did not
(`ringScaleForced=false` — the game's frames had opened the ring).

## Limits

- Row 7's `combine` and `battery load/unload` halves were not driven (they need a combinable pair and a
  battery-slot item staged first), and the `favourite` half is out of reach for this driver:
  `HandleWhileDragging` reads `Input.GetKeyDown(KeyBinds.GetBind("favourite"))`, and the run may not take
  over keyboard input.
- The radial-centre no-op shape (an item that is neither wearable nor usable) produced **no log line**:
  `RemoteDragIntentDispatcher.Emit` logs only `outcome.IsUnclassified`, so a classified no-op is
  observable in the trace but not in the log. Row 9 passed on the container-guard shape instead; this
  silence is recorded as an observation, not judged.
- The three bodies were placed at world (0, 445) to satisfy the operator's own line-of-sight gate (before
  the move, a remote intent was refused with `76561198863287957 cannot see 76561198281246659 on this
  client`). World coordinates and block cells differ on this machine: the drop site reads cell (510, 941).
- The `advance` of an item's condition rides the 1 Hz snapshot by design (the monitor deliberately does
  not compare condition), so row 13 was read one cycle after the write.
- One session cannot disprove a rare race; every verdict above is a reading from this run against the
  deployed artifact.

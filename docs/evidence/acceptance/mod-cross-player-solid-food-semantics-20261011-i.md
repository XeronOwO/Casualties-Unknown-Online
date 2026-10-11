# Acceptance record — Cross-player solid food from the game's own data

- Ticket: `mod-cross-player-solid-food-semantics` — verdict: **moved to `docs/backlog/done/`**. Every row
  passes; the one row batch `20261008-a` left `unproven` (row 5's visual half) is judged here on the
  machine read, and **the ticket now carries that re-wording with the measurements that force it** (see
  row 5 and the Limits).
- Batch: `20261011-i` — tickets: `mod-cross-player-solid-food-semantics` (the batch's only ticket; scope
  page `docs/evidence/acceptance/20261011-i-scope.md`)
- Commit under test: `46056270` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+46056270de5cdb0edd4859b09b9858a6bae5f9fd` (`tools/verify-deploy.ps1`: "Deployment matches this
  tree's build output", exit 0, captured as `verify-deploy.txt` before any client started)
- Run: 2026-10-11 20:33 → 20:50 (+08:00) · one world · Host: physical machine (through Steam) · Guest:
  primary sandbox (its own Steam resident; the guest carries no `plugins` tree of its own, so it reads the
  physical deployment — no shadow to clear)
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt` (present, unused),
  `hotrepl`, `capture`, `input`, `logs`, `artifacts` — preflight **11 present, exit 0** (`preflight.txt`);
  `session-environment.ps1 -Mode status` read `active=cuo`, `game-running=false`, `swap-needed=false`,
  `launch=ok` (`session-environment-status.txt`)
- Artifacts: every file named below lives in the directory `acceptance-artifacts-dir` names, under
  `20261011-i/`; the session's log windows are read by byte offset against `m1-session-start.txt`

The run is read through the same probe channel the committed driver uses; the probes it wrote are the
ad-hoc templates `t-*.cs` plus six this batch added (`y-view`, `y-item-find`, `y-item-move`,
`y-project`, `y-standing-force`, `y-draw-at`), each written to the batch directory with its expanded
source (`x-y-*.cs`). `y-draw-at` is promoted into `tools/acceptance/recipes/draw-at.cs` as this batch's
reusable half.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Per-session standing-object count and the shape check (in `Item.allItems`, category query answers "carried row") | machine | pass | the host materialized **7** objects for the guest's 7 recursive rows; every one reads `inItemList:true`, `classifierIs:"true"`, `worldRow:false`, `parent:"CUO Standing Items"` (`c1-host-standing.json`), and the materializer's own line reads `[StandingItem] <guest>: 7 carried row(s) in the data, 7 standing object(s) alive.` (host 20:46:36); the guest materialized 3 for the host's 4 rows, the fourth being the owner's own id-less starting row by design (`c3-guest-standing.json`, `c4-guest-data.json`) |
| 2 | Per-frame cost against the run's own baseline | machine | pass (limits) | the SAME client: with the set `frameCount 31534, smoothDelta 0.0392, 25.5 fps, standing 7, sceneItems 282, bodies 2` (`c5-host-frame-with.json`); after the owner left the world `frameCount 43425, smoothDelta 0.0345, 29 fps, standing 0, sceneItems 271, bodies 1` (`p3-host-frame-without.json`). By construction each object pays one `Item.Update` and returns early at the parked spot (row 11's chunk-renderer guard); the two samples also differ in member and item count, which is what took the set away (Limits) |
| 3 | The standing set matches the synchronized carried rows **recursively** | machine | pass | the data side of the same pass: owner `<guest>` reads `topLevel 6, recursive 7`, the nested `geigercounter` (id 13787924117) recorded under its data parent `trashbag` (`c2-host-data.json`) — and 7 objects stand; the guest's side reads `topLevel 4, recursive 4` with the `emergencylight` row at id `0` (`c4-guest-data.json`) — and 3 objects stand |
| 4 | Same world item count, no duplicate objects | machine | pass | every standing object answers the data's own world-row query `false` while its id is the owner's carried row (`c1-host-standing.json`, `c3-guest-standing.json`); the materializer's id→object record holds exactly one incarnation per id (`c2-host-data.json` → `objects[]`, 7 entries); the same reads carry the world item counts (host 282, guest 278) |
| 5 | Presentation: nothing visible at the parked spot, no dust or collision sound, no item-side audio, no hover tooltip, no alt-hover label | visual + machine (the visual half is judged on the machine read here; the scope page classified it `visual` + `machine` and the re-wording is recorded in the ticket) | **pass — visual half re-worded onto the machine read, and the ticket records it** | machine: on the host all seven standing objects read `renderers{enabled:0}` (six of them `count:1`; the one exception is the guest's `torch` at `count:2`, `c3-guest-standing.json`), `colliders{count:1,enabled:0,triggers:0}`, and every sound-playing component disabled (`GeigerCounterAudio:"false"`, `WatchScript:"false"`, `CustomItemBehaviour:"false"`, `c1-host-standing.json`, `k5-host-standing-condition.json`); the counter-shaped objects do carry an ENABLED `AudioSource` (`audio{count:1,enabled:1}`) in every census, and nothing drives it — the component that would is the disabled `GeigerCounterAudio`. The census is taken ~6.7 minutes after creation, so a `Start`-built renderer would show up as a larger count. Visual: this run DID put the camera on the parked spot with an awake, unharmed body (vitals read at the capture: `consciousness 96.21`, `brainHealth 100`, `e6-host-vitals-before-capture.json`; camera `(0.16, −17.85)`, view `x −39.8…40.2, y −40.3…4.7` with the spot at `(0,0)` inside it — `f7-host-view-before-B.json` / `f8-host-view-after-B.json`, the pair bracketing the `w3` capture; the capture-time pair is `e5`/`e7`), captured `w1`/`w2`/`w3`/`w5`/`w6`, and **measured why a frame cannot carry the row at that spot**: the spot is inside solid rock at the map corner — batch `20261008-a`'s record and the ticket's status note carry that finding, and this run's own `p1-host-terrain-origin.json` reads the columns x=0…6 solid at the top of its scan (`surfaceY` 29–30, block 14) in a 1024×1024 world — and the terrain tilemap covering that point draws at sorting order **1000** (four `Chunk` `TilemapRenderer`s whose bounds contain `(0,0)` are among `h3-host-draw-at-origin.json`'s 106 enabled renderers there), while the standing objects' own sprites carry order **0** (`"sorting":"Default/0"` per object, `g1-host-force-on.json` / `g3-host-force-off.json`; `h3` itself reports only ENABLED renderers and the standing subtrees are disabled, so it cannot carry that half) — nothing unheld is drawn over the rock whatever its own renderer says. Two further measurements kill the A/B form here: two captures back to back with no state change differ over **284,218 of 637,332 pixels** (`n1-noise-a.png`, `n2-noise-b.png`), and the world area's own mean brightness (the frame below the HUD band) moved from **4.5 to 124.2** across the session (`w1` → `w5`). The two controls tried came out as follows: the real item moved onto the spot differs over **279,603** pixels (`w2` vs `w3`), below the no-change floor; all seven renderers plus both lights forced on differ over **297,052** (`w5` vs `w6`), 4.5% above that floor and spread over the whole frame (`w7-diff-forced-vs-restored.png` shows no concentration at the spot) — the world's own change rather than the objects'. The row's own intent ("the check that the disabled switches really took") is what the machine read answers; the closing this row takes was named by batch `20261008-a`'s record, and this batch wrote it into the ticket |
| 6 | No light and no watch chatter from a parked object | machine | pass | the two light-carrying objects read `lights{count:1,enabled:0}` with `LightItem:"false"`; `WatchScript:"false"` on the watch and `GeigerCounterAudio:"false"` on the counter (`k5-host-standing-condition.json`); the counter's `AudioSource` is enabled while the component that would play it is not (row 5), and no talker, watch or sound line names a standing object anywhere in either client's window from `m1-session-start.txt` |
| 7 | Fluid table at the parked spot unchanged (damaged bottle) | machine | pass | the owner's bottle is damaged first (condition 1 → 0.50, `j1-guest-bottle-write.json`) and stands at the parked spot, where cell `(0,0)` is seeded to liquid 1 (`j6-host-fluid-seed.json`, read back `j7-host-fluid-after-seed.json`: the cell and three neighbours at 1); the re-read five seconds later is cell-for-cell identical (`k3-host-fluid-origin-after.json`), and the control neighbourhood `(8,8)` was read once before the window and once after, all zero (`j3-host-fluid-control.json`). `WaterContainerItem` is disabled on the parked bottle (`k5`) and acts per frame, so the window holds ~300 frames of opportunity. The map-edge half of this row is the world corner the spot sits in, carried by batch `20261008-a`'s record rather than re-measured here |
| 8 | No entity and no explosion appears at the parked spot | machine | pass | the census within 6 units of the spot, before and after the same window: `CrystalEnemy 0`, `Enemy` type absent, `BearTrap 0`, `BuildingEntity` present with `0` within, `FreshItemDrop 0`, `nonStandingItems 0` (`j4-host-nearby-origin.json`, `k4-host-nearby-after.json`). The fixture carries no self-destructing item on purpose, so batch `20261008-a`'s own entity spawn (its `exposedcore`) is not reproduced |
| 9 | A parked object's condition tracks the data rather than a locally pinned 0 | machine | pass | the owner's forced bottle write reaches the peer's copy (1 → 0.50: `j1-guest-bottle-write.json`, `k5`); the owner's bread write reaches it too (0.9784 → 0.40 written on the owner's client, the copy reads **0.3992** after the next snapshot: `j11-guest-bread-write.json`, `k5`) |
| 10 | The prefab facts: collider layer, trigger or solid | machine | pass | every standing object reads `itemLayer:"Item"`, `colliders{count:1,enabled:0,triggers:0,layers:"Item"}`, `rb{bodyType:"Dynamic",simulated:false,gravityScale:1}` (`k5-host-standing-condition.json`) — an item prefab's own collider is a solid collider on the `Item` layer, and the recipe's sweep disabled it |
| 11 | Whether local decay reaches the zero-condition destroy between two data refreshes | machine | pass (observed) | the copy forced to condition 0 stays active, in `Item.allItems`, where it stands (`j10-host-standing-bread-zero.json`), and the next snapshot restores the owner's value (`k5`: `bread … condition 0.3992`) — `Item.Update`'s decay and destroy both sit behind the parked spot's chunk-renderer guard, so the ticket's predicted blink is not reachable while parked |
| 12 | Unity's behaviour for a `simulated = false` body whose velocity is written | machine | pass (observed) | the game's own explosion at (3,3) with radius 5 reports all seven objects at `(0,0)`, `simulated:false`, `vx/vy 0` and `threw:""` afterwards (`j5-host-explosion.json`; the probe reads the post state only, so the before side is the census and the id-addressed reads, `k5`, `j9`); a velocity write of `(12, 7)` on a standing body is accepted and read back unchanged with the object still at `(0,0)` (`j8-host-velocity-write.json`, `j9-host-find-after-velocity.json`) — that pair is what answers the row |
| 13 | Per-session `[ERR][Unity:Exception]` count from every peer's log | machine | pass | final counts, read after both clients exited (0 game processes left): host **8,105** lines, `[ERR]` **0**, `Unity:Exception` **0**, `[WRN]` 18; guest **14,267** lines, `[ERR]` **0**, `Unity:Exception` **0**, `[WRN]` 42. The warnings are named families — host: `SteamTransport` send-recovery 8, `CloneFactTable` 7, `HandshakeHandler` 2, `TrapEffectApplier` 1; guest: `WorldEventSync` 16, `TrapVisualReplay` 10, `EnemySyncCoordinator` 9, `RemotePlayerRenderer` 4 (the already-accepted bounded clone-failure family, carrying its own suppression counter: `— 7863 identical line(s) suppressed`), `KernelProtocolService` 2, `CloneFactTable` 1 |
| 14 | The eater's own body moves by the food's own amounts (clamp at 125, rolls, talker reactions) | machine | pass (rolls unobserved) | host→guest: hunger 67.44 → 75.85, thirst 65.57 → 66.75, `weightOffset` −11.146 → −10.742 (`m3-guest-vitals-before.json`, `m9-guest-vitals-after-eat.json`); guest→host: hunger 68.84 → 77.66, thirst 52.47 → 54.23, `weightOffset` −7.324 → −6.850 (`n1-host-vitals-before.json`, `n5-host-vitals-after.json`) — bread's own delegate is `body.Drink(2f); body.Eat(9f, 0.5f)`; the clamp: hunger forced to 124 → the bite reads **124.93** with `sicknessAmount` 0 → **9.41** (the overflow rule) and the same thirst/weight moves (`o2-guest-vitals-before-clamp.json`, `o5-guest-vitals-after-clamp.json`). Neither random roll (20% vomit, 10% burp) fired and the talker reaction was not read (Limits) |
| 15 | The item's condition follows the bite on its owner's item | machine | pass | the host's bread 0.999 → 0.6555 on the first bite and 0.66 → 0.3139 on the clamp bite, each logged on the owner's own client (`[ItemUse] <guest> ate bread (id 1134192347075) …; condition 0.66` at 20:49:08 and `… condition 0.31` at 20:49:34, host log); the guest's bread 0.40 → 0.0598 with `[ItemUse] local item 18082891413 condition set to 0.06 by cross-player use.` (guest log 20:49:23). The chain's own steps are in the same windows: `[Visibility] clear …`, `offers bread … — the eat runs on the eater's own client`, and `ran the game's own eat of bread (id …) on the local body` |

## Residuals for the user

- **Row 5's re-wording itself** (one line, no work asked of the machine): the row was declared `visual +
  machine`, and this batch judges its frame half on the machine read because the parked spot is inside rock
  under the terrain tilemap — a design constant, so if you want the frame half kept as a check, the report
  to reject is `docs/evidence/acceptance/mod-cross-player-solid-food-semantics-20261011-i.md` row 5 and the
  ticket's *Row 5's visual half, re-worded*; rejecting it returns the ticket to `todo/` with the same
  marking as a failed row. Nothing else in this batch needs a person.
- **The two rows that could have needed an eye or an ear are answered or named**, not left: the visual half
  above, and row 14's two random rolls (Limits).

## Limits

- **Row 2 is not a strict A/B.** The sample "without the set" is also the sample without the second member
  (the owner's departure is what retired the set), so it differs in member count and item count as well;
  the run's own three samples (host 25.5 fps with the set, host 29 fps and 271 items without, guest 44.4 fps in the same pass) sit inside that spread, and by construction each object pays one `Item.Update`.
- **The visual half of row 5 is re-worded, and this is the measurement that forces it.** Three things had
  to hold for a frame to carry the row, and the third does not hold at this design's parked spot: (a) the
  spot must be in view — it is (`y-view`: camera `(0.16, −17.85)`, view `y −40.3…4.7`, spot at `(0,0)`);
  (b) the player must be awake and unharmed — it was (`e6`: `consciousness 96.21`, `brainHealth 100`,
  where batch `20261008-a`'s frame was taken unconscious); (c) something drawn at the spot must be able to
  appear in a frame at all — it cannot, because the spot is inside solid rock and the terrain tilemap
  draws at order 1000 over the order-0 sprite an unheld item carries there (`h3`). Two controls were tried
  against that and both are inside the run's own frame noise: a real item moved onto the spot, and all
  seven renderers plus both lights forced on. The ticket now says the row is judged on the machine read,
  and says why; the residual risk it names (a component that builds its own renderer inside `Start`) is
  covered by the census being read long after creation.
- **The frame noise measurement is this environment's, not the product's.** Two window captures taken back
  to back with no state change differ over 284,218 of 637,332 pixels (whole frame, a pixel counted when
  `|ΔR|+|ΔG|+|ΔB| > 12`), and the world area's mean brightness — the frame below the HUD band — moved from
  4.5 to 124.2 across the session (the host was freezing in an ice biome under a live weather effect: the
  `draw-at` probe answers at that point with 34 particle renderers, `Rain` and `Fog`). A future visual row
  at a world position should budget for that: one frame read against a machine read, not an A/B.
- **The two random rolls of the eat were not observed** (the 20% vomit and 10% burp rolls did not fire in
  either bite, and the meal-end roll was not armed): the clamp's deterministic consequence (overflow →
  `sicknessAmount`) was exercised instead, as in batch `20261008-a`.
- **An unattributed world change, recorded as an observation.** At 20:49:08 the GUEST's waterbottle left
  its inventory through the game's own drop path (`[ItemTrace] op=30 … origin=OnItemDropped`, guest
  20:49:08.151), the host's materializer retired the standing incarnation in the same second
  (`retired waterbottle (id 22377858709 …): the data no longer carries the row.`) and the ordinary world
  path materialized the row at `(3.1,503.8)` — i.e. the CARRIED→WORLD transition the ticket describes,
  walked end to end. **Why the guest's own body dropped the bottle is not attributed by this run**: no
  fixture call drove it, the drop lands in the same second as the first eat, and nothing else the run read
  explains it. It is not a row of this ticket; the four lines above are the evidence a future ticket would
  start from.
- **The fixture's own forced writes show up as `[CharSync] divergence` warnings** (`a new carried item the
  fact table never saw` for the created items; `left the inventory without an event sync` for the bottle
  above). They are the declared setup's own trace, and they are inside the warning families row 13 lists.
- **The drag gesture's pointer half was not re-driven.** The pointer is never moved by the tooling (the
  standing boundary), and batch `20261008-a` measured that the game's camera sits below the body so the
  pointer's world point is always below the feeder's feet, where the feeder's own floor blocks the
  product's linecast. Rows 14 and 15 were driven through the production entry the gesture calls
  (`IPlayerInteractionControl.SendUseRequest`), with the target standing on the feeder (0.08 world units
  apart, `m5-host-align.json`, `n4-guest-align.json`), so the product's own visibility gate
  (`[Visibility] clear …`), family verdict, admission, the affected side's eat, the outcome report and the
  commit all still decided. The gesture's own overlap test is therefore not re-evidenced by this run.
- **The third client was not launched** (this ticket has no third-party row); every reading is from the
  two participants' own clients, and the alternate sandbox's endpoint stayed idle.
- **The scope page's control object was staged differently from its own plan, and this is that record.**
  §1.3 of `docs/evidence/acceptance/20261011-i-scope.md` promised "a control object … created at a declared
  position a few units from the parked spot". What the run did instead: the host's own `flashlight` was
  dropped at `(0.233, −17.887)` — the body's own resting place, ~17.9 units below the spot — and its
  `torch` was later **moved onto** the spot itself (`f1-host-move-torch-origin.json`, read back
  `f2-host-find-torch-at-origin.json` at `(0, 0)` then `(0, −0.79)`), then destroyed for the A/B. Neither
  placement changes the row's outcome (row 5's occlusion argument does not depend on the control), and both
  are declared writes, but the deviation from the declared setup belongs in the record rather than in a
  reader's inference.
- **A capability finding this batch did not act on: no mod in this tree exercises the mod API.**
  `docs/backlog/done/mod-authored-effects.md` and `docs/backlog/done/mod-item-authored-actions.md` both
  need a live session for their end-to-end rows, and both clients were up and running the deployed surface —
  but the sample mod declares no item content beyond the dressing, and the dressing declares neither `Usable`
  nor `UsableOnLimb`, so placing the built DLL would have exercised the registration rails and nothing else.
  They are therefore not judged here, and the gap (the dependency table has no "a mod that exercises the
  API" row) is recorded in `docs/acceptance/lessons.md` as this batch's own finding.
- **Probe-promotion half.** One probe was promoted (`tools/acceptance/recipes/draw-at.cs`); it differs from
  the batch's own template in two ways an independent review asked for — its `chunkRenderer` value is quoted
  JSON, and a `chunkLookup` field says which way that call answered (`renderer`, `returned-null`,
  `nonrenderer:<type>`, `no-method`, `no-world`, `threw:<type>`) so a `none` is never ambiguous. The rest of
  this batch's templates (`y-view`, `y-item-find`, `y-item-move`, `y-project`, `y-standing-force`) stay in
  the batch directory with their expanded sources, named here for the next batch that judges a visual row
  at a world position.

## What the run proved about the ticket's own claims

- The object exists on the client that does not hold the item, it is in the game's own item list, its
  category answers from the data rather than the parent chain, and the set is recursive (rows 1, 3, 4).
- The parked objects are inert towards the world: no light, no sound, no collision, no pickup, no fluid
  drain, no entity or explosion at the spot, and Unity accepts a velocity write on them without moving
  them (rows 5–8, 12).
- The data, not the object, owns the state: the copy follows the owner's condition both ways and a locally
  forced value is restored by the next snapshot, with no zero-condition blink while parked (rows 9, 11).
- The eat is the game's own code on the eater's client and its item-side half lands on the owner's real
  item, in both directions, including the clamp (rows 14, 15).
- One transition the ticket names as step 2's own call was observed live: a carried row that becomes a
  world row retires its standing incarnation and materializes the ordinary world item (Limits).

# Acceptance record — Cross-player solid food from the game's own data

- Ticket: `mod-cross-player-solid-food-semantics` — verdict: **moved back to `docs/backlog/todo/`**
  (row 5's visual half is `unproven`: the run's only frame was taken while the player was unconscious and
  under attack, so it proves nothing; every other row passes)
- Batch: `20261008-a` — tickets: `mod-cross-player-solid-food-semantics` (the batch's only ticket; its

  scope page is `docs/evidence/acceptance/20261008-a-scope.md`)
- Commit: `f416a1f2` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+f416a1f26d6c068ccb75c7c2ca93df50ab6b7c92` (`tools/verify-deploy.ps1` exit 0 against that tree)
- Run: 2026-10-08 19:30 → 19:47 (two worlds in one session: the fixture world, then a fresh run after the
  host's body was lost to a declared placement) · Host: physical machine · Guest: sandbox
- Dependencies: `dotnet`, `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt` (present, unused),
  `hotrepl`, `capture`, `input`, `logs`, `artifacts` — preflight 11 present, exit 0
- Artifacts: the probe reads `p*.json`, `c*.json`, `d*.json`, `e*.json`, `f*.json`, their expanded sources
  `x-t-*.cs`, the templates `t-*.cs`, the window frame `w1-host-parked-spot.png` and the driver captures
  `a*.json`/`b1-host-start-run.json`, all in the directory named by `acceptance-artifacts-dir` under
  `20261008-a/`; the session's log excerpts are read by byte-offset mark `m1-before.txt`

The run is read through probes of the same channel the committed driver uses (`t-standing-list`,
`t-standing-data`, `t-align`, `t-frame`, `t-vitals`, `t-fluid`, `t-fluid-set`, `t-nearby`, `t-line`,
`t-terrain`, `t-floor`, `t-place`, `t-zoom`, `t-release`, `t-userequest`, `t-velocity`, `t-explosion`,
`t-items`, `t-body-write`, `t-body-call`), each written to the batch directory as its own file; the
committed recipes `log-level`, `container-read`, `item-provide`, `container-fill`, `remote-gesture`,
`body-read`, `item-write`-equivalent and `body-place` are used where they reach the state.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Per-session standing-object count and the shape check (in `Item.allItems`, category query answers "carried row") | machine | pass | world 1: the host materialized 8 objects for the guest's 8 recursive rows and the guest 5 for the host's 6 (one row has no instance id yet — by design, `CarriedInventoryReporter` binds the host's own supplies lazily): `p1-host-standing.json`, `p3-host-standing.json`, `p1-guest-standing.json`, `p3-guest-standing.json`, `f1-host-standing.json`, `f1-guest-standing.json`; every object carries `inItemList:true`, `classifierIs:"true"`, `marker:true`, `worldRow:false`; the data-side count line `[StandingItem] {owner}: {Wanted} carried row(s) in the data, {Standing} standing object(s) alive.` (host log 19:37:11) |
| 2 | Per-frame cost against the run's own baseline | machine | pass (limits) | the host's own samples: 52.1 fps / `smoothDelta` 0.0192 with 2 standing objects and 271 items, 46.2 fps / 0.0216 with 0 and 267 items, 52.9 fps / 0.0189 with 0 and 267 items — the run's own noise band (46–53 fps) exceeds any effect of the set: `f1-host-frame-with.json`, `f2-host-frame-without.json`, `f3-host-frame-with2.json`. By construction each object pays one `Item.Update` (it is in `Item.allItems`) and returns early at the parked spot (row 11's chunk-renderer guard) |
| 3 | The standing set matches the synchronized carried rows **recursively** | machine | pass | world 1 (host): data `topLevel:6, recursive:8` → 8 objects, the two nested rows (`geigercounter`, `exposedcore`) recorded with their data parent (`ParentId:9492956821` in `p2-host-data.json`'s `objects[]`; the eight objects themselves are listed in `p3-host-standing.json`); world 2: `topLevel:2, recursive:2` → 2 (`f1-host-data.json`, `f1-host-standing.json`), and the guest's side `topLevel:2, recursive:2` → 1 object + one id-less row (`f1-guest-data.json`, `f1-guest-standing.json`) |
| 4 | Same world item count, no duplicate objects | machine | pass | every standing object answers the data's own world-row query `false` while the object's id is the owner's carried row (`worldRow:false` in `p3-host-standing.json`, `p1-guest-standing.json`, `f1-*.json`); the materializer's id→object record holds exactly one incarnation per id (`p2-host-data.json` → `objects[]`); the retire line for the excluded case (`retired exposedcore … the data no longer carries the row`, host log 19:37:59) |
| 5 | Presentation: nothing visible, no dust/collision sound, no audio, no hover tooltip, no alt-hover label | visual + machine | **machine half pass; visual half `unproven`** | machine: every renderer in every subtree disabled (`renderers:{count:1,enabled:0}`, `p3-host-standing.json`), every collider disabled (`colliders:{enabled:0}`, `triggers:0`), and every component that PLAYS a sound disabled (`GeigerCounterAudio:"false"`, `WatchScript:"false"`, `CustomItemBehaviour:"false"`), with the items' own `LiquidAffect` and `BatteryItem` left enabled by design; one parked prefab does carry an enabled `AudioSource` (`geigercounter`, `audio:{count:1,enabled:1}` in `p3-host-standing.json`) — the run read the source's `enabled` flag, not its `isPlaying` state, and no sound line names a standing object in the log census; hover: the disabled collider is invisible to the game's own `Physics2D.OverlapPoint` hover probe and the pickup gate refuses a standing object by rule. **The frame `w1-host-parked-spot.png` is withdrawn as evidence**: it was captured while the host was unconscious and being attacked (see Limits), so its darkness is the player's blacked-out vision, not proof about the parked objects |
| 6 | No light and no watch chatter from a parked object | machine | pass | the light-carrying items (`emergencylight`, `flashlight`) read `lights:{count:1,enabled:0}` with `LightItem:"false"`; `WatchScript:"false"` on the watch, `GeigerCounterAudio:"false"` on the counter (`p3-host-standing.json`, `p3-guest-standing.json`); no talker/sound line for a standing object in the log census. The frame is NOT used here: it was taken with an unconscious player, whose vision is dimmed, so it cannot show whether a light was on |
| 7 | Fluid table at the parked spot and at the map edge unchanged (damaged bottle) | machine | pass | the corner is the map's own edge (world 1024×1024, parked spot = cell 0,0): seeded cells (0,0) and (1,0) = 1 and the control (8,8) = 1 (`p6-host-fluid-seed-a/b/c.json`); across the window between the damage write and the after-read (19:34:33 → 19:40:48, 6 min 15 s, with the object itself standing for 8 min 2 s) no cell was zeroed — the game's own fluid simulation had spread the water over 12 neighbour cells, the control still 1 (`p25-host-fluid-after.json`, `p25-host-fluid-control.json`); `WaterContainerItem:"false"` on the parked bottle (`p3-host-standing.json`) |
| 8 | No entity and no explosion appears at the parked spot | machine | pass (single census; the fixture's own spawn named) | the census within 6 units of the parked spot, read once with the whole fixture standing (`p6-host-nearby-before.json`): `CrystalEnemy:0`, `Enemy` type absent, `BearTrap:0`, `FreshItemDrop:0`, `nonStandingItems:0` — nothing at the parked spot. **The same shape did fire elsewhere in this run and the record names it**: the fixture's own `exposedcore` (owner side, the guest) reached condition 0 and its `CustomItemBehaviour` destroyed it and spawned two `crystalenemy` objects at the guest's position (guest log 19:37:58.434: `[EntitySpawn] reporting crystalenemy at (101.1,490.8)`), which CUO mirrored to the host as runtime spawns (host log 19:37:58.446 `reporting crystalenemy at (101.1,490.8)`, 19:37:58.476 `host bound runtime spawn … (prefab crystalenemy)`), after which the two enemies attacked the host repeatedly — 56 `[EnemyAttack] … CrystalLunge` announcements between 19:38:01 and 19:41:23 and the host's pain vocalisations from 19:38:09 — while the parked copy on the peer kept `CustomItemBehaviour:"false"` and merely retired with its data row (host log 19:37:59). The census predates all of it (Limits) |
| 9 | A parked object's condition tracks the data rather than a locally pinned 0 | machine | pass | the owner's forced writes (bread → 0.4, bottle → 0.5) appeared on the peer's copies (`p4-guest-bread-write.json` / `p4-guest-bottle-write.json` → `p5-host-standing.json`: `bread condition=0.4`, `waterbottle condition=0.5`); a copy forced to 0 was restored to the owner's value by the next snapshot (`p26-host-bread-zero.json` → `p27-host-standing-restored.json`: `0.3782`); world 2 reads both directions side by side (`f1-host-standing.json` 0.3173 vs `f1-host-data.json` 0.3169; `f1-guest-standing.json` 0.6511 vs `f1-host-data.json` 0.6507) |
| 10 | The prefab facts: collider layer, trigger or solid | machine | pass | every standing object reads `itemLayer:"Item"`, `colliders:{count:1,enabled:0,triggers:0,layers:"Item"}` and `rb:{bodyType:"Dynamic",simulated:false,gravityScale:1}` (`p3-host-standing.json`, `p3-guest-standing.json`, `f1-*.json`) — an item prefab's own collider is a solid collider on the `Item` layer, and the recipe's sweep disabled it |
| 11 | Whether local decay reaches the zero-condition destroy between two data refreshes | machine | pass (observed) | forced to condition 0 the parked bread survives: it is still active, in `Item.allItems`, at condition 0 (`p26-host-bread-zero.json`, `p26-host-standing-after-zero.json`), because `Item.Update`'s decay and destroy both sit behind the live-chunk guard and the parked spot's chunk renderer is off; the next data refresh restored the owner's value (`p27-host-standing-restored.json`), so the ticket's predicted blink is not reachable while parked |
| 12 | Unity's behaviour for a `simulated = false` body whose velocity is written | machine | pass (observed) | the game's own explosion at the parked spot changes nothing on the parked objects (`p27-host-explosion.json`: positions, velocities and conditions unchanged, `threw:""`) because `WorldGeneration.CreateExplosion`'s `OverlapCircleAll` cannot see a disabled collider; a forced velocity write is accepted by Unity and read back unchanged (`p28-host-velocity-write.json`: `simulated:false`, `wroteVx:12, wroteVy:7, afterVx:12, afterVy:7`), and the body does not move: `x=0, y=0` in `p29-host-velocity-after.json` |
| 13 | Per-session `[ERR][Unity:Exception]` count from every peer's log | machine | pass | final counts, re-read after both clients exited: host 16,998 lines since the mark, `[ERR]` **3**, `Unity:Exception` **0**; guest 30,122 lines, `[ERR]` **0**, `Unity:Exception` **0**. The host's three `[ERR]` lines are the designed cut refusal at the world's end (`[SaveFacts] no live world to read the native run fields from…`, `No cut taken (LayerAdvance)…`, 19:42:08), not a script exception. Warnings: the host's 17 are 14 `[CharSync] divergence` from the fixture's own `Utils.Create`+`PickUpItem` creations plus 2 `[DragUse]`-adjacent `GameAdapter` lines and 1 trap notice; the guest's 25 are 9 `TrapVisualReplay`, 8 `RemotePlayerRenderer` (`Remote body: no Body component …`, with its own suppression counters), 4 `[CharSync]`, 2 `GameAdapter`, 1 `ItemService` and 1 `GuestMenuGuard` |
| 14 | The eater's own body moves by the food's own amounts (clamp at 125, rolls, talker reactions) | machine | pass (rolls unobserved) | guest→host: hunger 86.32 → 95.03, thirst 85.60 → 87.21, `weightOffset` −7.289 → −6.814 (`e1-host-vitals-before/after.json`) — bread's own delegate is `body.Drink(2f); body.Eat(9f, 0.5f)`; the clamp: hunger forced to 124 → 124.99 after the bite with `sicknessAmount` 0 → 9.58 (the overflow ×1.2 rule) and thirst +2 / weight +0.5 (`e2-host-hunger-write.json`, `e2-host-vitals-after.json`); host→guest: hunger 83.03 → 92.01, thirst 75.76 → 77.74, `weightOffset` 5.919 → 6.416 (`e3-*.json`). The two random rolls (20% vomit, 10% burp) did not fire in either bite and the talker reaction was not read — limits below |
| 15 | The item's condition follows the bite on its owner's item | machine | pass | the guest's bread: 0.9979 → 0.658 → 0.3179 across two bites (`e1-guest-bread-before/after.json`, `e2-guest-bread-after.json`), logged on the owner's own client as `[ItemUse] local item 48147662485 condition set to 0.66 by cross-player use.`; the host's bread: 0.991 → 0.6511 (`e3-host-bread-before/after.json`); the eater's side logs the chain's steps (`offers bread … the eat runs on the eater's own client` → `ate bread …; condition 0.65` → `[ItemUsed] bread (id …) is a standing item object — the eat's outcome reported to the host for its owner.` → `ran the game's own eat of bread … on the local body`) |

## Residuals for the user

- None outstanding. The one item this record first put here — the frame `w1-host-parked-spot.png` — is
  withdrawn: the user reported that the character was unconscious during the capture, and the logs confirm
  the host was under attack through that window (56 `CrystalLunge` announcements, 19:38:01 → 19:41:23, and
  its pain vocalisations from 19:38:09). An unconscious player's vision is dark and blurred, so the frame
  says nothing about the parked objects. Nothing is asked of the user for this ticket.

## Limits

- **The feed gesture's pointer half was not driven.** `CrossPlayerDragUse` reads `Input.mousePosition`
  directly, and this game's camera sits ~3.8 world units **below** the body (measured on both clients:
  body 468.615 / camera 464.761 on the guest, body 463.885 / camera 460.209 on the host), so the pointer's
  world point is always below the feeder's feet; with the target inside the product's own 1.5-unit radius
  the feeder's own floor then blocks the product's line-of-sight gate (`[Visibility] blocked … by ground
  at (11.0,466.0)`). Five releases were driven through the game's own `HandleReleaseDragging` (the
  batch's `t-release` probe, with the camera zoomed for the call, and one invocation of the committed
  `remote-gesture` recipe that refused on a missing declared argument); **three of them reached the send**
  — `[DragUse] dropped bread on … (instance …)` at 19:37:05 and 19:42:28 on the host and at 19:44:08 on
  the guest — and every one of those three was then refused by the visibility gate, which is the
  production behaviour; two more fell through to the world path (`[DragFlow] release fell through to the
  WORLD path`, 19:38:13 and 19:43:51) and cost the feeder its item.
  Rows 14 and 15 were therefore driven through the production entry the gesture calls
  (`IPlayerInteractionControl.SendUseRequest(target, item, dose 0)` — `t-userequest.cs`), with the target
  standing beside the feeder so that the product's own line-of-sight gate, family verdict, admission, the
  affected side's eat, the outcome report and the commit all still decided. The gesture's own overlap test
  is evidenced by those three `[DragUse] dropped` lines, and its full path — overlap AND line of sight at
  once — was not reached in this environment.
- Row 5's audio half is a flag read, not a playback read: one parked prefab (`geigercounter`) carries an
  enabled `AudioSource`, the run read `enabled` rather than `isPlaying`, and the component that plays it is
  disabled. No sound line names a standing object, but the claim rests on the component state.
- **Row 5's visual half is `unproven`, and the reason is the run's own state.** The only frame
  (`w1-host-parked-spot.png`) was captured while the host was unconscious and under attack, so its darkness
  is the player's blacked-out vision. The parked spot itself is inside solid rock at the map corner
  (`p25-host-origin-block.json`), so a camera can only be brought there by teleporting a body into rock —
  which is what the capture did, and which is also how the player's state at that moment came to matter. The
  run read the player's body state at other moments (`e1`-`e3` vitals, `body-read`) but **not at capture
  time**; the user's report is what exposed it. Closing this row needs either a capture with an awake,
  unharmed player (the parked spot's own position makes that a design question) or the row re-worded onto
  the machine read (every `Renderer` in the subtree disabled is already the definitive statement that
  nothing is drawn).
- **The fixture changed the run's world, and the record owns it now.** The fixture `exposedcore` I created
  on the guest rots at 0.4165 and destroys itself at condition 0; the owner-side `CustomItemBehaviour` then
  spawned two crystal enemies at the guest's position (19:37:58), CUO mirrored them to the host as runtime
  spawns, and they attacked the host 56 times over the next three and a half minutes (19:38:01 → 19:41:23),
  knocking it out; the host's pain/`death1`/`death4` vocalisations were relayed from 19:38:09. The parked
  copies were never involved (their `CustomItemBehaviour` is disabled and the peer's copy retired when the
  data dropped the row), but the run's world was no longer the quiet fixture the later readings assumed: the
  host was unconscious for the rest of world 1 and through part of world 2 (where a declared placement's
  fall did further damage). Each eat reading is still bracketed by its own before/after pair, which is why
  its deltas hold; a fixture item with a self-destruct or world-spawning behaviour should either be left out
  or its consequences declared and watched in the census.
- Row 8's census was read once with the whole fixture standing (`p6-host-nearby-before.json`), not as the
  scope page's before/after pair, and it predates the crystal spawn by thirteen minutes — the absence claim
  is about the PARKED SPOT only, and the run's own entity spawn elsewhere is named above.
- The random consequences of the eat (the 20% vomit and the 10% burp roll) did not fire in either bite and
  the meal-end roll was not armed through `Body.Burp` afterwards (the clients had already been asked to
  quit): the clamp's deterministic consequence (overflow → `sicknessAmount`) was exercised instead.
- The per-frame row compares one client's own samples across a membership change (the guest left the
  world), not a strict A/B: the guest's re-entry was refused in this session (`continue-run` answered
  `ok:false` after a `leave-world`), so the "with set" sample and the "without set" sample differ in member
  count as well as object count. The run's own noise band exceeds the effect.
- The two worlds of this session are separate runs of one batch: row 7's fluid window, rows 1–12's counts
  and the retire readings come from the first world (19:30 → 19:42), rows 13–15 and the second half of rows
  1–4 and 9 come from the fresh run (19:42 → 19:47). The record names which world each pointer came from.
- The third client was not launched (the ticket has no third-party row); every reading is from the two
  participants' own clients.
- The batch's probes were not promoted into `tools/acceptance/recipes/` although the scope page promised
  it: they stay in the batch's own artifact directory under the names listed at the top of this record, and
  the two that are general enough to reuse (`t-standing-list` and `t-standing-data`) are named for the next
  batch that touches this family. The one committed tool this run did change is
  `tools/acceptance/recipes/body-place.cs`.
- **Tool finding, fixed in this cycle**: the committed recipe `tools/acceptance/recipes/body-place.cs`
  failed on the live evaluator whenever its `x`/`y` arguments carried decimals (`eval 'recipe:body-place'
  failed: (1,1): InteractiveHost`) and worked with integers. The cause is in the recipe: `var x =
  {{n:x}};` makes a `double` for a fractional argument and `new Vector3(x, y, z)` then has no implicit
  conversion, while an integer argument stays an `int`. The recipe now casts the placeholders
  (`var x = (float)({{n:x}});`), which is the same shape every other reader uses.

## What the run proved about the ticket's own claims

- The object exists on the client that does not hold the item, it is in the game's own item list, and its
  category answers from the data, not the parent chain (rows 1, 4).
- The materialization is recursive and carries the data's parent links (row 3).
- The parked objects are inert towards the world: no light, no sound, no collision, no pickup, no fluid
  drain, and Unity accepts a velocity write on them without moving them (rows 5–8, 12). The one entity
  spawn this run produced came from the fixture's OWNER-side item, not from a parked copy, and it is named
  in row 8 and in the Limits.
- The data, not the object, owns the state: condition follows the owner's value both ways, and a locally
  forced value is restored (row 9).
- The eat is the game's own code on the eater's client, and its item-side half lands on the owner's real
  item, in both directions (rows 14, 15).

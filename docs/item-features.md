# Item Features — Casualties Unknown (Demo)

The inventory of every item trait that carries state, consumes random numbers,
or affects multiplayer sync — the map for item-sync debugging and testing.
Each feature section states the mechanic, its decompiled implementation
(`reversing/` source, gitignored), its state fields, its randomness, and its
CUO sync status (with `src/` references).

The full item × feature matrix lives in
[`item-features-matrix.csv`](item-features-matrix.csv) — one row per item
(~190), maintained by `tools/item-features.ps1` (see Usage below). Read the
matrix with `list`/`get`, never by hand: a misaligned row is detected by
`validate` before any output is trusted.

## Matrix tool usage

```
tools/item-features.ps1 validate                        # column count per row, item uniqueness
tools/item-features.ps1 list                            # all data rows, tab-separated (machine readable)
tools/item-features.ps1 get <item> [feature]            # one item's row / one cell
tools/item-features.ps1 set <item> <feature> <value>    # edit one cell
tools/item-features.ps1 add-item <item> [feature=value ...]
tools/item-features.ps1 remove-item <item>
tools/item-features.ps1 add-feature <feature>           # add a column (every row gains a cell)
```

- The CSV is UTF-8 without BOM (git-clean; Excel opens it garbled on some
  locales — import via Data → From Text if needed). A cell may not contain a
  comma (quote it instead) — the script round-trips quotes via `Import-Csv`.
- Every read validates first; every write validates after — a misaligned row
  aborts with exit 1, never silently.
- `validate -Doc docs/item-features.md` additionally warns (not fails) when a
  matrix feature column has no matching `###` section below.

## Features

### battery — battery-powered items

Items with a battery compartment (`decayInfo & 16`: flashlight, headlamp,
lightbulb, watch, geigercounter, autozoomgoggles, gravbag — Item.cs:3988,
4322, 5544, 5596, 6093, 6164, 1993). **Charge = `Item.condition`**
(BatteryItem.cs:12-18 `hasCharge => condition > 0`; `GetCharge` =
`condition * maxCharge`, BatteryItem.cs:140-147; drain:
`DrainCharge(change)` → `condition -= change / (maxCharge * 0.01)`,
BatteryItem.cs:130-137).

Consumption sources:

1. **Decay system — "on only"** (`Item.HandleDecay`, Item.cs:183-208): drains
   via `DrainCharge(rotSpeed * 0.01 * presetMult * num * dt)` at
   `decayMultiplier * (isWet ? 6 : 1) * globalDecayRate`. `decayMultiplier` is
   written by the mode state: flashlight state 1 → 1, states 2/3 → 0.5, 0 → 0
   (CustomItemBehaviour.cs:557); emergencylight state 1 → 1; geigercounter
   active → 1 (GeigerCounterAudio.cs:17 / Item.cs:5602). **Off = no decay.**
   Gravbag is `decayInfo & 17` (16+1: only when empty, Item.cs:1993).
2. **Fixed per-use drains** (useAction `battery.DrainCharge`): minilaserdrill
   0.001428 (Item.cs:4608), plasmacutter 0.001 (4648, 4655), heavydrill 0.00238
   (4698), terrainscanner 0.02 (5580), epda 0.126 (5706), rangefinder 0.0025
   (5736), mp3player 0.005 (5792), grapplinghook 0.02 (GrapplingHook.cs:39),
   aed judgement (AEDMinigame 0.01/0.02/0.14: 53, 106, 139).
3. **Continuous**: AutoPump `dt/1200` + 0.002 per pump (AutoPump.cs:37, 41),
   ManualDefibMinigame `dt/800` + `charge/4000` (112, 135), CrystalEMP 100
   (CrystalEMP.cs:25).
4. **Charging**: hand-crank minigame `DrainCharge(-num * 3.3E-05f)` (negative
   drain, HandCrankMinigame.cs:76); recharger ramps `condition` toward 1
   (BatteryRecharger.cs:89).

Load/unload: `LoadBattery` transfers `battery.condition` into the host item
and destroys the battery item (BatteryItem.cs:85-106); `UnloadBattery` spawns
the battery back (109-127). Battery items themselves (smallbattery/
mediumbattery/largebattery, capacities 50/100/300, Item.cs:4231-4269) carry
their charge in `condition`.

Randomness: none.

**CUO sync: charge (`condition`) is synced on every path.** The
`decayMultiplier` driving the drain is derived from mode state — before #89 it
desynced the guest's decay rate, but `condition` always followed the host's
mirror.

### liquid — liquid containers

Volume lives in `WaterContainerItem.stack` (`List<LiquidStack>`,
WaterContainerItem.cs:347, `[Saveable]`), mirrored to `item.condition` by
`UpdateCondition` (WaterContainerItem.cs:264-267). Drink/inject drains the
stack (Item.cs useAction, 100 per drink; useLimbAction, per-drug amounts).
`destroyAtZeroCondition` items despawn at empty.

Randomness: none (contents are prefab `defaultContents`).

**CUO sync: `stack` round-trips as `Liquids`** (`CaptureLiquids`/
`RestoreLiquids`, ItemStateCodec.cs:105-118 / 251-265) on every path.
Restore rebuilds the stack directly — the prefab Awake already filled the
defaults, additive restore would read "full" again (ItemStateCodec.cs:259-263).

### consumable — eat/drink/inject

`condition` (or liquid stack) decrements per use; `destroyAtZeroCondition`
despawns at zero. The full per-item decrements are the Item.cs useAction/
useLimbAction tables (Item.cs:284-7086 — e.g. rag 0.05, bread 0.34, steak
0.334; injectables ApplyToLimb via WaterContainerItem). State = `condition` /
`stack` only — no components.

Randomness: see [randomaction](#randomaction—random-behavior-out-of-sync-by-design).

**CUO sync: `condition`/`stack` synced; the use itself is a guest-local fact**
— use reports are adopted unconditionally by the host
(`CheckUseEvidence`, ItemArbitration.cs:86-107; corrected in #89 — comparing
use evidence would bounce every use back).

### durability — per-hit tool condition

Tools and weapons decrement `condition` per swing (Item.cs useAction table:
shovel 0.00182, pickaxe 0.00182, machete 0.00263, flimsyknife 0.01, …).
torch/campfire decay `condition -= dt` while wet (CustomItemBehaviour.cs:
281-295, 352-369). State = `condition`.

**CUO sync: ✓** (`condition` on every path).

### modeswitch — CustomItemBehaviour.state

`public int state` (CustomItemBehaviour.cs:578-579, **no `[Saveable]`** —
the official save does not persist it). Uses: flashlight modes 0-3
(useAction `state++; if (state > 3) state = 0`, Item.cs:3990-3999;
consumption per state: 0 off / 1 on / 2 dim / 3 strobe `(int)(Time.time*18f)%2`,
CustomItemBehaviour.cs:520-558), emergencylight 0-1 (Item.cs:4014-4023),
lightbulb head-slot burn-in timer (`state += RoundToInt(dt*1000)`,
`state > 1500` → destroyed + bulb blowout, CustomItemBehaviour.cs:102-123).

Randomness: none (strobe is a time function).

**CUO sync: ✓ since #89** — the state whitelist in
`CaptureSaveableComponents` (ItemStateCodec.cs:134) admits
`CustomItemBehaviour`; the field rules only let `state` (public int) travel —
the `Item` reference (private, no `[SerializeField]`) and the `data` array
(object[], unsupported kind) stay out. Restore matches by component simple
name (ItemStateCodec.cs:267-309). All 10 carrying paths share this capture/
restore pair — no bypass. Use reports adopted unconditionally (see
[consumable](#consumable—eatdrinkinject)) so the mode switch is never
corrected back.

### payload — CustomItemBehaviour.data

`public object[] data` (CustomItemBehaviour.cs:582-583) — generic payload:
dynamite fuse flag (Item.cs:6671-6682), liquidcentrifuge cooldown timer
(`data[0] = 60f`, Item.cs:5667-5689), jetpack throttle
(CustomItemBehaviour.cs:382-428).

**CUO sync: ✕ by design.** `object[]` is an unsupported field kind
(ItemStateCodec.cs:155-159) — state is frame-level transient (jetpack
throttle) or short-lived visual (centrifuge cooldown); the dynamite fuse is a
**known gap** (a guest-loaded fuse detonates locally, not synced — tracked as
its own defect, not part of #89).

### gun — GunScript state machine

Nine `[Saveable]` JsonProperty fields: `roundInChamber`, `roundsInMag`,
`magCapacity`, `hasMag`, `triggerPressed`, `firingPinStruck`, `safe`,
`racked`, `lastRacked` (GunScript.cs:229-324). `gasTime` is private and not
saved. Items: pistol, rifle, makeshiftrifle, shotgun (Item.cs:4042, 4118,
4137, 4168).

Randomness: jam rolls `Random.value` per trigger (GunScript.cs:145, 168, 196,
203) and spread `Random.Range` (213) — see [randomaction](#randomaction—random-behavior-out-of-sync-by-design).

**CUO sync: ✓** (`[Saveable]` components captured/restored on every path).

### ammo — AmmoScript.rounds

`public float rounds` (AmmoScript.cs:75, `[Saveable]`). Items: smallmagazine,
riflemagazine, boxof12gauge, 9mmround, 556round, 12gauge (Item.cs:4058,
4075, 4184).

**CUO sync: ✓.**

### geiger — GeigerCounterAudio.active

`public bool active` (GeigerCounterAudio.cs:82, `[Saveable]`), toggled by the
geigercounter useAction (Item.cs:5597-5602) which also mirrors
`decayMultiplier`. **Already synced — the initial guess that it was a #89
sibling was disproved by the inventory.**

**CUO sync: ✓** (`[Saveable]`).

### randomroll — fixed-at-spawn random values

Values rolled once at spawn and then fixed — synced like any other state:
EPdaScript `savedIndex` (EPdaScript.cs:16), PlushScript `index` (PlushScript.
cs:30), BlueprintScript `recipeIndex` (BlueprintScript.cs:13), NonDescriptCan
contents (NonDescriptCan.cs:17-61). All `[Saveable]`.

Randomness: one-time `Random.Range` in Start/Awake — **inside the isolation
stream during world-gen** (deterministic per side); outside a generation
(spawned via materialization) each side rolls its own, then the capture
(after Start) overwrites with the host's value on apply.

**CUO sync: ✓.**

### randomaction — random behavior, out of sync by design

Per-action random consumption — **NOT state divergence**: the same action
lands different results on each side by game design.

- Food effects: popfruit (Item.cs:2491), bulbskin (2544), foliage (3549),
  browncap (3693, large effect tree), funguschunk (3892, ~40
  `Random.value` rolls), dryfoliage (3918).
- Gun jams and spread (GunScript.cs:145, 168, 196, 203, 213).
- Jetpack wet-throttle and flame alpha (CustomItemBehaviour.cs:396, 410).
- Exposedcore death animation `Random.ColorHSV` (CustomItemBehaviour.cs:254-255).

When testing, "both sides eat the same funguschunk and get different effects"
is correct behavior, not a bug.

### bodycomponent — components applied to body/limbs

Items that attach a component to a limb/body (all `[Saveable]`):
TourniquetScript via tourniquet (Item.cs:400), SplintLimb via splint
(Item.cs:1484-1488) and carcasssplint (1510-1513), ChilledLimb via icepack
(Item.cs:1635-1637), Painkillers/Antidepressants/SleepingPills/MindwipeScript
via the matching injectables (Item.cs useLimbAction tables).

State: the applied component's fields (e.g. `timeLeft`/`maxTime`,
SplintLimb.cs:48-59). **CUO sync: ✓** — these components travel in the
save-aligned character data snapshot (the body/limb component dictionaries
mirror SaveSystem's per-type component capture, SaveSystem.cs:113-146).

## Passive-effect items (no state, outside the matrix)

Items that continuously modify the body but hold no state themselves: the
crystalshard set (digestion/soothing/blood/oxygen/relief — per-frame
`sicknessAmount`/`happiness`/`bloodVolume`/`stamina`/`pain` deltas,
CustomItemBehaviour.cs:40-98, 221-229, 327-335, 472-484), autozoomgoggles
zoom assist (52-60), scubadivinggear wetness drain (339-347), blindfold
(493-501), roselight light intensity = condition (205-210).

## Crafting (the operation surfaces, landed 2026-08-13)

The crafting family syncs as OPERATIONS — one operation = one `CraftReportMsg`
carrying the complete terminal state (consumed/changed materials + products);
the host classifies each entry against its world/transfer tables, applies and
relays the whole report (source excluded). Details in docs/tech-decisions.md ("Crafting
domain"). Per-surface notes:

- **Recipe.TryMake** (the crafting menu, PlayerCamera.TryCraft → Recipe.cs:172):
  materials = inventory + 10 m floor items (Recipe.cs:107-135); the material
  disposition comes from the recipe data (`destroyItem && !isLiquid`); the
  liquid RESULT merges into an existing container with no new item
  (RecipeResult.cs:36-49) — the coordinator's liquid-fingerprint diff turns
  that container into a Changed entry; the deny path (no materials) reports
  nothing. The first-craft bonus (happiness +1, INT exp) and the fail-branch
  injuries ride the 1 Hz CharacterData snapshot (accepted latency).
- **Body.CombineItems** (drag-combine, Body.cs:1254): gun/mag and mag/round
  loads destroy the dragged item (its end-of-frame OnDestroy rides the
  destroy-claim set); the condition merge changes both items; a refused load
  or a full-condition no-op commits NOTHING (the per-branch terminal-change
  verification). The water branch opens the interactive LiquidTransfer UI
  instead — no report until `LiquidTransfer.Finish` (cancel = nothing).
- **Blueprint use** (Item.cs:4279): the blueprint's own destruction rides the
  existing use digest; the UNLOCK (`Recipes.recipes[idx].INT = 0`) rides
  `RecipeUnlockMsg` — every side applies it to its per-process static.
- **Enum component fields (codec kind 6)**: `GunScript.roundInChamber` and the
  ammo/firing-mode enums now ride the component digest (stored as the
  underlying int). The gun's live state (hasMag/roundsInMag/racked/safe —
  public bool/int) was already covered; the enum was silently dropped before
  this round (the CraftCodecContractTests kind-table now guards it).
- **Recorded gaps**: gun firing/racking has no reports (a stale chamber record
  corrects on the next drop — the report only makes records fresher); the
  container-material spill (UnloadAllItems children) deliberately rides the
  container-item domain (real new world items); noautopickup products ride the
  item domain's spawn path; mindwipe's recipe-static reset (entity domain);
  save-restore recipe INT divergence (multiplayer has no save-load path);
     Heater cooker (meat→steak) is RESOLVED as one `ItemCook` event (NetMsg 92) — see `docs/heater-cook-selfcheck.md`.

## Known state gaps (documented, not part of #89)

- **CustomItemBehaviour.data** — see [payload](#payload—customitembbehaviourdata);
  dynamite fuse gap tracked separately.
- **GrapplingHook** `fired`/`hookLatched`/`pulling` (GrapplingHook.cs:114-120,
  no `[Saveable]`) — a fired hook is local-only.
- **WatchScript** timers (WatchScript.cs:130-148) — local-only, low risk.
- **AutoPump.worn** (AutoPump.cs:56) — local-only, low risk.
- **Peer-view rendering**: the clone renderer (`CloneInventoryRenderer.RenderItemInto`,
  split out of `CharacterDataSync` at the 600-line gate) instantiates by prefab AND applies the
  snapshot's component state (`RestoreComponentStates` + the `Light2D` enabled sync) — a remote
  player's held flashlight now renders in its real mode. Display path only; the render application
  itself has no L0 test face (GameAdapter).
- **World-item component state on keyframes**: the 5 s periodic snapshot
  refreshes condition/position only (ItemPositionAuthority.cs:103-119) —
  component state of a world item stays at its last report time until
  picked up (where pickup evidence carries it) or corrected.

## Runtime item spawn surface (audit 2026-08-16)

Every runtime item creation — building/block drops, unloads, use-action
results, cook/trade/craft products, the unconscious droppings loop — funnels
into one of two sinks: standalone world items ride the generic
host-authoritative `ItemSpawn` channel, and items picked up in the same call
ride `PickUpItemPatch` → `PickupSync.OnPickedUp` (spawn-then-pickup). There is
**no world-level random/timed supply refresh** in the shipped game; the full
spawn-site inventory and the repeatability evidence are in
`docs/runtime-supply-refresh-audit.md`.

## Column key (matrix)

| column | meaning |
| --- | --- |
| `battery` | battery compartment / charge drains (incl. the battery items' own charge) |
| `liquid` | liquid container (`stack`) |
| `consumable` | eat/drink/inject, `condition`/`stack` per use, zero = despawn |
| `durability` | per-hit (or per-time) `condition` decrement |
| `modeswitch` | `CustomItemBehaviour.state` modes |
| `payload` | `CustomItemBehaviour.data` — NOT synced |
| `gun` | GunScript state machine |
| `ammo` | AmmoScript rounds |
| `geiger` | GeigerCounterAudio.active |
| `randomroll` | fixed-at-spawn random values (`[Saveable]`, synced) |
| `randomaction` | per-action randomness — diverges by design |
| `bodycomponent` | attaches a component to a body/limb |

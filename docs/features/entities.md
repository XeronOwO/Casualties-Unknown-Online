# Entity Features — Casualties Unknown (Demo)

> Architecture context: the current entity/domain ownership and kernel wire path
> are documented in
[> `architecture-evolution/domains.md`](../architecture/domains.md) and
[> `architecture-evolution/protocol.md`](../architecture/protocol.md).
> This matrix is the canonical game-mechanic/sync-status reference.

The inventory of every world entity that carries state, reacts to players, or
affects multiplayer sync — the lookup table for "did we miss a mechanism?"
[(the entity-domain twin of `item-features.md`](items.md)). Each
entity row in the matrix states its interaction face, its state fields, its
randomness, its replay presentation (what the other side sees/hears), and its
CUO sync status (with the covering `src/` mechanism or the exclusion reason).

The full entity × feature matrix lives in
[`entity-features-matrix.csv`](entity-features-matrix.csv) — one row per
entity (~70), maintained by `tools/entity-features.ps1` (see Usage below).
Read the matrix with `list`/`get`, never by hand: a misaligned row is
detected by `validate` before any output is trusted.

**The `sync` column is the completeness gate**: every row must end up
`covered` (with a path), `excluded` (with a reason), or `missing` (with a
priority) — a `missing` row is an open TODO. The table is the answer to
"is there anything left to sync?" — run `tools/entity-features.ps1 list |
Select-String missing` to get the open list.

The narrative tables below mirror the matrix's `sync`/`path` cells. The
`EntityFeaturesDocConsistencyTests` in `dotnet test` cross-check every
narrative row against the CSV, so editing one source without the other fails
the build test run — change the CSV with `tools/entity-features.ps1 set`,
then refresh the matching narrative row in the same commit.

## Matrix tool usage

```
tools/entity-features.ps1 validate                        # column count per row, entity uniqueness
tools/entity-features.ps1 list                            # all data rows, tab-separated (machine readable)
tools/entity-features.ps1 get <entity> [feature]          # one entity's row / one cell
tools/entity-features.ps1 set <entity> <feature> <value>  # edit one cell
tools/entity-features.ps1 add-entity <entity> [feature=value ...]
tools/entity-features.ps1 remove-entity <entity>
tools/entity-features.ps1 add-feature <feature>           # add a column (every row gains a cell)
```

- The CSV is UTF-8 without BOM (git-clean); a cell may not contain a comma
  (use `/` to separate values) — the script round-trips quotes via `Import-Csv`.
- Every read validates first; every write validates after — a misaligned row
  aborts with exit 1, never silently.

## Feature columns

### type
The entity family: `trap` (WorldGeneration-distributed mechanisms),
`lifepod` (escape-pod interior: buttons, shower), `unlock` (one-shot
progression unlocks: blood terminal, scrap eater, med station, recharger),
`trade` (merchant and speech), `crystal` (CrystalBehaviour effect family),
`fluid` (the world fluid grid and its writers), `environment` (corpses,
notes, tutorial, misc), `building` (BuildingEntity family), `creature`
(enemy AI).

### trigger
The interaction face that drives the entity's state: `collide`
(OnCollisionEnter2D), `trigger` (OnTriggerEnter2D), `click` (UsableObject
OnUse), `detect` (OnWillRenderObject / Update distance checks), `field`
(continuous proximity field on the local body), `story` (scripted),
`use` (player tool use).

### state
The state fields that can diverge between sides. `none` = stateless
(instantaneous or purely local-body effects).

### one-shot
`yes` = the entity consumes itself (activated/exploded/spent) and cannot
re-trigger — consumption must be shared once. `no` = repeatable, each side's
copy re-arms naturally (the vanilla behaviour; late joiners correctly see a
fresh copy).

### damages
`yes` = the entity deals damage (writes Limb/Body stats or destroys items).

### random
`yes` = the entity consumes the Random stream (generation-time rolls land
inside the isolated gen stream → deterministic; runtime rolls are local-only
→ results travel as character/world state, not as rolls).

### replay
What the receiving side plays for the event — the presentation layer
(sounds, animations, sprites, state writes). This is the "does everyone
see/hear it" column: an event's replay must cover the trigger side's
observable presentation (known gaps are listed inline in the type sections
below).

### sync
`covered` = synced (path in the next column); `missing` = not synced (open
TODO, priority in the path column); `excluded` = deliberately not synced
(reason in the path column).

### path
The covering mechanism (EntityEventKind for the trap/lifepod/unlock channel,
the domain class, the message) — or the priority for `missing` rows, the
reason for `excluded` rows.

## Traps (WorldGeneration-distributed, position-keyed entity events)

All covered by the entity-event channel: local compute (the trigger side runs
the full original effect) → report → host applies → relay → replay.
Replays: explosion family = pure-visual five-piece + real-body effect +
entity consumption; state family = run the trap's own state machine (its
sounds/animations play exactly like the trigger side); visual family = the
trap's sound/sprite/light.

| entity | trigger | one-shot | replay | sync | path |
|---|---|---|---|---|---|
| MineScript | collide | no | 0.8 s press visual (mine sound + pressedSprite) + explosion sound/particle/blastmark/shake + real-body damage + destroy | covered | MinePressed + MineExploded |
| SpikeStabberScript | trigger | yes | Stab() anim + sound + CheckStab damage | covered | SpikeStabbed |
| BearTrap | trigger | repeatable | closeSprite + sound; release replayed (BearTrapReleased) | covered | BearTrapClamped/Released |
| BarbedFence | trigger | repeatable | hitSprite + fence sound | covered | BarbedFenceHit |
| CoilScript | collide | repeatable | zap sound + light + shake | covered | CoilShocked |
| CactusScript | collide | repeatable | gore sound + self-damage health (silent BuildingEntityDamaged relay) | covered | CactusHit + BuildingEntityDamaged silent |
| JumpPadScript | collide | repeatable | light blink + jumppad sound | covered | JumpPadLaunched |
| StalactiteDropper | trigger/detect | yes | Drop() fall + DamagingCrate damage | covered | StalactiteDropped |
| GeyserScript | trigger | repeatable | TryRumble() + liquid eruption (liquidType synced in Extra) | covered | GeyserActivated |
| SoundCannon | detect | yes | spent + cancel charge (the blast's deafen/mute is the local player's UI — only the trigger side gets it) | covered | SoundCannonFired |
| TurretScript | detect | repeatable | tracers + gunshot; self-destruct = explosion family | covered | TurretFired/TurretSelfDestructed |
| CrystalElectric | collide | repeatable | zap + shake | covered | CrystalElectricShocked |
| CrystalFragile | collide | yes | destroy + drops | covered | CrystalFragileBroken |
| CaveTickSpawner | trigger | yes | nest destroy + particles + sound (the 16 spiders ride EntitySpawned + EnemyRuntimeSpawn binding; late joiner materializes via EnemySnapshot.RuntimeSpawns) | covered | CaveTicksSpawned |
| BananaPlantSlip | trigger | repeatable | plantslip sound | covered | BananaPlantSlip |
| GrabberPlant | detect | repeatable | scream bubble + ragdoll (speech / entity-state streams) + EnemyEffectMsg terminal state | covered | GrabberGrabbed layout key + EnemyEffectMsg terminal state |

## Lifepod interior

| entity | trigger | sync | path |
|---|---|---|---|
| ShuttleStartOpen | trigger | covered | ShuttleDoorOpened — door anim + shuttleOpen sound on both sides |
| LifepodController (heat button) | click | covered | LifepodHeatChanged — Extra = heatState 0/1/2; replay writes heatState/desiredTemp/enabled/sprite |
| LifepodShower | click | covered | LifepodShowerActivated — one-shot replay particles + activated; one consumption shared |
| Heater (cooker branch) | field | covered | `CookItemCommand` kernel batch — host conversion event; temperature field excluded (local body effect, rides the 1 Hz character stream) |

## Unlocks (one-shot progression — hard gameplay divergence)

| entity | trigger | sync | path |
|---|---|---|---|
| BioTerminalScript (blood unlock) | click | covered | BioTerminalUnlocked — replay Backgroundify()s the terminal + the reinforceddoor |
| ScrapEaterScript | click | covered | ScrapEaterProgress — Extra = progress; replay progress + Backgroundify on threshold |
| MedStationScript | trigger | covered | MedStationHealed — one-shot replay didHeal + laser anim + Backgroundify |
| BatteryRecharger | click | covered | BatteryInserted — Extra = slot + firstTime one-shot; charge rides the item-domain condition |

## Trade

| entity | trigger | sync | path |
|---|---|---|---|
| TraderScript | field | covered | trade domain (#59/#93; TradeStateSync/TradeExecutor) + TraderSwing hostile swing — host-computed overwrites (TraderState, every interaction + world entry + 5 s fallback); the acting side runs the game method in full and reports TraderAction |
| Talker | field | covered | SpeechMsg (NetMsg 74) — entity key + text id; clone-side bubble replay |
| LampScript | collide | covered | trade domain (#59/#93) — LightBroken's flat reputation -40 runs on both sides from the broadcasted base |

## Crystals (CrystalBehaviour family)

The effect assignment happens inside the isolated generation stream, so both
sides get the same crystal type — only the effect's runtime behaviour needs
sync. Continuous local-body effects (Burning/Temperature/Septic/Healing/
Blinding/Irradiated) are excluded: each side's body is simulated locally.

| entity | sync | path |
|---|---|---|
| CrystalUnstable | covered | CrystalUnstableExploded (26) + CrystalUnstableTicked (32) — the 5 s pre-explosion ticking (glow ramp + jitter + crystaltick sound) rides the transient Ticked event; the explosion (health0 + RemoteEntityDeath) is the durable consumption |
| CrystalMetamorphic | covered | CrystalMetamorphicTriggered (27) — death + item drops ride the event |
| CrystalMimic | covered | CrystalMimicTriggered (30); enemies ride EntitySpawned + EnemyRuntimeSpawn; crystalenemy tint rides EntitySpawned/EnemySnapshot — activated latch event |
| CrystalShy | covered | CrystalShySwapped (28); scan order risk recorded |
| CrystalTeleport | covered | CrystalTeleportTriggered (EntityEventKind 33) + 20 Hz player stream — the observerlaugh/FlashBrief replay; the body teleport itself rides the player stream |
| CrystalEMP | covered | CrystalEMPActivated (29) — black visual + battery drain |
| CrystalBurning | excluded | local body effect |
| CrystalTemperature | excluded | local body effect |
| CrystalSeptic | excluded | local body effect |
| CrystalHealing | excluded | local body effect |
| CrystalBlinding | excluded | local body effect |
| CrystalIrradiated | excluded | local body effect |
| CrystalGravity | excluded | local physics |
| CrystalKinetic | excluded | local physics |
| CrystalDripping | covered | FluidRegion/FluidWorldSync — the drip writes ride the fluid domain |

## Fluid world grid

| entity | sync | path |
|---|---|---|
| FluidManager | covered | FluidRegion/FluidWorldSync + FluidPresentation (NetMsg 96) — host simulates every member's viewport (deduplicated bands); 10 Hz diff + 1 Hz full RLE over FluidRegion; transient water-push / waterflow sound rides FluidPresentationMsg |
| OilPipeScript | covered | FluidRegion/FluidWorldSync — oil production rides the host fluid stream |
| LifepodPump | covered | FluidRegion/FluidWorldSync — pump writes ride the host fluid stream |

## Environment

| entity | sync | path |
|---|---|---|
| CorpseScript | covered | BuildingEntity/GeneratedItemAuthority — corpse hp + loot |
| SurvivorNote | excluded | local UI + local time-scale (accepted divergence) — slowmo on the reading side only |
| TutorialHandler | covered | TutorialClawStateMsg — host 20Hz claw presentation stream; per-side course/props remain by design |
| GrapplingHook | covered | item component state + RemoteItemPresentation — fired/latched/pulling ride the carried-item state; clone presents the fired sprite; rope remains a local projection |
| Climbable | excluded | local body |
| BounceShroom | excluded | local physics |
| GeigeFruitScript | excluded | local body |
| LeadbushScript | excluded | local body |
| CampfireAnimation | excluded | pure visual |
| WaterPusher | covered | FluidPresentation (NetMsg 96) — transient water push/slip from the host's fluid simulation |
| ItemLock | excluded | marker only |
| RadioactiveObject | excluded | local body field |
| XalorisScript | covered | EnemyEffectMsg septic tick — 0.5 s edge terminal state |

## Buildings

| entity | sync | path |
|---|---|---|
| Openable (locks/crates) | covered | BuildingEntityOpened |
| BuildingEntity (attack damage) | covered | BuildingEntityDamaged; red HitFlash replay on non-attacker views; remote destruction visuals replay via BuildingEntityUpdatePatch |
| DrillPod | excluded | WorldJoin-level — repair/world reset is world-join granularity |
| GunmineScript | excluded | hand-placed — outside the generation stream |
| SawbladeScript | excluded | hand-placed — outside the generation stream |

**Openable prefab configuration (asset sweep 2026-08-16):** the serialized
`Openable` components are all in `resources.assets` (17 instances, 11 root
prefabs). `isKeypad = true` only on the `dropcapsule` prefab and on the two
nested `dropcapsule` props inside `Structures/BrickLoot`; `instantOpen = true`
only on `foodbox` (root + the nested copy in `BioContainer`). Every other
`Openable` is lockpick: `containercrate` precision 0.5, `medcrate` 1.25,
`lifepodchest` 4.0. Evidence and scan method:
`docs/evidence/selfchecks/world/openable-keypad-prefabs-selfcheck.md`.

## Creatures

Enemy AI is covered by the host-authoritative enemy-sync domain (`docs/features/enemies.md`).
Continuous enemy fields ride `StateStreamEnvelope` over `KernelEnvelope`;
`EnemyAttack` remains the host-order local-apply command; combat terminal results
(bite/lunge/proximity) are kernel journal events
(`EnemyBiteResultEvent`, `EnemyLungeResultEvent`, `EnemyEffectResultEvent`).
The `Heater` temperature field on `xaloris` is **excluded by design**: a
local-body effect that writes only the local player's body temperature, so no
enemy-sync surface is needed. `LookTarget` gaze/scare and the eye face timers now ride the 20 Hz player
`StateStreamEnvelope` so remote clones turn their head/eyes toward the same world
point and show the owner's scared/panic/closed-eye face.

Remote animal deaths now replay the creature-specific death presentation
(spider `gore`/`BloodExplosion`, crystal-enemy death sound/`CrystalDistort`,
trader `gore`) through `AnimalDeathReplay` before the remote entity is
destroyed. The attacker-side experience reward and drop rolls stay
attacker-side; late-joiner health snapshots do not replay the creature-specific
effects (see `docs/evidence/selfchecks/enemies/animal-death-presentation-selfcheck.md`).

| entity | sync | path |
|---|---|---|
| SpiderHandler | covered | EnemyState stream (SpiderLegTargets) + EnemyAttack/EnemyBite events + ClawAnim replay |
| CaveTicks | covered | EnemyState stream (SpiderLegTargets) + EnemyAttack/EnemyBite events + ClawAnim replay |
| ElderThornbackBehaviour | covered | EnemyState stream + EnemyEffectMsg horror events |
| CrystalEnemy | covered | EnemyState stream (CrystalWindup telegraph) + EnemyAttack + kernel `EnemyLungeResultEvent`; runtime crystalenemy tint rides EntitySpawned/EnemySnapshot |

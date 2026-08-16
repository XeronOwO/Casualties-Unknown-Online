# Entity Features — Casualties Unknown (Demo)

The inventory of every world entity that carries state, reacts to players, or
affects multiplayer sync — the lookup table for "did we miss a mechanism?"
(the entity-domain twin of [`item-features.md`](item-features.md)). Each
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
| MineScript | collide | no | explosion sound/particle/blastmark/shake + real-body damage + destroy (press visual NOT replayed — gap) | covered | MineExploded |
| SpikeStabberScript | trigger | yes | Stab() anim + sound + CheckStab damage | covered | SpikeStabbed |
| BearTrap | trigger | repeatable | closeSprite + sound; release replayed (BearTrapReleased) | covered | BearTrapClamped/Released |
| BarbedFence | trigger | repeatable | hitSprite + fence sound | covered | BarbedFenceHit |
| CoilScript | collide | repeatable | zap sound + light + shake | covered | CoilShocked |
| CactusScript | collide | repeatable | gore sound | covered | CactusHit |
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
| Heater (cooker branch) | field | covered | ItemCook (NetMsg 92) — host conversion event; temperature field stays local-presentation |

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
| TraderScript | field | covered | trade domain #132 — host-computed overwrites (TraderState, every interaction + world entry + 5 s fallback); the acting side runs the game method in full and reports TraderAction |
| Talker | field | covered | SpeechMsg (NetMsg 74) — entity key + text id; clone-side bubble replay |
| LampScript | collide | covered | trade domain #132 — LightBroken's flat reputation -40 runs on both sides from the broadcasted base |

## Crystals (CrystalBehaviour family)

The effect assignment happens inside the isolated generation stream, so both
sides get the same crystal type — only the effect's runtime behaviour needs
sync. Continuous local-body effects (Burning/Temperature/Septic/Healing/
Blinding/Irradiated) are excluded: each side's body is simulated locally.

| entity | sync | path |
|---|---|---|
| CrystalUnstable | covered | CrystalUnstableExploded (26); 5s ticking gap recorded — the explosion latch is shared; the 5 s pre-explosion ticking stays a recorded local-presentation gap |
| CrystalMetamorphic | covered | CrystalMetamorphicTriggered (27) — death + item drops ride the event |
| CrystalMimic | covered | CrystalMimicTriggered (30); enemies ride EntitySpawned + EnemyRuntimeSpawn — activated latch event; enemy SetColor is trigger-side local (recorded gap) |
| CrystalShy | covered | CrystalShySwapped (28); scan order risk recorded |
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
| FluidManager | covered | FluidRegion/FluidWorldSync — host simulates every member's viewport (deduplicated bands); 10 Hz diff + 1 Hz full RLE over FluidRegion |
| OilPipeScript | covered | FluidRegion/FluidWorldSync — oil production rides the host fluid stream |
| LifepodPump | covered | FluidRegion/FluidWorldSync — pump writes ride the host fluid stream |

## Environment

| entity | sync | path |
|---|---|---|
| CorpseScript | covered | BuildingEntity/GeneratedItemAuthority — corpse hp + loot |
| SurvivorNote | excluded | local UI + local time-scale (accepted divergence) — slowmo on the reading side only |
| TutorialHandler | excluded | tutorial domain (claw items are per-player local props until pickup — double-give fixed; claw 20Hz flow todo remains) |
| GrapplingHook | excluded | visual low — rope is a local projection |
| Climbable | excluded | local body |
| BounceShroom | excluded | local physics |
| GeigeFruitScript | excluded | local body |
| LeadbushScript | excluded | local body |
| CampfireAnimation | excluded | pure visual |
| WaterPusher | excluded | local physics |
| ItemLock | excluded | marker only |
| RadioactiveObject | excluded | local body field |
| XalorisScript | covered | EnemyEffectMsg septic tick — 0.5 s edge terminal state |

## Buildings

| entity | sync | path |
|---|---|---|
| Openable (locks/crates) | covered | BuildingEntityOpened |
| BuildingEntity (attack damage) | covered | BuildingEntityDamaged |
| DrillPod | excluded | WorldJoin-level — repair/world reset is world-join granularity |
| GunmineScript | excluded | hand-placed — outside the generation stream |
| SawbladeScript | excluded | hand-placed — outside the generation stream |

## Creatures

Enemy AI is covered by the host-authoritative enemy-sync domain
(`docs/enemy-sync.md`): positions/health ride `EnemyState`/`EnemySnapshot`,
attacks ride `EnemyAttack`/`EnemyBite`/`EnemyLunge`, and the proximity side
effects of ElderThornback/Xaloris/GrabberPlant ride `EnemyEffectMsg`.
Recorded local-presentation gaps: `LookTarget` gaze/scare and the `Heater`
temperature field on `xaloris`.

| entity | sync | path |
|---|---|---|
| SpiderHandler | covered | EnemyState stream + EnemyAttack/EnemyBite events |
| CaveTicks | covered | EnemyState stream + EnemyAttack/EnemyBite events |
| ElderThornbackBehaviour | covered | EnemyState stream + EnemyEffectMsg horror events |
| CrystalEnemy | covered | EnemyState stream + EnemyAttack/EnemyLunge events |

# Enemies: spawning, hitting and who judges

The world's enemies in co-op — how they come to exist for the party and who decides whether one hits a player.

## What the player does

Meets an enemy in the world, fights it, and is hit or avoids the hit. The player expects an enemy they can see to be the
same enemy the others see, a hit they see land to be the hit that lands, and damage to follow the picture on their own
screen.

## What the game itself does

The world generates its enemies and its own combat resolves a hit against a body. In co-op the world and its enemies are
the host's, and every other client holds them as projections.

## Rules and states

- **Spawn classification.** An enemy the world generates is classified by the host as a runtime spawn, so the party
  materialises the same enemy instead of a contained failure: the accepted record was found when "both sandboxed clients
  logged 176 contained materialization failures at world entry while the host logged none".
- **The victim's own screen decides.** A hit is judged on the screen of the player it lands on: "a victim that sees no
  contact takes no damage, the victim's own screen decides". The host's own body stays on the game's native path.
- **A notification applies at most once.** The broadcast carries a per-enemy sequence, so "a repeated or reordered
  notification applies at most once (the ledger's per-enemy sequence)"; every in-world guest receives it with the same
  sequence, and a guest out of world receives nothing.
- **A guest never decides the hit for another player**: the ensemble is the victim's screen plus the host's arbitration,
  never a third player's view. The code names the same split: `src/CasualtiesUnknownOnline.GameAdapter/Character/EnemyCombatReplay.cs`
  "JUDGES an announced enemy attack against this client's own view — the frozen enemy copy it renders and its own" state,
  so the replay is the victim's judgement and never a re-simulation of the attacker's.
- **A crystal's threat is a function of its health, and a boss changes behaviour at two exact health lines.**
  `CrystalEnemy.cs:55` scales its presence with `1f - build.health / 450f` (its windup sound pitches and swells with speed and
  proximity), and it plays `crystalenemywindup` before the attack raycast. `ElderThornbackBehaviour.cs:81-99` switches stage at
  `build.health < 3750f` (leg speed +1, move force ×1.35, move wait ×0.6, new drone) and again at `< 2500f` (leg speed +1, move
  force ×1.25, wait ×0.5, retreat health cleared, look force ×1.8), with its shader progress `(6000f - health) / 6000f * 8f`.
  A remote player's fight therefore has the same two audible/visible stages, because the health that drives them is the host's.
- **A trap's damage is randomised per hit and cut by the victim's armour.**
  `BearTrap.cs:31-43`: a trap that rolls "`Random.Range(0f, 1f) < 0.5f`" bites the limb for
  `skinHealth -= Random.Range(70f, 100f) / GetArmorReduction()`, `muscleHealth -= Random.Range(50f, 100f) / …`,
  `bleedAmount += Random.Range(14f, 18f) / …`, `DamageWearables(0.9f)` and `brainHealth -= Random.Range(0f, 40f)`.
  `BarbedFence.cs:29-32` is the lighter family: `skinHealth -= Random.Range(10f, 25f) / …`, `bleedAmount += Random.Range(1f, 3f)
  / …`, `pain += Random.Range(20f, 60f) / …`, `DamageWearables(0.2f)`. A blast mine arms on pressure and detonates after
  `0.8f` s (`MineScript.cs:31-40`), or on destruction below `0.5f` health.
  `src/CasualtiesUnknownOnline.GameState/Domains/Entities/EnemyState.cs` holds `EntityId`, `PrefabId`, `Health`,
  `RuntimeSpawned` and `Stunned`, with "high-frequency position/velocity remains a stream" — so an enemy's position is a
  stream fact and its identity, health and spawn kind are decisions. `EnemyCombatEffectKind.cs` names the proximity effect
  families a result can carry: `ElderHorrorTick`, `ElderHorrorDefeat`, `XalorisSepticTick` and `GrabberGrabbed`.

## Refusals

None of its own: an enemy's attack is not refused, it is judged. A notification that arrives twice is dropped rather than
applied twice.

## Who judges

The VICTIM judges whether the hit landed, on its own picture and its own timeline; the host arbitrates the resulting
damage and broadcasts it with a per-enemy sequence. This is the general rule of decision 184 applied to combat: the
affected side decides, the host orders.

## What travels

The enemy's existence and state from the host; a hit's notification with its per-enemy sequence; the resulting body state.

## What the player sees and hears

The same enemies for the party, the game's own combat presentation, and damage that matches what the victim saw.

## Records

Decision 184 (the affected side judges). Accepted: `../../backlog/done/enemy-hit-determination-local.md` (31 tests green, the
matrix quoted above), `done/enemy-runtime-spawn-classification.md` (all five rows, the contained-failure finding),
`done/enemy-generation-pairing-late-join.md`, `done/enemy-snapshot-binding-recovery.md`.

## Gaps

- The enemy roster's own kinds and their attacks are the game's data and are not tabulated here yet.
- How a late joiner receives enemies already in flight is recorded in the pairing ticket rather than stated here.

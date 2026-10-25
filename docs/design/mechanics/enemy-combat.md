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
- **What the kernel keeps about an enemy is the durable half.**
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

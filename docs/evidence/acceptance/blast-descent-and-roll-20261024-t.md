# Batch 20261024-t — a descent that reaches a carrier layer, and what the member's roll really looked like

- Ticket: `docs/backlog/review/host-blast-rolls-a-second-drop-set.md`
- Artifact: `0.1.0+19aeb2f5`, deployed and verified against `<game-dir>` before the run
- Session: host + one member, one launch, kept for the whole batch.

## What the batch added and read

- **`layer-descend` works**: it starts the game's own `WorldGeneration.RegenerateWorld(bool twice)` — the sink every
  descent in the game runs through (the end-of-layer panel, the drill pod, the console's `skiplayer`) — on the world
  object as a coroutine. Two descents took the session from a layer with no carrier (`host 0 of 303`, `guest 0 of 0`) to
  one that has six, and the census lists the SAME six on both clients:

  `TurretSelfDestructed` at (-457,-77), (-101,-6), (-32,96), (16,-451), (-359,430), (334,-368) — prefab `turret`,
  health 500, **4 drop rows each**, `anchorOwn:true` on the host.

  So the staging blocker this ticket carried for three rounds (a generated layer with no destructive carrier) is gone: the
  batch can descend to one, and both sides share it.

- **`building-destroy-at` kills a building on the member**, and the member's roll was read:
  `[ItemSpawned] local scrapmetal ×3 + circuitboard` at (-32.0, 95.7) — four items, i.e. the turret's four-row set, ONE
  set, reported by the member.

- **But that write does not fire the carrier event.** The member's drops went out as standalone `ItemSpawn` reports, not
  inside an `TurretSelfDestructed` event, because a plain health write runs only `BuildingEntity.Update`'s death branch —
  the turret's own destruction path (the one the carrier patch observes) never runs. The host's copy therefore stayed
  alive at health 500, which is the duplicate's precondition left in place. The row needs the game's own damage path, i.e.
  an explosion.

- **`blast-at` is blocked by the evaluator**: a recipe that touches `ExplosionParams` / `WorldGeneration.CreateExplosion`
  is refused as `(1,1): InteractiveHost` in both shapes tried (object initializer, then statement-by-statement). That is
  the one capability the row still misses; everything else is now in place.

## The C#-in-eval shapes this batch confirmed

- A nearest-point loop using `UnityEngine.Vector2.Distance` fails as `(1,1)`; the same loop with manual `dx*dx+dy*dy`
  compiles (that is how `building-destroy-at` reads its target).
- An object initializer inside a recipe body fails the same way.
- A recipe that is larger than its need fails the same way, so these files stay minimal and the arithmetic lives in the
  batch.

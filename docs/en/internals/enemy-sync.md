# How enemies stay in step

[Documentation](../README.md) > [Internals](README.md) > How enemies stay in step

---

**After this page** you can explain why a guest never simulates an [enemy](../reference/glossary.md), what the host
actually sends, and who decides that a bite landed. The rule it follows is in
[Who decides what happens to a player](judgment-ownership.md); the rows per creature family are in
[Feature matrices](../reference/feature-matrices.md).

## Why the host owns the enemies

An enemy is not a type of its own. It is a `BuildingEntity` with the game's `animal` flag set, plus one
of several unrelated AI scripts — `SpiderHandler`, `CrystalEnemy`, `GrabberPlant`,
`ElderThornbackBehaviour`, `XalorisScript`, `CaveTicks` and others. There is no shared enemy base class;
the flag is the only common anchor, and every script brings its own state machine.

Those scripts wander, aim and lunge through Unity physics and Unity's own random numbers. Two machines
running them independently therefore diverge — and they diverge in the part the player notices, because
an enemy's position decides whether it can reach you. So the host owns them: it keeps the game's AI and
physics for every enemy in the world, and a guest does not run them at all. What a guest renders is a
frozen copy driven by host state, which is the same treatment a
[remote clone](../reference/glossary.md) of another player gets, for the same reason.

## Continuous state rides the stream

Position, velocity, rotation and the presentation flags that make an enemy look alive — the spider's
legs, the crystal's wind-up — travel on the host-to-guest [state stream](../reference/glossary.md), at the same
cadence as a player's continuous state. It is the unified stream path, not a private enemy channel.

A stream is update-only, and that limit matters here: a stream value may refresh a field on an enemy the
receiver already knows, and it may never create or remove one and never overwrite a terminal fact. A
value that is allowed to be dropped has no business deciding what exists.

## Lifecycle and health facts ride the kernel

Everything durable about an enemy is a kernel fact in the Entities domain: the enemy's row
(`EnemyStateTable`), the typed commands that change it (`UpsertEnemyCommand`, `RemoveEnemyCommand`,
`ResetEnemiesCommand`, and the combat-recording commands), and the events that record what happened
(`EnemyUpsertedEvent`, `EnemyRemovedEvent`, `EnemiesResetEvent`, and the bite/lunge/effect result
events).

Three projections carry those facts outward: one turns host kernel facts into the host's runtime state,
one turns kernel terminal facts into the snapshot a joining or reconnecting member receives, and one
turns combat results into the host's character save and the peers' presentation. Because the durable
part is a kernel fact rather than a stream value, a guest that joins late, drops or reconnects gets the
same answer as one that was there all along.

The absolute snapshot is not only a join-time message: the host also re-sends it on the in-session
repair cadence, so a member that never left the world still converges after an entry send that the lazy
session swallowed. The snapshot is a ROSTER — one row per enemy keyed by the host id — and a repeat is
harmless because the member adopts it row by row: it reuses the local copy a row plausibly is (the same
prefab near the host's match hint), materializes a row it has no copy for at the host's current
position, and destroys local copies the roster does not contain. The match hint is only a reuse hint,
never an identity: a row the hint cannot place is materialized, so drift cannot leave an enemy unbound.
An empty roster is authoritative too — a member destroys its surplus copies instead of keeping a
different world — and a roster stamped with another world generation is refused.

The host captures only over a SETTLED world, and while that does not hold neither the snapshot nor
the 20 Hz stream is published: a row bound while the layer is still generating is a row the
world-entry layer reset must drop, and a dropped row is a member's candidate lost for nothing. The
member builds its adoption candidates from its tracked copies PLUS a live scene scan, so a reset
that empties the tracked set cannot empty the candidates while the copies are still in the scene
(batch `20261021-f`: the host's early capture put 83 rows into the kernel, the entry reset dropped
them, and the candidates' loss rebuilt 946 copies of a 956-row world).

## An attack is announced, not decided

The host cannot apply an attack to a remote body directly: another player's clone has its colliders
disabled, so no collision callback on the host's machine ever fires for it. The old answer was for the
host to decide who was hit and send the result. The current answer is that the host does not decide: it
keeps its own half — the enemy's action (its AI, its aim, its own bite retreat and cooldown) and the native
collision path for the host's own body — and it announces without waiting on its own collider, so an attack
that reaches nobody is a legal outcome.

- The host publishes the enemy's action — which enemy, which kind of attack, and a per-enemy sequence
  number — to every member in the world, over the attack announcement message `EnemyAttackMsg`
  ([Protocol messages](../reference/protocol-messages.md)). Those three are the whole message — `EnemyId`,
  `Kind`, `AttackSeq` — and it names no victim and no limb, because the host picks neither.
- Each client judges that announcement on its own screen: has this attack actually reached my body? A
  spider bite needs a real collider contact plus the game's own facing gate
  (`SpiderHandler.CheckForLimbDamage`); a crystal lunge runs the game's own ray (`CrystalEnemy.Lunge`),
  where the first body wins and the ground stops it. The limb is chosen on that client too, because that
  is the machine that knows what it is rendering.
- The client applies the game's own damage locally and reports the terminal state, which lands in the
  kernel as a combat result event.

The sequence number is what makes "one attack, one victim, once" work: a repeat, a reorder or a
malformed identity fails closed on the receiving side instead of being applied twice. Two clients
judging the same attack is not a conflict to arbitrate — it is two players standing in the same place,
and each of them is right about their own body. No latency value enters any of this: the judgment uses
the client's own screen and its own timeline.

## Runtime spawns and removal

Most enemies come from world generation: the generation stream distributes them, but physics decides the
exact placements, so the guest's regenerated layout can differ from the host's. The guest therefore does
not treat its own generation as the identity: the host's roster is the truth, and the guest adopts it
per row — reuse when a local copy is plausibly the same animal, materialize when it is not, destroy the
surplus.

An enemy created during play is a [runtime spawn](../reference/glossary.md) — a cave-tick spawner is the usual case.
"During play" is a boundary both sides record as a SET, not the game's `generatingWorld` flag read
when the animal's `Start` runs: the edge is the ACTIVE animals that existed when the world settled, and
anything that appears after it while the session is up is a runtime spawn. The flag read at `Start`
cannot carry that fact — a dense world's generator leaves hundreds of `Start` calls to run after the flag
flips, and every one of them read as a runtime spawn (873 of 956 generation animals, batch `20261021-f`).
The same edge fact reaches the general entity-creation channel, so a generation animal is never reported
there as a runtime creation either.
It is a row of the same roster: the creation report creates the member's copy, and the host's next
snapshot carries the row's creation key, so a keyed row binds the copy carrying that key (or
materializes it on a member that missed the creation). A runtime row can reach the member twice: the
20 Hz stream announces its id — for a member already in the world, before the next snapshot does — and
the live pass that answers it takes the row's prefab and its runtime marker from the SAME durable kernel
fact (the stream frame carries presentation only, so its buffer row has neither), which binds the
creation copy on the next batch instead of leaving the enemy unknown to the member's attacks until a
snapshot arrives. The snapshot's creation key is the exact backfill when that live pass cannot bind.
Removal is a kernel fact too, and a session-scoped guard stops a stale stream value from rolling back an
enemy the kernel has already removed.

## What a game update can move

Freezing heterogeneous scripts is the largest adapter surface CUO has, and it is the part a game update
is most likely to move: a renamed field, a new state in one script's machine, a changed collision path.
Harmony contract tests lock the freeze list, so the failure surfaces as a red test rather than as an
enemy that behaves differently on one machine.

The boundaries are deliberate and worth knowing before extending any of this:

- Only presentation and continuous state are streamed. An enemy's internal AI state — its target, its
  timers — is not, and does not need to be: the guest is not running that AI.
- Because no guest ever simulates enemy physics, there is no deterministic-simulation divergence to
  resolve, and no enemy outcome needs rolling back.
- A new enemy family that can be spawned during play has to reuse the spawn-report plus runtime-binding
  path, or a late joiner will never materialize it.
- Local body effects stay local: an effect that writes only the affected player's own body is excluded
  from sync by design, not by omission.

## Related reading

- [Who decides what happens to a player](judgment-ownership.md) — the rule the attack path follows
- [State streams and snapshots](state-and-snapshots.md) — why a stream value may never create or remove anything
- [Feature matrices](../reference/feature-matrices.md) — every creature family and its verdict
- [Protocol messages](../reference/protocol-messages.md) — the messages and ids named here
- [The adapter and a game update](adapter-and-updates.md) — what a game update may move under these rows

---

[Documentation](../README.md) > [Internals](README.md) > How enemies stay in step

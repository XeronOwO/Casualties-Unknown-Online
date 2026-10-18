# The member reports that still have no bound

- Status: Todo — Rejected (batch `20261019-a`: the entity-event drop bound's forged-refusal and unmodified-session rows pass, but an ordinary event's accepted drop never reaches the kernel — the trap composite is rejected on the trap's raw negative health — and the fragile-crystal row is unproven in a world that carries no fragile crystal) — opened 2026-10-16 by the survey behind `docs/architecture/member-report-validation.md`
  (every row of its census marked `unbounded:` points here). S2b landed the block-break carrier's building
  drops; this ticket is the rest of the same question, one surface per row, each with its own evidence.
  Section A (the reports that name another member's identity) CLOSED 2026-10-17 鈥?its five surfaces now
  compare against the transport sender, and the census rows say so. Sections B and C remain open, with three
  exceptions: the container-sync row was READ on the machine 2026-10-17 (batch `20261017-d`; record
  `docs/evidence/acceptance/member-report-bounds-container-sync-20261017-d.md`), and the
  `NetMsg.EntityEvent` DROPS row landed 2026-10-18 (its trigger half stays unbounded on purpose) — its
  evidence and remaining acceptance are in section C, and the `BuildingEntityDamaged` magnitude row CLOSED the same
  day as `unbounded`: its assumed anchor does not exist (the damage values are code, not item data) and the reading
  is in section B. The `NetMsg.TraderAction` GIVE row is IMPLEMENTED and READ ON THE MACHINE 2026-10-18 — the
  identity anchor landed (wire field + host-side judgement + the generation-time container-contents naming it
  exposed) and batch `20261018-z` passed all four owed rows; section B.
- Priority: Medium-High
- Category: Security / report validation
- Source: the user's 2026-10-14 request (basic checks from the game's own mechanics, never an anti-cheat)
  and the 2026-10-16 direction that the validation standard becomes a general development rule 鈥?which is
  also why these are written down instead of left implicit.
- Related: `docs/architecture/member-report-validation.md` (the census these rows come from),
  `done/validation-and-source-checks.md` (S1/S2a/S2b landed the seam, the block drops and the building
  drops), `docs/evidence/sync-coverage-matrix.md` (each surface's event + fallback policy)

## The three patterns

Three shapes, and the fix direction is the same within each: an identity that is already known to the
host, replaced by the payload's claim; a number the host could derive, taken from the report; and a claim
with no anchor at all, which needs a design decision rather than a check.

## The three reach bounds were LANDED, READ "PASSING", AND THEN REVERTED (2026-10-18)

Everything this ticket's B section says about the reach bound above 鈥?the three commits, the two machine readings,
the "PASSES" verdicts 鈥?is **withdrawn**: `fd2d2b3f` reverts `6bdf1e36`, `5168fe78`, `1a651ddc` and the two
acceptance verbs built on them. The independent review (which the whole family had skipped) found four blockers, and
the two readings that "passed" were produced by a test verb sharing the check's own mistake, so they proved nothing:

1. **The block path compared block INDICES with WORLD POSITIONS.** `OnRemoteBlockPlaced` hands over a `Vector2Int` of
   block coordinates while the reporter's body is a world position; the game's own conversion is
   `WorldGeneration.WorldToBlockPos` (`new Vector2Int((int)(pos.x + this.halfWidth), (int)(pos.y + this.halfHeight))`,
   with `halfWidth = width * 0.5f`, `width = chunkWidth * CHUNKSIZE`, `CHUNKSIZE = 64`). The offset is at least 128
   cells, so `<= 8` was false for every real break: the bound would have refused **all legitimate player mining**.
   The repo's own existing consumers do the conversion first (`GameAdapterBridge`:
   `world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y))`).
2. **The block bound sat on the wrong message, and its own selector is reported by the bounded party.** A break lands
   through `BlockDamaged` (`BlockBreakSync.FlushPendingBlockBreak` 鈫?`SendBlockDamaged`), which
   `BlockBreakArbitration` accepts while the cell still stands and the generation verifies 鈥?so a refused air write
   still had the break applied and relayed a frame later. And the branch is selected by `playerBreak`, which the
   reporter writes: setting it false skips the check entirely.
3. **A refusal did not stop the relay.** `BuildingEntityDamagedHandler` / `BuildingEntityOpenedHandler` broadcast
   unconditionally AFTER firing the event, so the other guests applied a refused open/damage while the log and the
   comment claimed "nothing applied, nothing relayed". The handler needs the verdict back (a `bool`/`WorldReportVerdict`
   from the fire call) and must relay only on acceptance.
4. **The damage channel is not only a player gesture.** `ExplosionBuildingSync` reports explosion damage through the
   same path, so a reach bound refuses legitimate explosions.

Smaller findings worth carrying: a stale position refuses a legitimate fast action; a refused report gets no answer so
the reporter's client re-reports in a loop; quantisation differs between the three paths; two host-role gates spell
the same rule differently; three comments and one log line are false as written.

**What the next cycle must settle BEFORE writing a check**, in this order:

- **Which message owns the break.** The bound has to sit where the break actually lands (`BlockDamaged`, or both),
  not only on the air write.
- **What a host can PROVE about the cause.** "The reporter said it was a player gesture" is not an anchor. Either the
  wire carries a cause the host can verify (its own quake window, its own record for the cell, the weapon identity),
  or the bound applies to every reported write and the host distinguishes cause itself.
- **Whether a refusal can suppress the relay** 鈥?if the verdict cannot travel back to the handler, no bound on these
  paths is honest.
- **World coordinates everywhere.** One helper, world space only, converting block cells with `WorldToBlockPos` at the
  single place a cell becomes a point.

Until that, these rows are open and the census rows for them must say so rather than "closed".

### The design the next cycle implements (decided 2026-10-18, from the review's four questions)

**1. The break belongs to `NetMsg.BlockDamaged`, not to the air write.** The air write is what makes a cell empty;
`BlockBreakSync.FlushPendingBlockBreak` 鈫?`_world.SendBlockDamaged(...)` is what actually lands the break and carries
the drops, and `BlockBreakArbitration` accepts it while the generation verifies and the cell still stands. So the
reach check moves to the damaged path 鈥?and the air write keeps only the bound it already has (generation + cell
state), because a bound on a message that does not decide the outcome is decoration.

**2. The cause must be something the HOST can prove, and it has two cases.** A report that claims a player gesture
(`playerBreak: true`) must come from a body within reach. A report that claims an environment/quake write must be
matched against the host's own quake state 鈥?the same predicate the adapter already uses,
`WorldGeneration.world.earthquakeTime` in `GameAdapterBridge`'s set-block gate, where a quake break is recognised and
an environment break passes. No quake running on the host 鈫?that claim is refused BY NAME (`"no quake is running on
the host"`), which is exactly what rule 16 asks for: the receiver checks a claim against what it can establish itself.
The alternative 鈥?trusting the reporter's `playerBreak` flag 鈥?is what the withdrawn attempt did and must not come
back.

**3. A refusal has to be able to STOP the relay, and today it cannot.** Both entity handlers
(`BuildingEntityDamagedHandler`, `BuildingEntityOpenedHandler`) fire the event and then broadcast unconditionally, so
a refused report is still applied by every other guest. The verdict must come back from the fire call 鈥?`bool` is
enough 鈥?and the handler relays only when it is true. The same shape applies to `BlockDamagedHandler`. Without this
step no bound on these paths is honest, so it is done FIRST, as its own verifiable change: a refused report must be
observably absent from the other client's log.

**4. One helper, world space only.** The block cell is converted where it is used, with the game's own
`WorldGeneration.WorldToBlockPos` (the repo's existing consumers call it: `GameAdapterBridge` does
`world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y))`), never by subtracting an index from a
world coordinate. The entity paths already sit in world space and stay there. Two copies of the helper are acceptable
only while they are byte-identical in rule; the constants and the accept-on-missing-position rule live in one place.

**Order of the cycle**: (3) verdict, verified end to end on the machine 鈫?(1)+(4) the break bound with world-space
conversion and a red test that FAILS today with the real cell 鈫?(2) the quake anchor 鈫?then the entity rows on the
same verdict plumbing, with the explosion channel excluded by the host's own explosion record rather than by the
reporter's claim.

### Second review of the verdict plumbing (2026-10-18) 鈥?attempted, REVERTED, findings recorded in full

A first attempt at step (3) made the fire calls return a `bool` and relay only on true. The independent review found
two blockers and six majors, so it was reverted uncommitted rather than shipped. The findings ARE the spec for the
next attempt:

1. **BLOCKER 鈥?a refused open was still recorded as a durable host-side fact.** `FireBuildingEntityOpenedReceived`
   called `_eventChannel.ReportOpenedEntity(pos.X, pos.Y)` BEFORE invoking the handlers, and that record is durable:
   `OpenedEntityRegistry.Report` 鈫?`_kernelAuthority.TryRecordOpenedEntity(...)` 鈫?`WorldEntities.OpenedEntities`,
   written into every save (`WorldSnapshotEncoder`: `state.OpenedEntities.Select(SaveWorldEntityRow.OfOpenedEntity)`)
   and replayed for a late joiner (`WorldEntityKernelProjection`), where the adapter applies `entity.health = 0f`. So
   both new refusal cases left the kernel claiming the entity was opened and the log's "nothing applied" was false.
   **Fix**: invoke first, record only when the verdict is true.
2. **BLOCKER 鈥?the verdict rode a multicast `Func`, where only the LAST subscriber's answer survives.** A subscriber
   added after `WorldEventSync.BindToSession` could resurrect a refused open or mute a legitimate one, the adapter
   applies inside its own subscriber (so `true` never meant "may I apply"), and `?? true` failed open when nothing was
   bound. **Fix**: a single-consumer admission seam consulted BEFORE the apply event; keep the apply event `void`.
3. **MAJOR 鈥?the damage verdict was a constant.** `return true; // applied (or nothing stood there)` gated a handler
   that could never close, and the threaded `sender` was read by nobody 鈥?a mechanism with no consumer.
4. **MAJOR 鈥?a refusal was never answered.** The reporting guest had already opened the entity on its own copy
   natively, so refusing with no message left it open for the reporter and locked for everyone else 鈥?a permanent
   divergence, and the report rule requires an answer (the block path answers with `SendBlockPlacedCorrection`).
5. **MAJOR 鈥?"no entity there" silently stopped the relay** while that method's own summary said the relay ignores
   the value: two different refusals collapsed into one `bool`.
6. **MAJOR 鈥?the census would have contradicted the code**: `member-report-validation.md`'s `BuildingEntityOpened`
   row still said the bound "could not suppress the handler's relay", which the change made false; the gate cannot
   catch a stale sentence, so the page has to be rewritten in the same change.
7. **MAJOR 鈥?`WorldEventSync` landed at exactly 600 lines** with no debt entry, one line short of the gate; the fix
   is extracting the building-entity relay into its own type, not recording the debt again.
8. **MAJOR 鈥?the one new Runtime branch had no test**: every adapted subscriber returned `true`, and nothing
   subscribed to `BuildingEntityOpenedReceived` at all. **Fix**: host subscribes a `false` verdict, G1 sends, assert
   G2 received nothing (and that the host's checkpoint has no opened row); a `true` case asserts the relay.

Also noted clean by that review: the sender really is the reporter on the host and the host on a guest (so the
`Role == Host` gate is right), and the open path's coordinates are world-space on both sides 鈥?but `InteractionReach`
must be renamed to say world units, because a constant named "cells" compared against world coordinates is exactly
how the withdrawn block bound went wrong.

## DECISION 265 (2026-10-17, user's ruling): data validation only 鈥?the reach family is withdrawn for good

**Everything above that talks about a distance, a reach, "could this body have been there", the frozen constant, the
admission seam or the four-question design is CLOSED, not pending.** The user's ruling: CUO validates DATA, never
BEHAVIOUR. A report is checked against facts the receiver already holds 鈥?an identity it can compare, a value it already
computed, a table it owns. Modelling what a player is physically able to do (reach, aim, timing, cooldown, input rate)
is anti-cheat, which the MVP explicitly excludes, and it duplicates the game's rules in a second, weaker implementation
that refuses legitimate play whenever the two disagree.

What that means for this ticket, row by row:

- **`BlockPlaced`** 鈥?already bounded by DATA (the generation stamp + the first-writer cell rule). No reporter check is
  wanted; the row reads `bound` for the data half and says the reporter half is out of scope.
- **`BuildingEntityOpened` / `BuildingEntityDamaged`** 鈥?no data anchor exists yet: the open message carries no lock
  identity and the host has no model of lock progress; the damage message carries no weapon identity. Those are WIRE
  additions, and they are the only legitimate way to bound these two. Until then both rows stay `unbounded` and say so.
- **The verdict/suppress plumbing** that a refusal needs has no customer left, so it is not built speculatively.

What survives from the cycles above and is worth keeping when a data anchor arrives: a refusal must be answered
(otherwise the refused side keeps a state nobody else has), it must be asked BEFORE anything is applied or recorded
(the kernel's opened-entity table is saved and replayed to late joiners), and it must be able to stop the relay.
Those are properties of any future data bound, not of a reach bound.
### Native evidence for a DATA anchor on the open report (2026-10-18)

The open is decided by a hand-driven minigame on the opener's own client, so the host cannot reproduce it 鈥?and under
decision 265 it must not try. `reversing/Assembly-CSharp/Assembly-CSharp/LockpingMinigame.cs` is that minigame: its
`Update` accumulates `this.lockProgress += Time.deltaTime * (0.66f + num * 0.065f);` while the hand sits inside a band
(`this.clickingInside = this.HandAngle > 0f && this.HandAngle < 180f && this.HandDistance > 195f && this.HandDistance < 247f;`),
and completion is `this.toDestroy.health = 0f;`. Hand angle and distance are the opener's input; no host-side model can
judge them, which is exactly why a reach check was the wrong shape.

What the host CAN check, both of them data:

- **The item the gesture needs.** A lockpick is an inventory object; "the reporter carries the item this lock requires"
  is an identity claim the host holds the inventory to compare against. The message carries no item identity today, so
  this is a wire addition 鈥?the same shape the trader-give and building-damage rows need (weapon identity).
- **The code, for a keypad lock.** The host already owns keypad codes (`NetMsg.KeypadCode` = 57 is its own authority),
  so a report that carries the entered code can be compared against the record the host already holds 鈥?no new model,
  no behaviour simulation. This is the cheapest genuine anchor and the first one to build.

What neither anchor covers, and must not be faked: whether the minigame was actually played. That is behaviour, it is
anti-cheat, and it stays unbounded by ruling.
### The keypad anchor does not exist 鈥?the code is shipped to every client (2026-10-18)

Checked before building it, and the plan does not survive: the keypad code is HOST-OWNED but not HOST-ONLY.
`EntitySpawnSync` creates it on the host (`KeypadCodeTable.EnsureCode(openable) // the host creates it 鈥?its code is host
authority from the start`), carries it in the spawn report (`KeypadCode = KeypadCodeTable.EnsureCode(openable)`), and
every receiving client writes it into its own copy:

    if (msg.KeypadCode.Length > 0)
        Traverse.Create(openable).Field("code").SetValue(msg.KeypadCode);

So a code the opener "entered" is a value that client was already given. Comparing a reported code against the host's own
copy would always agree 鈥?it is not an anchor, it is a tautology, and building it would be exactly the kind of check that
looks defended and proves nothing.

What that leaves for this row, all of it honest:

- **The item anchor is the real one** and it needs the wire: "the opener carries the tool this lock requires" is an
  inventory identity claim the host holds the data to check. Same shape as the weapon identity the building-damage row
  needs, so both land in one protocol change.
- **The minigame result stays unbounded** (decision 265 鈥?behaviour, not data).
- **The keypad code is not a bound at all**; it is a shared secret the design hands to every peer at spawn. If the
  project ever wants keypads to be a real barrier, that is a separate decision about who may hold the code, not a
  validation check on the open report.
### Where the weapon identity actually is (2026-10-18) 鈥?and why the wire field is not mechanical

`Body.Attack` computes building damage from an `AttackInfo` (`atk`), not from an item:

    buildingEntity.health -= (buildingEntity.animal ? atk.damage : atk.structuralDamage) * num2 * (...);

(`atk.unarmed`, `atk.structuralDamage`, `atk.distance`, `atk.swingSounds` are the same object.) The report site 鈥?`BodyPatches`' postfix 鈥?holds the `Body` (`__instance`) and the damaged entities, so it CAN name the held item through
`Body.GetItem(slot)` / `Body.HoldingItem(slot)`, but the link between "the item held in this frame" and "the `atk` that
computed this damage" is one frame apart and not a direct reference. That makes "carry the weapon identity" a real
design step with a correctness question (what if the hand changes in that frame), not a mechanical field addition 鈥?and
it is the reason the damage row has stayed open rather than half-built. **The next section checks that premise**: the
frame question is answered there (the same call, not a frame apart) and the table it assumed does not exist.

Two candidate anchors, both DATA, to choose between when this row is built:

- **The item identity on the report** and the host comparing it with the item it holds for that player plus that item's
  own damage table 鈥?the shape this ticket has assumed. Needs the frame question above answered.
- **The attack's own numbers**: the report carries the structural damage the client computed, and the host compares it
  with the item's table itself. Same wire content, different comparison point; avoids the frame question but duplicates
  the multiplier (`num2` includes the claw curve when unarmed), which is game logic the host should own, not re-derive.

The open row for the OPEN report has the same shape one level simpler: the lockpick item is the gesture's tool and there
is no damage arithmetic, so it is the one to land first.
### The damaging item's "damage table" is code, not data — so the magnitude has no anchor either (2026-10-18)

The section above left two questions open: whether the held item can be tied to the `atk` that computed the damage, and
whether the host can own "the item's own damage table". The first is answered YES and the second NO, so the row closes
unbounded by decision 265 instead of being half-built.

**The frame question is not a gap.** `Body.Attack(AttackInfo atk, int slot)` overwrites the slot argument with the
body's own hand slot (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs`: `slot = this.handSlot;`), and every item
attack reaches it synchronously from that item's `useAction` delegate — `Body.UseItemInHand` runs
`this.GetItem(num).Stats.useAction(this, this.GetItem(num));` with `int num = this.handSlot;`, and the delegate body is
the `body.Attack(new AttackInfo { ... }, 0)` call itself (`Item.cs`'s `Item.GlobalItems` initializer). Nothing inside
`Attack` or inside an item's `useAction` assigns `handSlot` (no `useAction` in `Item.cs` mentions it at all), so a
postfix that reads `__instance.GetItem(__instance.handSlot)` names exactly the item whose delegate built that `atk` —
the same call, not a frame apart. The one attack with no item is the unarmed claw: `UseItemInHand`'s fallback builds its
own `AttackInfo` with `unarmed = true` (`Body.cs`: `this.Attack(new AttackInfo { damage = 30f, structuralDamage = 20f,
... unarmed = true }, 0);`).

**The table does not exist.** `ItemInfo` — the game's per-item table, and the only item data a host can read by
definition id (`reversing/Assembly-CSharp/Assembly-CSharp/ItemInfo.cs`: `value`, `weight`, `tags`, `qualities`,
`usable`, …) — has no damage member. Every melee number is a literal inside the item's `useAction` delegate: `Item.cs`
has 27 `body.Attack(new AttackInfo` sites, each a freshly constructed `AttackInfo` with its own `structuralDamage`
literal (75f, 65f, 45f, … 140f, 10f, and the flimsy knife's 16f), plus the unarmed 20f above. So "the host compares
the reported number with the item's table" has no table to compare against: the host's own `Item.GlobalItems` holds the
same delegates, and the only ways to read a delegate's literals are to invoke it — which runs the attack — or to read
its IL. Neither is data validation; invoking it is the gesture simulation decision 265 forbids.

**And a copied table would still be a second rule.** The reported number is not the literal either: `Body.Attack`
writes `buildingEntity.health -= (buildingEntity.animal ? atk.damage : atk.structuralDamage) * num2 *
((atk.metalMoreDamage && buildingEntity.metallic) ? 10f : 1f);`, where `num2` is the attacker's own body state —
`this.slots[slot].armPowerMult` (`InventorySlot.cs`: the sum of `staminaStrength.Evaluate(limb.totalForce)` over the
slot's use limbs, times `actualStaminaStrength`, capped at 0.65 by a broken or dislocated arm), `× 0.75` for hand slot
1, `× (1 + skills.STRFrom10 * 0.0334)`, and, unarmed, `× clawDamageCurve.Evaluate(clawHealth)`. A CUO-owned copy of the
constants would still refuse a legitimate hit whenever the host's model of the attacker's body differs from the
attacker's own client — the exact failure the ruling names when it says a second implementation "refuse[s] legitimate
play whenever the two disagree". Making the recomputation exact needs the attacker's own frame and body reading, which
is the gesture itself.

**What the wire could carry does not bound anything.** Adding the damaging item's instance id (the reverse lookup
`review/item-identity-bridge.md` calls one line) lets the host check that the sender holds the item it names — a real
DATA fact, but one about the claim's SOURCE, never its magnitude: a member holding a digging tool can still report any
number. That is exactly the "looks defended and proves nothing" check this ticket refuses elsewhere, so no wire field
is added.

**Verdict**: the row is `unbounded` by decision 265 for the same reason the open report is — the number is the
attacking client's own computation over code and its own body state, and the receiver holds no fact that contradicts
it. The census row now says this, and both candidate anchors in the section above are closed.
### The open's tool identity is blocked on ONE mapping, not on the wire (2026-10-18)

Attempted and reverted the same way. What is now known, so the next attempt starts at the gap instead of the field:

- The wire is not the problem: `BuildingEntityOpenedMsg` takes a `ToolItemInstanceId` and `SendBuildingEntityOpened(pos,
  toolItemInstanceId = 0)` carries it in one line, through `WorldService` and `WorldStateMessageService`.
- The host check is not the problem either: `IKernelItemFacts.FindItem(ulong instanceId)` returns the `ItemState`, whose
  `Location` is an `ItemLocation` with `Carried(ActorId owner)` and friends, so "the reporter really carries that item"
  is a comparison the host can make against data it already holds.
- **The gap is the client side: the game's `Item` carries no instance id, and CUO has no accessor that turns a live game
  `Item` into the replicated instance id.** `reversing/.../Item.cs` has no instance-id member at all, and the mapping
  lives inside the domain modules that build commands from messages (`PlayerDomainModule` reads `c.ItemInstanceId` off a
  message), not as a lookup an adapter patch can call.

So the next step is one small Runtime/Adapter addition with a real consumer: an accessor that answers "what is the
replicated instance id of this live game item" (or the inverse map maintained where items are adopted). With it, the
open patch names `Minigame.game.currentItem` 鈥?the lockpick, per
`reversing/.../LockpingMinigame.cs` (`NeedsItem() => this.pickLevel >= 0`, `Minigame.game.currentItem`) 鈥?and the host
check above becomes possible. Until then the open report has no data anchor and the row stays open, honestly.
**Prerequisite filed:** the client cannot name the tool it used 鈥?the game's `Item` has no instance id and CUO keeps no map 鈥?so both remaining rows wait on `docs/backlog/review/item-identity-bridge.md`.

### Third attempt, reviewed and reverted: the tool claim cannot be made falsifiable (2026-10-18)

Built end to end and withdrawn uncommitted. The independent review found four blockers, and two of them are structural
rather than bugs 鈥?they close this anchor, not just this attempt:

1. **Empty-handed lockpicking is legal.** `reversing/.../Openable.OnUse` clears the tool when the skill check fails
   (`if (PlayerCamera.main.body.skills.INT < 10) { item = null; }`), so "I opened it with no tool" is ordinary play. A
   report that names a tool can therefore only be checked when it CLAIMS one, and a zero id has to be accepted 鈥?which
   makes the id unfalsifiable: any client sends 0 and passes.
2. **The id is gone by the time the report fires.** The successful lockpick path writes `this.toDestroy.health = 0f;`
   and then `Minigame.game.EndMinigame()`, which clears `currentItem` (`MinigameBase`: `this.currentItem = null;`) 鈥?   all inside the `Update` whose POSTFIX is our hook. So the tool's id reads 0 exactly on the path this check exists
   for. It would have to be snapshotted in a prefix, or taken at the gesture's own start.
3. The rest was mechanical but fatal in production: the id was dropped one hop inside the adapter (`WorldBuildingEntitySync`
   called `SendBuildingEntityOpened` without it), and the check ran on the guest relay path too, where `sender` is the
   HOST, so every relayed open would have been refused against the opener's item.
4. A refusal here also has no answer, and the opener's own copy is already open with its drops rolled 鈥?so an
   unanswerable refusal here is worse than none: it turns "forge a tool" into a one-way loot advantage.

**Conclusion for this row**: the open report has no data anchor that survives contact with legal empty-handed play. It
stays `unbounded` and the census says so. If the project ever wants tool claims to be checkable, the wire has to carry a
claim that can be FALSE (a gesture kind), and the refusal has to be answered 鈥?both are prerequisites, not details.
## A. A report that names another member's own body or hands 鈥?CLOSED 2026-10-17

LANDED: every surface below compares the identity it would write against the TRANSPORT SENDER. The
admission seam judges a pickup's destination and the enemy-combat victim BEFORE its item rules, because
neither needs a fact the host has to look up (`KernelCommandGateway.AdmitMemberSubmission`, pinned by
`KernelMemberIdentityReportTests`), and three handlers compare HOST-SIDE only, since the host's own relay
legitimately names a third member (`LimbStateEventHandler`, `SceneStateHandler`, `SpeechHandler`; pinned
by `SceneStateReportIdentityTests`, `LimbStateSyncTests`, `SpeechChannelTests`). The scene handler's
`msg.SteamId != 0 ? msg.SteamId : sender` fallback is gone: the payload no longer decides whose state it
is. Verdict shape, per the family: IGNORED with no answer in the seam and dropped at the handler 鈥?nothing durable written, nothing relayed.

The machine reading is `docs/evidence/acceptance/member-report-bounds-a-20261017.md` (batch `20261017-a`, three clients): each of the seven wire members read a by-name refusal on the host, and an unmodified stretch of the same session read none.

What did NOT move: magnitudes, distances and rates 鈥?the affected member's own report still carries them,
which is the design intent quoted below. What the direction leaves open, named rather than implied: the
three handler shapes are `Bidirectional`, so a forged frame addressed straight at a GUEST (rather than at
the host) is still applied there 鈥?a limb latch into its clone fact table, a scene row into its presence
table, a bubble on the named clone. That is presentation-only on that client, overwritten by the owner's
own next report or keyframe, with no durable record and no host write; bounding it would need a
guest-side identity rule the star topology has no anchor for. The pickup verdict's cost is the
reconciliation budget rather than silence: a refused pickup kind is RE-REPORTED
(`GuestCommandReconciliation`, `IntervalMs = 5_000`, `MaxReports = 12`), so one forged report is up to
thirteen host audit lines over about a minute, while the three enemy kinds are single-shot.

The five surfaces the census recorded, with the account this section opened with 鈥?each now bound as
described above:

- **`WireCommandKind.ItemPickup`** 鈥?the domain commits `ItemLocation.Carried(command.NewOwner)` with no
  comparison against the sender (`ItemDomainModule.DecidePickup`), and the seam's eligibility family
  covered a destroy report, a state report and a container sync but not this one, so a report could put a
  world item into another member's hands. NOW: the destination must be the sender, in the same
  eligibility family the S1a stage built.
- **`NetMsg.LimbStateEvent`** 鈥?the stored write was keyed by the body the payload names
  (`CharacterDataStore.ApplyLimbStateEvent`), with no sender comparison in the handler, so a report could
  latch another member's limb. NOW: on the host an inbound frame must name the sender.
- **`WireCommandKind.RecordEnemyBite` / `RecordEnemyLunge` / `RecordEnemyEffect`** 鈥?the entity domain
  accepted all three outright (`EnemyDomainModule`), so the victim, the limb and the effect magnitudes
  were the reporter's own. NOW: the victim must be the sender, which is what the design intent requires 鈥?  the AFFECTED side's client is the judge (`done/health-simulation-and-host-authority.md`).
- **`NetMsg.SpeechMsg`** 鈥?the bubble was replayed under the speaker the payload named, with no sender
  comparison and no length cap. NOW: on the host an inbound frame must name the sender. The length cap is
  still open: it belongs with the abuse bound the chat handler's own rate cap is waiting for, and no
  reading of a native bubble's length exists to bound it with.
- **`NetMsg.SceneState`** 鈥?the reporter was `msg.SteamId != 0 ? msg.SteamId : sender` and the membership
  table was the only check, so a report could move ANOTHER member's scene state and have the host send
  that member the whole entry fanout. NOW: on the host the claim must name the sender, and the fallback is
  deleted rather than kept as a second path.
## B. A number the host could derive, taken from the report (one row closed, one re-judged 鈥?2026-10-17)

- **`NetMsg.BuildingEntityDamaged` (51)** 鈥?the only check is that an entity stands at the position;
  `entity.health -= damage;` takes the magnitude as reported, so one report destroys any building. The
  assumed anchor was the damaging item's own identity and its damage table (`Body.Attack`'s melee write is the
  native call site), which the message does not carry — and which, on reading the game, does not exist as data
  (see the CLOSED paragraph below).

  **The WHERE half landed and read (2026-10-18, `1a651ddc`, deployed `+1a651ddc`)**: the sender now travels the whole
  path 鈥?packet handler 鈫?world event 鈫?service passthrough 鈫?relay 鈫?`WorldBuildingEntitySync` 鈥?and a damage report
  further than the shared reach from the reporter's own body is refused before anything is applied or relayed.
  Driven from the member client: `/entitydamage 400 0 50` produced
  `[EntitySync] <member>'s damage report at (400,508) is further than 8 cells from its own body 鈥?refused: nothing
  applied, nothing relayed`, and `/entitydamage 2 0 5` added no refusal line.

  **The HOW MUCH half is CLOSED 2026-10-18 — unbounded by decision 265, not faked**: the anchor this row assumed does
  not exist. The damage is an `AttackInfo` literal (code, inside each item's `useAction` in `Item.GlobalItems`) times
  the attacker's own body multipliers, so the host has no table to compare against and no way to reproduce the number
  without simulating the gesture; carrying the item's identity would bound the claim's SOURCE only, never its
  magnitude. The full reading, including the answered frame question the earlier text left open, is "The damaging
  item's 'damage table' is code, not data" above.
- **`NetMsg.BuildingEntityOpened` (52)** 鈥?same shape: `entity.health = 0f;` after a presence test only. The
  lockpick and keypad minigames run on the opener's own client, so the open is its result. Half the anchor
  already exists host-side: the keypad code is the host's (`NetMsg.KeypadCode` = 57), and the entity's own
  type says which lock it is; what is missing is a host-side model of the progress a lock requires.

  **The anchor half is CLOSED 2026-10-18 as well — no data anchor survives**: the keypad code turned out to be
  shipped to every client (the section below works through the tautology), and a tool claim cannot be falsified
  because the game's own `Openable.OnUse` clears the lockpick below INT 10 (`if (PlayerCamera.main.body.skills.INT <
  10) { item = null; }`), so empty-handed opening is ordinary play; the tool's id is also already cleared by the
  time the open fires. The minigame result stays the opener's own (decision 265), and the census row now says so.

  **The reach bound is the half that needs no model, and the chain it must change is traced (2026-10-18)** 鈥?  recorded because the bound costs one wire-shaped change, not a redesign: the report carries the position and
  NOT the reporter, so the host cannot compare the two. `BuildingEntityOpenedHandler` fires
  `ctx.World.FireBuildingEntityOpenedReceived(pos)`, `WorldStateMessageService.FireBuildingEntityOpenedReceived`
  invokes `BuildingEntityOpenedReceived?.Invoke(pos)` on a `Action<NetVector2>` that `IWorldControl` declares
  without a sender, and `WorldEventSync.OnRemoteBuildingEntityOpenedRelay` hands it straight to
  `WorldBuildingEntitySync.OnRemoteBuildingEntityOpened`, which writes the health. Note the send side already has
  the same shape for `BlockPlaced`, whose handler DOES thread the sender 鈥?that is the pattern to copy, not a new
  mechanism. So the cycle is: carry the sender through the handler, the event and the relay; in the sync, look the
  opener's body position up the way the character-data bound already does, refuse beyond the interaction reach
  with the name of the sender, the position and the distance, and keep applying an in-reach open. The minigame
  itself stays the opener's (a lock's progress is not reproducible host-side); this row bounds WHERE, not HOW.
- **`NetMsg.TraderAction` (73)** 鈥?the trader is re-located from the reported position and the stock side is
  re-derived (`TradeStockMachine.TryPurchase` / `.TryGiveItem`), but the item id, the price, the reputation
  offsets and the flags ride the payload. The host holds the trader's own state, so the action can be
  re-priced host-side instead of trusted. RE-JUDGED 2026-10-17 鈥?this row belongs with section C, not
  here, and the reading is the decompiled tree: the PURCHASE half is bound already (the host prices its own
  stock entry, `TradeExecutor.ExecutePurchase`), while the GIVE half's number is
  `min(item.Stats.GetValue(item), MAX_VALUE_GIVEN - totalValueGiven)` (`TraderScript.cs`, `GiveItem`) and
  `ItemInfo.GetValue` reads the INSTANCE 鈥?`item.condition`, a magazine's `rounds/maxRounds`, a
  container's liquid `stack` 鈥?so a type id cannot reproduce it. The anchor this row needs is the item
  INSTANCE's identity (or its state) and the wire carries only the type id; the cap half is already
  aligned (`TradeStockMachine.TryGiveItem` clamps to the remaining headroom, as the game does). The
  reputation chain and the flags are the acting player's OWN body state, which the message documents as
  computed there, so they are not this row's business either.

  **IMPLEMENTED 2026-10-18 — the GIVE half's number is the host's own, and the machine reading is owed.**
  `TraderActionMsg.ItemInstanceId` names the handed-over item (read in `TraderPatches.GiveItemPatch`'s Prefix,
  because the native method destroys the object), and the host judges the claim in `TradeGiveJudge`: the
  instance must be a judged row, live, and the sender's own by the container CHAIN
  (`ItemLocationChain.HolderOf`) or world content; its worth is `ItemInfo.GetValue`'s arithmetic over the row's
  recorded state (`TradeStockMachine.GiveItemValue`, the game's own `value`/`valuePerLiter` tables read in
  `TradeExecutor`), and the claim is credited as it stands but never above that number. A claim above the host's
  number is CLAMPED and ANSWERED (the reject marker rides the authoritative overwrite) instead of refused: the
  direction that matters is an unrecorded GAIN (repair, refill, reload) — the host's row is then lower than the
  item the giver held, and refusing would destroy a legitimate hand-in over timing; decay over-states the row, so
  the claim sits below it and nothing is clamped. Unjudged instances, another member's items, terminal rows,
  installed limb items, unknown liquids and a repeat give of the same instance are refused by name.
  The clamp's first draft exposed an identity gap and it was closed at the ROOT instead of papered over: a
  generation-time container's contents are now named at the publish (`GeneratedItemAuthority.NameContainerContents`)
  and get kernel rows through the same container-sync command a member's report maps to
  (`ItemService.PublishGeneratedItems`), and the carried-set statement names a carried container's contents
  (`CarriedInventoryReporter.EnsureContentsAreNamed`) — so a give from inside a generated container names a
  judged item, not an id the host never saw.

  Self-check (mechanism × change × evidence):

  | Mechanism | Change | Evidence |
  |---|---|---|
  | The give's claimed credit | judged against the host's own row for the named instance; credited as claimed but clamped to the row's own worth | `TradeGiveJudgeTests` (16 facts) + `TradeStockMachineTests` (formula and clamp); mutation: clamp neutralized → 5 failed |
  | Whose item the reporter may speak for | the chain's holder or world content; another member's row, a terminal row and an installed limb item are refused by name | `TradeGiveJudgeTests`; mutation: ownership arm neutralized → 3 failed |
  | One instance, one credit | a per-session receipt set; a repeat is refused | `TradeGiveJudgeTests.SecondGiveOfTheSameInstance_IsRefused`, `TradeSimulationTests.GiveItem_SameInstanceTwice_CreditsOnce`; mutation: repeat arm neutralized → 2 failed |
  | The state a give can run into | a clamped claim is answered (reject marker) and converges; a refusal credits nothing; the guest reads the applied credit versus its local total | `TradeSimulationTests` (4 give paths through the real judge) |
  | A generated container's contents | instance ids at the generation publish, one kernel row each (contained) | `ItemSnapshotSimulationTests.GenerationSnapshot_AWorldContainersContents_LandAsContainedRows`; the naming itself is Unity-typed (generation) and is read on the machine |
  | A carried container's contents | ids at the carried-set statement; the host adopts them as the member's contained rows | `CarriedInventoryRegistrationTests.ContainedChildren_WithInstanceIds_LandAsTheMembersOwnRows`; the capture is Unity-typed and is read on the machine |
  | The acceptance lever | `/traderreport` sends one give report; the cheat family's gate decides who may run it | `AcceptanceTraderReportTests` (open, closed switch, host role, non-finite position) |

  NAMED as left open: the item's recorded state is itself the holder's earlier report (condition, rounds,
  liquids), so the bound forces the give claim to agree with the state that member already stated rather than
  proving that state; the native `!item.GetComponent<BoughtItem>()` guard is not mirrored (the kernel payload
  carries no marker, so a bought item can be handed back at its own value, which the native method refuses); a
  `World` row proves the item EXISTS, never that the reporter held it — the native gesture's own gate is a
  distance/line check, behaviour, out of scope by decision 265; and the Unity-typed halves (the Prefix id read,
  the game-table value, the generation/carried naming) have no unit test — the machine reading is their evidence.

  **READ ON THE MACHINE 2026-10-18 (batch `20261018-z`, two clients, deployed `+ad5d3bc7`) — the four owed
  rows PASS** (`docs/evidence/acceptance/member-report-bounds-trader-give-20261018-z.md`):
  (1) a real hand-in — the member's own client ran the native `TraderScript.GiveItem` over a world item the run
  had created, `bandage` instance `9492956821`, condition 1 — drew the host's own number,
  `[Trade] <member>'s give credited 10 (bandage (#9492956821, World, condition 1) is worth 10 on the host's
  record; the report claimed 10).` with `acceptedAsClaimed=True`, and both clients converged on
  `valueGiven=10 totalValueGiven=10`;
  (2) a `/traderreport` claim of 500 on the carried `emergencylight` instance `5197989525` (the host's record
  makes it worth 8) drew the clamp — `claimed more than the host's own record makes the item worth — … is worth
  8 on the host's record; the report claimed 500. Credited 8, the answer rides the state broadcast.`,
  `acceptedAsClaimed=False` — and the member settled on the applied 8 (`local totalValueGiven 10,
  authoritative 18`), both sides then reading 18;
  (3) an unjudged id (`999999999`) and a repeat of the credited id were each refused BY NAME (`the host has
  never judged item #999999999`, `item #5197989525 was already given to a trader in this session`),
  `acceptedAsClaimed=False`, `applied 0`, and the totals stayed 18 on both sides;
  (4) the unmodified stretch — the host's first trade line to the first forged step, 67 s carrying eleven
  snapshot rounds — held zero refusals, and the whole session carries exactly the two the run provoked, with no
  `trader not found` warning.
  The lever and the machine are what the run used (`/hostrules` + `/op`, then `/traderreport`); the run's own
  limits — the hand-in was the native method invoked on the member's client rather than a walk to the trader and
  a trade-UI drag (the native method has no distance gate and reach is out of scope by decision 265, and the body
  stood 271.5 world units away), and the clamp's "unrecorded GAIN" direction was not exercised — are named in
  the record.
- **`NetMsg.RecipeUnlock` (77)** 鈥?`RecipeUnlockReceived?.Invoke(recipeIndex);` applies and the handler then
  relays, with no range check against the host's own recipe table (the SNAPSHOT path's `known` set answers a
  different question 鈥?whether the host had already LEARNED the index, never whether its table HAS one).
  index must be one entry of THIS host's table (`CraftSyncService.MayApplyReportedUnlock`, pinned by
  `RecipeUnlockBackfillTests`); an index it has no recipe for is refused by name 鈥?nothing applied here,
  nothing relayed 鈥?while the host's own unlock, every guest-side apply and a read that FAILED keep the
  behaviour they had. The machine reading is `docs/evidence/acceptance/recipe-unlock-bound-20261017-b.md` (batch `20261017-b`, three clients): the refusal names the sender, the index and the table's own 132 entries, and a real index still unlocks in the same session.
- **`NetMsg.BlockPlaced` 鈥?an air write, the break carrier's cell (found by batch `20261016-c`)** 鈥?the census
  row's own bound (`the generation stamp + the first-writer cell rule`) covers the layer and the cell's state,
  never the REPORTER: `WorldEventSync.OnRemoteBlockPlaced` reads the host's own cell
  (`standingBlock = WorldGeneration.world.GetBlock(pos)`), compares it with the claimed write and applies, with
  no comparison against the sender's own body anywhere on the path 鈥?so a member naming any cell of the current
  layer has that block broken there. The anchor the host already holds is the member's own body position; what
  has to be settled BEFORE a check is whether every legitimate writer stays inside a reach (`Body.Attack`'s
  melee write is the native call site, but a quake breaks a whole region and an environment write is nobody's
  gesture) or whether the anchor has to be the cell's own cause instead. Live reading: `20261016-c` had the
  member break a cell 122 blocks from its own cell as that run read the body, and the host applied the break and
  judged its drops (`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`).

  **DECIDED by the user 2026-10-18 鈥?bound the GESTURE only.** The question was what an out-of-reach write
  should do, and the user asked first whether moving the quake to host-side authority would change play. It would
  not, because the quake is ALREADY host-authoritative: `WorldGenerationUpdatePatch` records that the quake timer
  runs on every side while the host broadcasts the start and **the guest's own timer is frozen**, and the patch
  bridge's own gate says only quake breaks are gated while environment breaks pass. So the user's answer is to
  bound the gesture and leave the quake and environment writes exactly as they are 鈥?with the confirmation that
  the gesture bound changes no legitimate play.

  **The split is already on the wire**: `OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block, bool
  playerBreak, WorldGenerationRelation generation)` carries `playerBreak`, so the check belongs beside the
  generation refusal, inside the host branch, on `playerBreak` writes only. The anchor is the one the visibility
  bound already uses 鈥?`CharacterDataMsg.Position` as `PlayerInteractionVisibility.TryGetPosition` reads it 鈥?and
  the reach constant is chosen once and shared with the `BuildingEntityOpened` row below, so a hand that can pick a
  lock is also a hand that can break the wall beside it.

  **LANDED 2026-10-18** (`6bdf1e36`, deployed and verified `+6bdf1e36`): `InteractionReach.Cells` holds the one
  number, `WorldEventSync` takes `IEntitySyncControl` for the reporter's position, and a `playerBreak` air write
  further than that reach is refused before anything is applied, recorded or relayed. An absent position is not a
  refusal. Gates 668 and the behaviour suite are green; quake and environment writes carry `playerBreak: false` and
  never enter the branch.

  **The live row is the only thing left, and it is one lever short**: driving it needs a client to send
  `NetMsg.BlockPlaced` for a far cell, and the acceptance vocabulary has no way to reach it 鈥?`PatchBridge` exposes
  no world control (`Impl`, `ModContent`, `SessionSurface`, the patch ports only), and the send lives on
  `IWorldControl.SendBlockPlacedReport(x, y, block, playerBreak)`. So the next run needs ONE of: a world-control
  accessor on the bridge for a recipe, or a console verb that reports a break. Until then the row is
  implemented-but-unverified and the ticket stays in `todo/`.

  **READ ON THE MACHINE 2026-10-18 (`4b852aa3`, one live session) 鈥?the row PASSES.** The lever is a console verb,
  `/blockreport <dx> <dy> <block> <playerBreak>`, which registers itself through the same attribute scan the other
  command owners use (an unrequested singleton is never constructed, so the registry builds it explicitly). Driven
  from the MEMBER client, two reports one after the other:

  - `200 0 0 true` 鈫?the host answered with the new refusal:
    `[BlockSync] <member>'s player break at (200,486) is further than 8 cells from its own body 鈥?refused: nothing
    applied, nothing recorded, nothing relayed.`
  - `1 0 0 true` 鈫?no refusal, and the write went through the ordinary path (the host reads its own cell, applies,
    and relays).

  So the bound does what the user decided: a hand that cannot reach the cell is refused with a line, a hand that can
  keeps working, and quake/environment writes (`playerBreak=false`) never enter the branch at all.

  **The `BuildingEntityOpened` half landed on the same number and was read the same way (2026-10-18, `5168fe78`,
  deployed `+5168fe78`).** An OPEN is a lock being worked, so it is a gesture: the sender now travels the whole path
  (packet handler 鈫?world event 鈫?service passthrough 鈫?relay) and the host refuses an open further than the shared
  reach, judged on the opener's own body. Driven from the member client: `/entityopen 300 0` produced
  `[EntitySync] <member>'s open at (300,504) is further than 8 cells from its own body 鈥?refused: nothing applied,
  nothing relayed`, while `/entityopen 1 0` met no refusal and took the ordinary path. A guest applying the host's
  broadcast does not judge 鈥?it is applying a fact already judged.

  **Refused-by-gate, recorded rather than smuggled:** `WorldEventSync` had been sitting at exactly the 600-line cap,
  so this bound pushed it over. Moving the helper into its own part did NOT help 鈥?the gate counts a type, not a
  file 鈥?so the class is now recorded in `docs/architecture-debt.json` with its real size. The split it deserves is
  its own task: the file keeps accreting bounds and one more will make this a real architectural problem.

  **The accessor cannot be a plain bridge member** 鈥?found by reading the interface's own contract before editing
  it: `IPatchBridge`'s doc states that a domain-specific member belongs on its OWN port ("declares a per-domain
  port instead 鈥?`IFluidPatchPort` is the first 鈥?) because the shape gate *"fails on a member added here or on
  the implementation"* (`PatchBridgePortShapeGateTests` plus the reflective `PatchBridgePortContractTests`). So the
  lever is a new narrow port 鈥?one member, `SendBlockPlacedReport`, implemented by
  `GameAdapterBridge` and surfaced as `PatchBridge.WorldReports => _bound as IWorldReportPatchPort` 鈥?exactly the
  shape `Fluid`/`LayerAdvance`/`Wearable` already use. Adding it will also ask those two gates to be told about the
  new port, which is the first thing to check when that cycle starts.

  **Attempted and REVERTED 2026-10-18 鈥?the port route costs more than it looks.** The port itself is four small
  edits and the bridge compiles, but the shape gate's port table ALSO feeds the aggregate census: registering the
  new port there makes `Aggregate_DeclaresExactlyTheFrozenCensus` expect the port's member among the AGGREGATE's
  members, which the aggregate deliberately does not have. That is a gate-shape question (a port's member has to
  stay out of the aggregate's pin), not a product question, and answering it must not be smuggled into an
  acceptance-lever cycle. Reverted to `f521e633` with the tree green; the work is parked here. **The cheap second
  road is still open and is what the next attempt should take first**: an acceptance-only console verb that reports
  one block write, which touches no interop seam at all.

  **The console road, surveyed (2026-10-18)** so the next attempt starts on it: registration is attribute-based 鈥?  `ConsoleCommandRegistry.AddBuiltIns(owner)` scans the owner's methods for `ConsoleCommandAttribute` and registers
  them 鈥?so the verb is ONE method on whichever class already owns the built-ins, calling
  `IWorldControl.SendBlockPlacedReport(x, y, block, playerBreak)`. The one question to answer before writing it is
  the PERMISSION: the verb has to run on a MEMBER client (that is the reporter side under test), and a member-side
  verb that writes world state is exactly what the member-write policy (`AllowMemberCheatVerbs`, `Forbids`) has
  an opinion about. Decide that first 鈥?either the verb is admitted for members because it only publishes a report
  that the host still bounds, or it is host-only and the reading has to be taken with the HOST as the reporter,
  which changes what the row proves.

  **One correction to the reasoning above, and one dependency the check needs (2026-10-18).** The quake's breaks
  are NOT host-only: `WorldEventSync`'s own quake comment records that "every side shakes together and breaks its
  own nearby region; the regions union via the air-write relay". So a quake break IS a member report 鈥?and it is
  exactly why the `playerBreak` flag is the right split rather than a special case: a player break is reported with
  `playerBreak: true` (`BlockBreakSync` applies and broadcasts it that way), while a recovery replay states
  `PlayerBreak = false` and the pending-report record defaults to false. Quake and environment writes therefore
  never enter the branch this bound adds, which is what the user's decision rests on.

  **What blocks the implementation is one dependency, not the design**: `WorldEventSync` holds only
  `ISessionControl`, `IWorldControl`, `BlockBreakSync`, `RestoredWorldFactReplay`, `OperationTrace`,
  `WorldEntityKernelProjection`, `WorldEntryFanout` and the logger 鈥?no position source. The reach check needs the
  sender's body position (`PlayerInteractionVisibility` gets it from the entity stream), so this cycle is: add that
  narrow port to `WorldEventSync`'s constructor and its DI registration, then the check itself, then the
  `BuildingEntityOpened` row on the same constant.

## C. A claim with no anchor (a design decision, not a check)

- **`WireCommandKind.ItemSpawn`** 鈥?the item-creation half named in S2a's own limits: the report names an
  item nobody has seen and carries no creating cause. A bound needs an anchor the wire does not have (the
  consuming use, the container it came out of, the craft that produced it) 鈥?the same decision the spawn
  family already owes.
- **`NetMsg.EntityEvent` (66)** — LANDED 2026-10-18. The TRIGGER stays the reporter's fact: the host applies
  it to its own world, and whether the trap was really tripped is not a claim the host can check by data
  (decision 265). The DROPS riding a DESTRUCTIVE event are bounded by the host's OWN application of that
  event, which is this row's anchor answer: the capture point is the host's explosion diff
  (`TrapBuildingHealthScope` reads `GameBuildingDropTable.ForEntity` for every entity its own blast damaged,
  while the entity still exists) plus the trap entity's own table read before the apply
  (`TrapEffectApplier.FindTrap<BuildingEntity>` + `GameBuildingDropTable.ForEntity`, the applier's own
  position-keyed identity rule rather than a collider query). The trap entity is EXCLUDED from the scope's
  second read (`TrapBuildingHealthScope.Begin(trapEntity)`): an explosion damages it too, and one row is one
  roll, so the same table must not reach the bound twice. `TrapDropBoundJudge` refuses by name every drop no
  row can account for (`ItemRejectMsg.Reason.TrapDropUnaccounted`), answers the reporter (which destroys its
  local copy), and relays only the accepted set. A kind outside the building-death carrier's profile
  (`TrapDamageProfiles`: the four destructive kinds) refuses every drop it carries; the wording is
  deliberately not "a kind that destroys nothing", because `CrystalMetamorphicTriggered` destroys its crystal
  and rolls its own drops while staying outside this carrier (they are an item-domain fact). The per-entity
  identity the row asked about is NOT needed and is NOT invented: the message carries none, and the rows are
  matched as a set (`DropBoundPolicy`), so the bound decides whether the event's own tables can account for
  the set, never which entity dropped what. This row does NOT change who owns a death: the host's own
  application of a member's blast still kills its own copies of the blast's victims natively and rolls a
  second set for them — a separate ownership defect, found by this cycle's family audit and filed as
  `todo/host-blast-rolls-a-second-drop-set.md` (the direct-blast mark was attempted and withdrawn because a
  chain explosion runs outside the apply scope and the per-side random damage can make a death host-only).
  Evidence: `TrapDropBoundJudgeTests` (five cases; RED recorded three times by neutralizing the bound, the
  kind arm and the reason arm) — the capture and the caller's use of the returned list are Unity-typed and
  read by the acceptance batch. Acceptance owed: a two-client session, a member's destructive event with a
  drop its tables cannot produce refused by name on the host and destroyed on the reporter, an ordinary
  event's drops landing whole, a fragile-crystal break (that kind's only anchor is the trap-table read, so
  it must be read on the machine), and no refusal line in an unmodified session. DEPENDENCY named: the
  refusal row needs a lever that can send a forged `EntityEventMsg` with a hand-built drop list — no
  acceptance recipe constructs one today (the block family's `block-drop-forge` builds `TrapDropEntryMsg`
  inside `SendBlockDamaged`), so that lever is part of the acceptance batch, not of this code change.

  Self-check (mechanism × change × evidence):

  | Mechanism | Change | Evidence |
  |---|---|---|
  | The reported drop set of a destructive event | bounded against the host's own application: the tables of the entities its blast damaged (`TrapBuildingHealthScope.DropRows`, read while the components still exist) plus the trap entity's own table (read before the apply, and excluded from the scope so it counts once) | `TrapDropBoundJudgeTests`, five cases; RED recorded by neutralizing the partition (3 failed), the kind arm (1) and the reason arm (1) |
  | The answer to the reporter | a refused drop is answered with `ItemRejectMsg.Reason.TrapDropUnaccounted`; the reporter destroys its local copy | `Bound_KeepsWhatTheApplicationsTablesCanProduce_AndAnswersTheRest` observes the reason on the guest; `ItemApplication.OnItemRejected` handles it in the same destroy-the-local-copy arm as a refused break drop |
  | The reason's two mappings | `RejectionReason.TrapDropUnaccounted` ↔ `ItemRejectMsg.Reason.TrapDropUnaccounted` (host → wire → guest) | the same test drives the host judge, the real wire and the guest's `ItemRejected` surfacing |
  | A kind outside the building-death carrier | cannot carry a drop: every one is refused by name and the kind is named in the line | `Bound_AKindOutsideTheBuildingDeathCarrier_AnswersEveryDrop` |
  | The trap table's single count | the entity whose table the caller read before the apply is excluded from the scope's second read | `TrapBuildingHealthScope.Begin(trapEntity)` + `ReferenceEquals`; Unity-typed, named for the acceptance batch rather than unit-tested |
  | The row set being empty | every reported drop is refused (the safe direction: a report cannot introduce an entity's drops where this host accounted for none) | `Bound_NoTableAccountsForTheSet_AnswersEveryDrop` |

  **READ ON THE MACHINE 2026-10-18 (batch `20261019-a`, two clients, deployed `+c909fd3d`): row 1 PASSES, row 4
  PASSES, row 3 is UNPROVEN, and row 2 FAILS** (`docs/evidence/acceptance/member-report-bounds-entity-event-drops-20261019-a.md`).
  The lever the row owed now exists: `trap-event-forge` sends one hand-built drop inside a real
  `EntityEventMsg`, `trap-drop-census` reads the destructive carriers with the entity's own table and the
  entity the host's lookup resolves, and `trap-crystal-break` drives the game's own fragile-crystal touch. The
  forged report at a summoned mine drew the TABLE arm — `claimed 1 drop(s) the 1 row(s) … cannot produce —
  refused by name (bandage@0.50), 0 drop(s) accepted and relayed` — and the answer destroyed the member's copy
  (`[ItemReject] … destroying the local copy (TrapDropUnaccounted)`). The real event's drop folded correctly on
  the member (`[ItemTrapDrop] … captured`, `[TrapDropFlush] … drops=1`) and the host applied the event and
  materialized the drop, but the authoritative composite was REJECTED: `record-building-health rejected:
  InvariantViolation (… invalid health -751.9034)` and then `wire-command rejected: InvariantViolation (…
  -900.2673)`. The kernel's `WorldEntityDomainModule` validation refuses any building health below zero, and
  this path feeds it the game's raw float: `EntityEventSync.ReadDestroyedTrapHealth` returns the trap's
  post-blast health (negative after the applier's own explosion) and `TrapBuildingHealthScope` the same for
  every blast-damaged entity — both inside the ONE composite that also carries the consumption and the drops.
  The accepted drop therefore exists only as the host's presentation, never as a kernel row
  (`container-read mode=host` has no `13787924117`), so a save or rejoin loses it. The same clamp gap is one
  channel over: the explosion's building-damage report drew `record-building-health rejected … invalid health
  -295.9813` in the same session. **Next step for this row**: one clamp of the physical health where it becomes
  a kernel fact (0 = destroyed) for the trap batch AND the damage report, with a red test on both, then re-run
  this row. The fragile-crystal row could not be read at all: this run's world carries no `CrystalBehaviour`
  (nine and thirteen crystal-prefab buildings on layers 0 and 1, zero effect components), so no fragile crystal
  exists to break; the run descended one layer and re-read rather than assume.
- **`WireCommandKind.ItemContainerSync`'s creations 鈥?IMPLEMENTED 2026-10-17, READ ON THE MACHINE
  2026-10-17 (batch `20261017-d`) 鈥?PASSES.**
  The sibling S3a left: the same claim travelled two more ways 鈥?a parent this host never judged
  (materialized as the reporter's own carried item by `ItemDomainModule.DecideSyncContainer`'s
  missing-parent arm) and each listed child it did not know (created as a contained row under the parent
  THAT REPORT names, `child.ParentItemId`, which need not be the sync's own parent id). Both are bounded in
  code; this row is the two-client session that reads them, and it has been run:
  `docs/evidence/acceptance/member-report-bounds-container-sync-20261017-d.md`. Both creation arms were
  driven on the machine 鈥?an unjudged PARENT with an unjudged child, and a KNOWN parent with an unjudged
  child 鈥?and each read the host's by-name refusal, its answer, the member's immediate re-statement, the
  re-stated set landing the container and its content with the state they carried, and an ordinary session
  that meets neither refusal.

  THE DESTINATION ARMS need no statement, and they are what the report can actually WRITE 鈥?behind the
  seam, the kernel rewrites the parent, moves every listed child to `child.ParentItemId` verbatim, and
  destroys the descendants the report omits. Three rows are judged against the host's own record, and the
  row a child sits in or is being moved to must be the sender's: the parent (naming a world container
  rewrote its data and relocated listed world rows into the sender's own container; a load that lands in
  the world reports a drop, so no production sender names one), each child's named destination, and a known
  child's current container. The membership test is the family's own, `Location.Owner.Value == sender`: a
  nested container is recorded as CONTAINED with that owner, never as carried, and the CHILD's own owner is
  never the test (a container that changed hands leaves its old contents recorded under the previous
  holder). The destroy half walks CONTAINED rows only and a world container's real contents are WORLD rows,
  so one report cannot reach them; it needs a prior report that converted them 鈥?which these same arms
  refuse.

  THE STATEMENT ARM is the state report's bound on this kind: once
  `ICarriedRegistrationFacts.HasJudgedCarriedSet(sender)` holds, an unjudged parent or an unjudged child is
  refused BY NAME (`RejectionReason.UnclaimedItem`) and ANSWERED, nothing is applied, and the member's way
  back is re-stating the set 鈥?the statement carries a carried container's contents recursively, so the
  re-stated set lands the container and what is inside it. On the client, `ItemService.OnCommandRejected`
  settles EVERY refused container report by asking for that re-statement (never the pickup rollback, which
  would unparent a live container into the world while the host still records it as the member's).

  EVIDENCE (unit level plus the machine row below): `CreationBeforeOperationTests` gains twelve cases, of
  which TEN were observed RED 鈥?four in the first run, before any of this code existed (the refusal frames
  absent; a forged child created inside another member's container; nothing landing after a refusal), and
  six by mutation control in the finished tree (the parent arm neutralised 鈫?the world row's data is
  rewritten and its staged content destroyed; the destination arm neutralised 鈫?the child is hung under the
  peer's row; the destination arm narrowed to "carried" 鈫?the member's own nested container is refused; the
  child-home arm neutralised 鈫?the peer's contained item is moved; the child's OWN owner used as the test 鈫?  the container-changed-hands case is refused). The two remaining cases are guards that assert today's
  behaviour: the pre-statement parent is still materialized and the pre-statement nested tree still lands.
  Style: measured after the change, `KernelProtocolCommandHandler` 565 and `ItemService` 595 lines, both
  under the 600 gate; a further growth of either owes the split.

  **Read on the real machine 2026-10-17, batch `20261017-d`**
  (`docs/evidence/acceptance/member-report-bounds-container-sync-20261017-d.md`): both creation arms PASS end to
  end with two clients. Arm 1, an unjudged parent AND an unjudged child: the member's own container-load report
  (`[ContainerLoad] waterbottle (id 13787924117) moved inside body container trashbag 鈥?root content event up to
  trashbag (id 9492956821)`) drew the host's by-name refusal (`Container sync from <member> refused: container
  9492956821 was never judged, and that member's carried-set statement already was 鈥?鈥), its answer
  (`ItemContainerSync refused (UnclaimedItem)`), the member's own settlement line and `the next carried-set report
  is due now`, and the re-statement 10 ms later (`re-reported 2 carried item(s)`) landed the pair on the host 鈥?  kernel `Carried` / `Contained` rows under that member carrying the state the statement captured (`liquid
  water=500`, `condition 1`) 鈥?while the reporting client's own tree survived intact. Arm 2, a KNOWN parent with
  an unjudged child: the same round trip through the child branch's own message (`content item 18082891413 of
  container 9492956821 was never judged`), its re-statement landing the child under the judged bag, after which
  the kernel's `contained` half carries both children. The ordinary session reads ZERO of these refusals before
  the first stage and zero in the stretch after each settlement, and the whole session carries exactly the two
  the run provoked 鈥?no third line, and no protocol-violation refusal at all.

  What the row still does NOT close: a member that never states a carried set keeps the pre-change behaviour
  (the `ItemSpawn` class); and a
  NESTED container whose interior row still records a previous holder is refused by the destination and
  home arms with no recovery, because the re-stated set's `AdoptCarriedContents` stops at a row the kernel
  does not record as the member's own. That shape is not producible through the game's own paths today
  (`Container.LoadItem` refuses loading a container that is itself inside a container and refuses loading a
  non-empty container into one), and today's container-load report flattens to the outermost carried root;
  it is named here because the arms judge the row a child sits in, and a future container-in-container path
  would hit it.

  Self-check (mechanism 脳 change 脳 evidence):

  | Mechanism | Change | Evidence |
  |---|---|---|
  | The parent a report can rewrite and whose omitted contained descendants it destroys (`DecideSyncContainer`) | the parent must be the sender's own row when the host holds it 鈥?a world parent is refused before anything is applied, so its data is not rewritten and a listed world row is not relocated | `AContainerSyncMayNotRewriteAWorldContainer` and `AContainerSyncMayNotDestroyAWorldContainersContents` 鈥?both RED with the arm neutralised |
  | Each listed child's destination (`ItemLocation.Contained(command.Actor, child.ParentItemId)`) | the destination must be the sender's own row or one the report creates | `AContainerSyncMayNotHangAChildUnderAnotherMembersRow` 鈥?RED with the arm neutralised; `AContainerSync_BeforeTheCarriedSetWasStated_StillAddsAChildToTheMembersOwnNestedContainer` 鈥?RED with the arm narrowed to "carried" |
  | A known child the record holds inside somebody else's container | the row the child sits in must be the sender's own (world-rooted excluded); never the child's own recorded owner | `AContainerSyncMayNotMoveAnotherMembersContainedItem` 鈥?RED with the arm neutralised; `AContainerSync_AfterTheContainerChangedHands_StillAcceptsTheRecordedContents` 鈥?RED with the child's own owner used as the test |
  | The creation claim (`ICarriedRegistrationFacts.HasJudgedCarriedSet`) | an unjudged parent or child after the statement was judged is refused by name and answered | `AContainerSyncForAnUnjudgedParent_AfterTheCarriedSetWasStated_IsRefusedAndNothingIsMaterialized` and `AContainerSyncForAnUnjudgedChild_AfterTheCarriedSetWasStated_IsRefused` 鈥?both RED in the first run; `..._BeforeTheCarriedSetWasStated_IsStillMaterialized` is the window's guard |
  | The settlement (`ItemService.OnCommandRejected`) | every refused container report settles by the carried-set statement, never by the pickup rollback that unparents a live container | `ARefusedContainerSync_ReStatesTheCarriedSet_AndTheContentsLand` 鈥?RED in the first run |

## Acceptance each row owes

A two-client session where one client is modified by hand or by a harness, one row per surface: the forged
report is refused (or ignored, where the family ignores) BY NAME on the host, the forged side is answered
or converged, the host's own record is unchanged, and an unmodified session still meets no refusal. The row
template and the evidence shape are `docs/evidence/acceptance/validation-and-source-checks-s2a-20261016.md`
and the batch workflow in `docs/acceptance/`.

## Non-goals

- No bans, kicks or scoreboards: a check answers the report (the user's 2026-10-14 call).
- Not a second simulation: nothing here re-runs another client's frame, and no bound may refuse a
  legitimate roll (the S2a evidence is the worked case for that limit).

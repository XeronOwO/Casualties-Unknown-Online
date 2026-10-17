# The member reports that still have no bound

- Status: Todo — opened 2026-10-16 by the survey behind `docs/architecture/member-report-validation.md`
  (every row of its census marked `unbounded:` points here). S2b landed the block-break carrier's building
  drops; this ticket is the rest of the same question, one surface per row, each with its own evidence.
  Section A (the reports that name another member's identity) CLOSED 2026-10-17 — its five surfaces now
  compare against the transport sender, and the census rows say so. Sections B and C remain open.
- Priority: Medium-High
- Category: Security / report validation
- Source: the user's 2026-10-14 request (basic checks from the game's own mechanics, never an anti-cheat)
  and the 2026-10-16 direction that the validation standard becomes a general development rule — which is
  also why these are written down instead of left implicit.
- Related: `docs/architecture/member-report-validation.md` (the census these rows come from),
  `todo/validation-and-source-checks.md` (S1/S2a/S2b landed the seam, the block drops and the building
  drops), `docs/evidence/sync-coverage-matrix.md` (each surface's event + fallback policy)

## The three patterns

Three shapes, and the fix direction is the same within each: an identity that is already known to the
host, replaced by the payload's claim; a number the host could derive, taken from the report; and a claim
with no anchor at all, which needs a design decision rather than a check.

## The three reach bounds were LANDED, READ "PASSING", AND THEN REVERTED (2026-10-18)

Everything this ticket's B section says about the reach bound above — the three commits, the two machine readings,
the "PASSES" verdicts — is **withdrawn**: `fd2d2b3f` reverts `6bdf1e36`, `5168fe78`, `1a651ddc` and the two
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
   through `BlockDamaged` (`BlockBreakSync.FlushPendingBlockBreak` → `SendBlockDamaged`), which
   `BlockBreakArbitration` accepts while the cell still stands and the generation verifies — so a refused air write
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
- **Whether a refusal can suppress the relay** — if the verdict cannot travel back to the handler, no bound on these
  paths is honest.
- **World coordinates everywhere.** One helper, world space only, converting block cells with `WorldToBlockPos` at the
  single place a cell becomes a point.

Until that, these rows are open and the census rows for them must say so rather than "closed".

### The design the next cycle implements (decided 2026-10-18, from the review's four questions)

**1. The break belongs to `NetMsg.BlockDamaged`, not to the air write.** The air write is what makes a cell empty;
`BlockBreakSync.FlushPendingBlockBreak` → `_world.SendBlockDamaged(...)` is what actually lands the break and carries
the drops, and `BlockBreakArbitration` accepts it while the generation verifies and the cell still stands. So the
reach check moves to the damaged path — and the air write keeps only the bound it already has (generation + cell
state), because a bound on a message that does not decide the outcome is decoration.

**2. The cause must be something the HOST can prove, and it has two cases.** A report that claims a player gesture
(`playerBreak: true`) must come from a body within reach. A report that claims an environment/quake write must be
matched against the host's own quake state — the same predicate the adapter already uses,
`WorldGeneration.world.earthquakeTime` in `GameAdapterBridge`'s set-block gate, where a quake break is recognised and
an environment break passes. No quake running on the host → that claim is refused BY NAME (`"no quake is running on
the host"`), which is exactly what rule 16 asks for: the receiver checks a claim against what it can establish itself.
The alternative — trusting the reporter's `playerBreak` flag — is what the withdrawn attempt did and must not come
back.

**3. A refusal has to be able to STOP the relay, and today it cannot.** Both entity handlers
(`BuildingEntityDamagedHandler`, `BuildingEntityOpenedHandler`) fire the event and then broadcast unconditionally, so
a refused report is still applied by every other guest. The verdict must come back from the fire call — `bool` is
enough — and the handler relays only when it is true. The same shape applies to `BlockDamagedHandler`. Without this
step no bound on these paths is honest, so it is done FIRST, as its own verifiable change: a refused report must be
observably absent from the other client's log.

**4. One helper, world space only.** The block cell is converted where it is used, with the game's own
`WorldGeneration.WorldToBlockPos` (the repo's existing consumers call it: `GameAdapterBridge` does
`world.WorldToBlockPos(new Vector2(remote.Position.X, remote.Position.Y))`), never by subtracting an index from a
world coordinate. The entity paths already sit in world space and stay there. Two copies of the helper are acceptable
only while they are byte-identical in rule; the constants and the accept-on-missing-position rule live in one place.

**Order of the cycle**: (3) verdict, verified end to end on the machine → (1)+(4) the break bound with world-space
conversion and a red test that FAILS today with the real cell → (2) the quake anchor → then the entity rows on the
same verdict plumbing, with the explosion channel excluded by the host's own explosion record rather than by the
reporter's claim.

### Second review of the verdict plumbing (2026-10-18) — attempted, REVERTED, findings recorded in full

A first attempt at step (3) made the fire calls return a `bool` and relay only on true. The independent review found
two blockers and six majors, so it was reverted uncommitted rather than shipped. The findings ARE the spec for the
next attempt:

1. **BLOCKER — a refused open was still recorded as a durable host-side fact.** `FireBuildingEntityOpenedReceived`
   called `_eventChannel.ReportOpenedEntity(pos.X, pos.Y)` BEFORE invoking the handlers, and that record is durable:
   `OpenedEntityRegistry.Report` → `_kernelAuthority.TryRecordOpenedEntity(...)` → `WorldEntities.OpenedEntities`,
   written into every save (`WorldSnapshotEncoder`: `state.OpenedEntities.Select(SaveWorldEntityRow.OfOpenedEntity)`)
   and replayed for a late joiner (`WorldEntityKernelProjection`), where the adapter applies `entity.health = 0f`. So
   both new refusal cases left the kernel claiming the entity was opened and the log's "nothing applied" was false.
   **Fix**: invoke first, record only when the verdict is true.
2. **BLOCKER — the verdict rode a multicast `Func`, where only the LAST subscriber's answer survives.** A subscriber
   added after `WorldEventSync.BindToSession` could resurrect a refused open or mute a legitimate one, the adapter
   applies inside its own subscriber (so `true` never meant "may I apply"), and `?? true` failed open when nothing was
   bound. **Fix**: a single-consumer admission seam consulted BEFORE the apply event; keep the apply event `void`.
3. **MAJOR — the damage verdict was a constant.** `return true; // applied (or nothing stood there)` gated a handler
   that could never close, and the threaded `sender` was read by nobody — a mechanism with no consumer.
4. **MAJOR — a refusal was never answered.** The reporting guest had already opened the entity on its own copy
   natively, so refusing with no message left it open for the reporter and locked for everyone else — a permanent
   divergence, and the report rule requires an answer (the block path answers with `SendBlockPlacedCorrection`).
5. **MAJOR — "no entity there" silently stopped the relay** while that method's own summary said the relay ignores
   the value: two different refusals collapsed into one `bool`.
6. **MAJOR — the census would have contradicted the code**: `member-report-validation.md`'s `BuildingEntityOpened`
   row still said the bound "could not suppress the handler's relay", which the change made false; the gate cannot
   catch a stale sentence, so the page has to be rewritten in the same change.
7. **MAJOR — `WorldEventSync` landed at exactly 600 lines** with no debt entry, one line short of the gate; the fix
   is extracting the building-entity relay into its own type, not recording the debt again.
8. **MAJOR — the one new Runtime branch had no test**: every adapted subscriber returned `true`, and nothing
   subscribed to `BuildingEntityOpenedReceived` at all. **Fix**: host subscribes a `false` verdict, G1 sends, assert
   G2 received nothing (and that the host's checkpoint has no opened row); a `true` case asserts the relay.

Also noted clean by that review: the sender really is the reporter on the host and the host on a guest (so the
`Role == Host` gate is right), and the open path's coordinates are world-space on both sides — but `InteractionReach`
must be renamed to say world units, because a constant named "cells" compared against world coordinates is exactly
how the withdrawn block bound went wrong.

## DECISION 262 (2026-10-18, user's ruling): data validation only — the reach family is withdrawn for good

**Everything above that talks about a distance, a reach, "could this body have been there", the frozen constant, the
admission seam or the four-question design is CLOSED, not pending.** The user's ruling: CUO validates DATA, never
BEHAVIOUR. A report is checked against facts the receiver already holds — an identity it can compare, a value it already
computed, a table it owns. Modelling what a player is physically able to do (reach, aim, timing, cooldown, input rate)
is anti-cheat, which the MVP explicitly excludes, and it duplicates the game's rules in a second, weaker implementation
that refuses legitimate play whenever the two disagree.

What that means for this ticket, row by row:

- **`BlockPlaced`** — already bounded by DATA (the generation stamp + the first-writer cell rule). No reporter check is
  wanted; the row reads `bound` for the data half and says the reporter half is out of scope.
- **`BuildingEntityOpened` / `BuildingEntityDamaged`** — no data anchor exists yet: the open message carries no lock
  identity and the host has no model of lock progress; the damage message carries no weapon identity. Those are WIRE
  additions, and they are the only legitimate way to bound these two. Until then both rows stay `unbounded` and say so.
- **The verdict/suppress plumbing** that a refusal needs has no customer left, so it is not built speculatively.

What survives from the cycles above and is worth keeping when a data anchor arrives: a refusal must be answered
(otherwise the refused side keeps a state nobody else has), it must be asked BEFORE anything is applied or recorded
(the kernel's opened-entity table is saved and replayed to late joiners), and it must be able to stop the relay.
Those are properties of any future data bound, not of a reach bound.
### Native evidence for a DATA anchor on the open report (2026-10-18)

The open is decided by a hand-driven minigame on the opener's own client, so the host cannot reproduce it — and under
decision 262 it must not try. `reversing/Assembly-CSharp/Assembly-CSharp/LockpingMinigame.cs` is that minigame: its
`Update` accumulates `this.lockProgress += Time.deltaTime * (0.66f + num * 0.065f);` while the hand sits inside a band
(`this.clickingInside = this.HandAngle > 0f && this.HandAngle < 180f && this.HandDistance > 195f && this.HandDistance < 247f;`),
and completion is `this.toDestroy.health = 0f;`. Hand angle and distance are the opener's input; no host-side model can
judge them, which is exactly why a reach check was the wrong shape.

What the host CAN check, both of them data:

- **The item the gesture needs.** A lockpick is an inventory object; "the reporter carries the item this lock requires"
  is an identity claim the host holds the inventory to compare against. The message carries no item identity today, so
  this is a wire addition — the same shape the trader-give and building-damage rows need (weapon identity).
- **The code, for a keypad lock.** The host already owns keypad codes (`NetMsg.KeypadCode` = 57 is its own authority),
  so a report that carries the entered code can be compared against the record the host already holds — no new model,
  no behaviour simulation. This is the cheapest genuine anchor and the first one to build.

What neither anchor covers, and must not be faked: whether the minigame was actually played. That is behaviour, it is
anti-cheat, and it stays unbounded by ruling.
### The keypad anchor does not exist — the code is shipped to every client (2026-10-18)

Checked before building it, and the plan does not survive: the keypad code is HOST-OWNED but not HOST-ONLY.
`EntitySpawnSync` creates it on the host (`KeypadCodeTable.EnsureCode(openable) // the host creates it — its code is host
authority from the start`), carries it in the spawn report (`KeypadCode = KeypadCodeTable.EnsureCode(openable)`), and
every receiving client writes it into its own copy:

    if (msg.KeypadCode.Length > 0)
        Traverse.Create(openable).Field("code").SetValue(msg.KeypadCode);

So a code the opener "entered" is a value that client was already given. Comparing a reported code against the host's own
copy would always agree — it is not an anchor, it is a tautology, and building it would be exactly the kind of check that
looks defended and proves nothing.

What that leaves for this row, all of it honest:

- **The item anchor is the real one** and it needs the wire: "the opener carries the tool this lock requires" is an
  inventory identity claim the host holds the data to check. Same shape as the weapon identity the building-damage row
  needs, so both land in one protocol change.
- **The minigame result stays unbounded** (decision 262 — behaviour, not data).
- **The keypad code is not a bound at all**; it is a shared secret the design hands to every peer at spawn. If the
  project ever wants keypads to be a real barrier, that is a separate decision about who may hold the code, not a
  validation check on the open report.
### Where the weapon identity actually is (2026-10-18) — and why the wire field is not mechanical

`Body.Attack` computes building damage from an `AttackInfo` (`atk`), not from an item:

    buildingEntity.health -= (buildingEntity.animal ? atk.damage : atk.structuralDamage) * num2 * (...);

(`atk.unarmed`, `atk.structuralDamage`, `atk.distance`, `atk.swingSounds` are the same object.) The report site —
`BodyPatches`' postfix — holds the `Body` (`__instance`) and the damaged entities, so it CAN name the held item through
`Body.GetItem(slot)` / `Body.HoldingItem(slot)`, but the link between "the item held in this frame" and "the `atk` that
computed this damage" is one frame apart and not a direct reference. That makes "carry the weapon identity" a real
design step with a correctness question (what if the hand changes in that frame), not a mechanical field addition — and
it is the reason the damage row has stayed open rather than half-built.

Two candidate anchors, both DATA, to choose between when this row is built:

- **The item identity on the report** and the host comparing it with the item it holds for that player plus that item's
  own damage table — the shape this ticket has assumed. Needs the frame question above answered.
- **The attack's own numbers**: the report carries the structural damage the client computed, and the host compares it
  with the item's table itself. Same wire content, different comparison point; avoids the frame question but duplicates
  the multiplier (`num2` includes the claw curve when unarmed), which is game logic the host should own, not re-derive.

The open row for the OPEN report has the same shape one level simpler: the lockpick item is the gesture's tool and there
is no damage arithmetic, so it is the one to land first.
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
open patch names `Minigame.game.currentItem` — the lockpick, per
`reversing/.../LockpingMinigame.cs` (`NeedsItem() => this.pickLevel >= 0`, `Minigame.game.currentItem`) — and the host
check above becomes possible. Until then the open report has no data anchor and the row stays open, honestly.
**Prerequisite filed:** the client cannot name the tool it used — the game's `Item` has no instance id and CUO keeps no map — so both remaining rows wait on `docs/backlog/todo/item-identity-bridge.md`.

## A. A report that names another member's own body or hands — CLOSED 2026-10-17

LANDED: every surface below compares the identity it would write against the TRANSPORT SENDER. The
admission seam judges a pickup's destination and the enemy-combat victim BEFORE its item rules, because
neither needs a fact the host has to look up (`KernelCommandGateway.AdmitMemberSubmission`, pinned by
`KernelMemberIdentityReportTests`), and three handlers compare HOST-SIDE only, since the host's own relay
legitimately names a third member (`LimbStateEventHandler`, `SceneStateHandler`, `SpeechHandler`; pinned
by `SceneStateReportIdentityTests`, `LimbStateSyncTests`, `SpeechChannelTests`). The scene handler's
`msg.SteamId != 0 ? msg.SteamId : sender` fallback is gone: the payload no longer decides whose state it
is. Verdict shape, per the family: IGNORED with no answer in the seam and dropped at the handler —
nothing durable written, nothing relayed.

The machine reading is `docs/evidence/acceptance/member-report-bounds-a-20261017.md` (batch `20261017-a`, three clients): each of the seven wire members read a by-name refusal on the host, and an unmodified stretch of the same session read none.

What did NOT move: magnitudes, distances and rates — the affected member's own report still carries them,
which is the design intent quoted below. What the direction leaves open, named rather than implied: the
three handler shapes are `Bidirectional`, so a forged frame addressed straight at a GUEST (rather than at
the host) is still applied there — a limb latch into its clone fact table, a scene row into its presence
table, a bubble on the named clone. That is presentation-only on that client, overwritten by the owner's
own next report or keyframe, with no durable record and no host write; bounding it would need a
guest-side identity rule the star topology has no anchor for. The pickup verdict's cost is the
reconciliation budget rather than silence: a refused pickup kind is RE-REPORTED
(`GuestCommandReconciliation`, `IntervalMs = 5_000`, `MaxReports = 12`), so one forged report is up to
thirteen host audit lines over about a minute, while the three enemy kinds are single-shot.

The five surfaces the census recorded, with the account this section opened with — each now bound as
described above:

- **`WireCommandKind.ItemPickup`** — the domain commits `ItemLocation.Carried(command.NewOwner)` with no
  comparison against the sender (`ItemDomainModule.DecidePickup`), and the seam's eligibility family
  covered a destroy report, a state report and a container sync but not this one, so a report could put a
  world item into another member's hands. NOW: the destination must be the sender, in the same
  eligibility family the S1a stage built.
- **`NetMsg.LimbStateEvent`** — the stored write was keyed by the body the payload names
  (`CharacterDataStore.ApplyLimbStateEvent`), with no sender comparison in the handler, so a report could
  latch another member's limb. NOW: on the host an inbound frame must name the sender.
- **`WireCommandKind.RecordEnemyBite` / `RecordEnemyLunge` / `RecordEnemyEffect`** — the entity domain
  accepted all three outright (`EnemyDomainModule`), so the victim, the limb and the effect magnitudes
  were the reporter's own. NOW: the victim must be the sender, which is what the design intent requires —
  the AFFECTED side's client is the judge (`todo/health-simulation-and-host-authority.md`).
- **`NetMsg.SpeechMsg`** — the bubble was replayed under the speaker the payload named, with no sender
  comparison and no length cap. NOW: on the host an inbound frame must name the sender. The length cap is
  still open: it belongs with the abuse bound the chat handler's own rate cap is waiting for, and no
  reading of a native bubble's length exists to bound it with.
- **`NetMsg.SceneState`** — the reporter was `msg.SteamId != 0 ? msg.SteamId : sender` and the membership
  table was the only check, so a report could move ANOTHER member's scene state and have the host send
  that member the whole entry fanout. NOW: on the host the claim must name the sender, and the fallback is
  deleted rather than kept as a second path.
## B. A number the host could derive, taken from the report (one row closed, one re-judged — 2026-10-17)

- **`NetMsg.BuildingEntityDamaged` (51)** — the only check is that an entity stands at the position;
  `entity.health -= damage;` takes the magnitude as reported, so one report destroys any building. The
  anchor is the damaging item's own identity and its damage table (`Body.Attack`'s melee write is the
  native call site), which the message does not carry today.

  **The WHERE half landed and read (2026-10-18, `1a651ddc`, deployed `+1a651ddc`)**: the sender now travels the whole
  path — packet handler → world event → service passthrough → relay → `WorldBuildingEntitySync` — and a damage report
  further than the shared reach from the reporter's own body is refused before anything is applied or relayed.
  Driven from the member client: `/entitydamage 400 0 50` produced
  `[EntitySync] <member>'s damage report at (400,508) is further than 8 cells from its own body — refused: nothing
  applied, nothing relayed`, and `/entitydamage 2 0 5` added no refusal line.

  **The HOW MUCH half is still open, and deliberately not faked**: the magnitude's anchor is the weapon's own damage
  table, which the message does not carry. A host-side cap would be a second rule for a number the game already
  computes, so it waits for the wire to carry the damaging item — the same shape the trader-give row above settled.
- **`NetMsg.BuildingEntityOpened` (52)** — same shape: `entity.health = 0f;` after a presence test only. The
  lockpick and keypad minigames run on the opener's own client, so the open is its result. Half the anchor
  already exists host-side: the keypad code is the host's (`NetMsg.KeypadCode` = 57), and the entity's own
  type says which lock it is; what is missing is a host-side model of the progress a lock requires.

  **The reach bound is the half that needs no model, and the chain it must change is traced (2026-10-18)** —
  recorded because the bound costs one wire-shaped change, not a redesign: the report carries the position and
  NOT the reporter, so the host cannot compare the two. `BuildingEntityOpenedHandler` fires
  `ctx.World.FireBuildingEntityOpenedReceived(pos)`, `WorldStateMessageService.FireBuildingEntityOpenedReceived`
  invokes `BuildingEntityOpenedReceived?.Invoke(pos)` on a `Action<NetVector2>` that `IWorldControl` declares
  without a sender, and `WorldEventSync.OnRemoteBuildingEntityOpenedRelay` hands it straight to
  `WorldBuildingEntitySync.OnRemoteBuildingEntityOpened`, which writes the health. Note the send side already has
  the same shape for `BlockPlaced`, whose handler DOES thread the sender — that is the pattern to copy, not a new
  mechanism. So the cycle is: carry the sender through the handler, the event and the relay; in the sync, look the
  opener's body position up the way the character-data bound already does, refuse beyond the interaction reach
  with the name of the sender, the position and the distance, and keep applying an in-reach open. The minigame
  itself stays the opener's (a lock's progress is not reproducible host-side); this row bounds WHERE, not HOW.
- **`NetMsg.TraderAction` (73)** — the trader is re-located from the reported position and the stock side is
  re-derived (`TradeStockMachine.TryPurchase` / `.TryGiveItem`), but the item id, the price, the reputation
  offsets and the flags ride the payload. The host holds the trader's own state, so the action can be
  re-priced host-side instead of trusted. RE-JUDGED 2026-10-17 — this row belongs with section C, not
  here, and the reading is the decompiled tree: the PURCHASE half is bound already (the host prices its own
  stock entry, `TradeExecutor.ExecutePurchase`), while the GIVE half's number is
  `min(item.Stats.GetValue(item), MAX_VALUE_GIVEN - totalValueGiven)` (`TraderScript.cs`, `GiveItem`) and
  `ItemInfo.GetValue` reads the INSTANCE — `item.condition`, a magazine's `rounds/maxRounds`, a
  container's liquid `stack` — so a type id cannot reproduce it. The anchor this row needs is the item
  INSTANCE's identity (or its state) and the wire carries only the type id; the cap half is already
  aligned (`TradeStockMachine.TryGiveItem` clamps to the remaining headroom, as the game does). The
  reputation chain and the flags are the acting player's OWN body state, which the message documents as
  computed there, so they are not this row's business either.
- **`NetMsg.RecipeUnlock` (77)** — `RecipeUnlockReceived?.Invoke(recipeIndex);` applies and the handler then
  relays, with no range check against the host's own recipe table (the SNAPSHOT path's `known` set answers a
  different question — whether the host had already LEARNED the index, never whether its table HAS one).
  index must be one entry of THIS host's table (`CraftSyncService.MayApplyReportedUnlock`, pinned by
  `RecipeUnlockBackfillTests`); an index it has no recipe for is refused by name — nothing applied here,
  nothing relayed — while the host's own unlock, every guest-side apply and a read that FAILED keep the
  behaviour they had. The machine reading is `docs/evidence/acceptance/recipe-unlock-bound-20261017-b.md` (batch `20261017-b`, three clients): the refusal names the sender, the index and the table's own 132 entries, and a real index still unlocks in the same session.
- **`NetMsg.BlockPlaced` — an air write, the break carrier's cell (found by batch `20261016-c`)** — the census
  row's own bound (`the generation stamp + the first-writer cell rule`) covers the layer and the cell's state,
  never the REPORTER: `WorldEventSync.OnRemoteBlockPlaced` reads the host's own cell
  (`standingBlock = WorldGeneration.world.GetBlock(pos)`), compares it with the claimed write and applies, with
  no comparison against the sender's own body anywhere on the path — so a member naming any cell of the current
  layer has that block broken there. The anchor the host already holds is the member's own body position; what
  has to be settled BEFORE a check is whether every legitimate writer stays inside a reach (`Body.Attack`'s
  melee write is the native call site, but a quake breaks a whole region and an environment write is nobody's
  gesture) or whether the anchor has to be the cell's own cause instead. Live reading: `20261016-c` had the
  member break a cell 122 blocks from its own cell as that run read the body, and the host applied the break and
  judged its drops (`docs/evidence/acceptance/validation-and-source-checks-s2b-20261016.md`).

  **DECIDED by the user 2026-10-18 — bound the GESTURE only.** The question was what an out-of-reach write
  should do, and the user asked first whether moving the quake to host-side authority would change play. It would
  not, because the quake is ALREADY host-authoritative: `WorldGenerationUpdatePatch` records that the quake timer
  runs on every side while the host broadcasts the start and **the guest's own timer is frozen**, and the patch
  bridge's own gate says only quake breaks are gated while environment breaks pass. So the user's answer is to
  bound the gesture and leave the quake and environment writes exactly as they are — with the confirmation that
  the gesture bound changes no legitimate play.

  **The split is already on the wire**: `OnRemoteBlockPlaced(ulong sender, int x, int y, ushort block, bool
  playerBreak, WorldGenerationRelation generation)` carries `playerBreak`, so the check belongs beside the
  generation refusal, inside the host branch, on `playerBreak` writes only. The anchor is the one the visibility
  bound already uses — `CharacterDataMsg.Position` as `PlayerInteractionVisibility.TryGetPosition` reads it — and
  the reach constant is chosen once and shared with the `BuildingEntityOpened` row below, so a hand that can pick a
  lock is also a hand that can break the wall beside it.

  **LANDED 2026-10-18** (`6bdf1e36`, deployed and verified `+6bdf1e36`): `InteractionReach.Cells` holds the one
  number, `WorldEventSync` takes `IEntitySyncControl` for the reporter's position, and a `playerBreak` air write
  further than that reach is refused before anything is applied, recorded or relayed. An absent position is not a
  refusal. Gates 668 and the behaviour suite are green; quake and environment writes carry `playerBreak: false` and
  never enter the branch.

  **The live row is the only thing left, and it is one lever short**: driving it needs a client to send
  `NetMsg.BlockPlaced` for a far cell, and the acceptance vocabulary has no way to reach it — `PatchBridge` exposes
  no world control (`Impl`, `ModContent`, `SessionSurface`, the patch ports only), and the send lives on
  `IWorldControl.SendBlockPlacedReport(x, y, block, playerBreak)`. So the next run needs ONE of: a world-control
  accessor on the bridge for a recipe, or a console verb that reports a break. Until then the row is
  implemented-but-unverified and the ticket stays in `todo/`.

  **READ ON THE MACHINE 2026-10-18 (`4b852aa3`, one live session) — the row PASSES.** The lever is a console verb,
  `/blockreport <dx> <dy> <block> <playerBreak>`, which registers itself through the same attribute scan the other
  command owners use (an unrequested singleton is never constructed, so the registry builds it explicitly). Driven
  from the MEMBER client, two reports one after the other:

  - `200 0 0 true` → the host answered with the new refusal:
    `[BlockSync] <member>'s player break at (200,486) is further than 8 cells from its own body — refused: nothing
    applied, nothing recorded, nothing relayed.`
  - `1 0 0 true` → no refusal, and the write went through the ordinary path (the host reads its own cell, applies,
    and relays).

  So the bound does what the user decided: a hand that cannot reach the cell is refused with a line, a hand that can
  keeps working, and quake/environment writes (`playerBreak=false`) never enter the branch at all.

  **The `BuildingEntityOpened` half landed on the same number and was read the same way (2026-10-18, `5168fe78`,
  deployed `+5168fe78`).** An OPEN is a lock being worked, so it is a gesture: the sender now travels the whole path
  (packet handler → world event → service passthrough → relay) and the host refuses an open further than the shared
  reach, judged on the opener's own body. Driven from the member client: `/entityopen 300 0` produced
  `[EntitySync] <member>'s open at (300,504) is further than 8 cells from its own body — refused: nothing applied,
  nothing relayed`, while `/entityopen 1 0` met no refusal and took the ordinary path. A guest applying the host's
  broadcast does not judge — it is applying a fact already judged.

  **Refused-by-gate, recorded rather than smuggled:** `WorldEventSync` had been sitting at exactly the 600-line cap,
  so this bound pushed it over. Moving the helper into its own part did NOT help — the gate counts a type, not a
  file — so the class is now recorded in `docs/architecture-debt.json` with its real size. The split it deserves is
  its own task: the file keeps accreting bounds and one more will make this a real architectural problem.

  **The accessor cannot be a plain bridge member** — found by reading the interface's own contract before editing
  it: `IPatchBridge`'s doc states that a domain-specific member belongs on its OWN port ("declares a per-domain
  port instead — `IFluidPatchPort` is the first …") because the shape gate *"fails on a member added here or on
  the implementation"* (`PatchBridgePortShapeGateTests` plus the reflective `PatchBridgePortContractTests`). So the
  lever is a new narrow port — one member, `SendBlockPlacedReport`, implemented by
  `GameAdapterBridge` and surfaced as `PatchBridge.WorldReports => _bound as IWorldReportPatchPort` — exactly the
  shape `Fluid`/`LayerAdvance`/`Wearable` already use. Adding it will also ask those two gates to be told about the
  new port, which is the first thing to check when that cycle starts.

  **Attempted and REVERTED 2026-10-18 — the port route costs more than it looks.** The port itself is four small
  edits and the bridge compiles, but the shape gate's port table ALSO feeds the aggregate census: registering the
  new port there makes `Aggregate_DeclaresExactlyTheFrozenCensus` expect the port's member among the AGGREGATE's
  members, which the aggregate deliberately does not have. That is a gate-shape question (a port's member has to
  stay out of the aggregate's pin), not a product question, and answering it must not be smuggled into an
  acceptance-lever cycle. Reverted to `f521e633` with the tree green; the work is parked here. **The cheap second
  road is still open and is what the next attempt should take first**: an acceptance-only console verb that reports
  one block write, which touches no interop seam at all.

  **The console road, surveyed (2026-10-18)** so the next attempt starts on it: registration is attribute-based —
  `ConsoleCommandRegistry.AddBuiltIns(owner)` scans the owner's methods for `ConsoleCommandAttribute` and registers
  them — so the verb is ONE method on whichever class already owns the built-ins, calling
  `IWorldControl.SendBlockPlacedReport(x, y, block, playerBreak)`. The one question to answer before writing it is
  the PERMISSION: the verb has to run on a MEMBER client (that is the reporter side under test), and a member-side
  verb that writes world state is exactly what the member-write policy (`AllowMemberItemDataWrite`, `Forbids`) has
  an opinion about. Decide that first — either the verb is admitted for members because it only publishes a report
  that the host still bounds, or it is host-only and the reading has to be taken with the HOST as the reporter,
  which changes what the row proves.

  **One correction to the reasoning above, and one dependency the check needs (2026-10-18).** The quake's breaks
  are NOT host-only: `WorldEventSync`'s own quake comment records that "every side shakes together and breaks its
  own nearby region; the regions union via the air-write relay". So a quake break IS a member report — and it is
  exactly why the `playerBreak` flag is the right split rather than a special case: a player break is reported with
  `playerBreak: true` (`BlockBreakSync` applies and broadcasts it that way), while a recovery replay states
  `PlayerBreak = false` and the pending-report record defaults to false. Quake and environment writes therefore
  never enter the branch this bound adds, which is what the user's decision rests on.

  **What blocks the implementation is one dependency, not the design**: `WorldEventSync` holds only
  `ISessionControl`, `IWorldControl`, `BlockBreakSync`, `RestoredWorldFactReplay`, `OperationTrace`,
  `WorldEntityKernelProjection`, `WorldEntryFanout` and the logger — no position source. The reach check needs the
  sender's body position (`PlayerInteractionVisibility` gets it from the entity stream), so this cycle is: add that
  narrow port to `WorldEventSync`'s constructor and its DI registration, then the check itself, then the
  `BuildingEntityOpened` row on the same constant.

## C. A claim with no anchor (a design decision, not a check)

- **`WireCommandKind.ItemSpawn`** — the item-creation half named in S2a's own limits: the report names an
  item nobody has seen and carries no creating cause. A bound needs an anchor the wire does not have (the
  consuming use, the container it came out of, the craft that produced it) — the same decision the spawn
  family already owes.
- **`NetMsg.EntityEvent` (66)** — the host applies the trap report against its own world but takes the
  trigger as a fact, and the building-death drops riding a DESTRUCTIVE trap event are unbounded (S2b closed
  the block-break carrier; this is its sibling). The anchor question is the same one S2b answered for the
  break: what identifies the entities that died in the blast, when the association on the reporter's side is
  a radius and not an identity.

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

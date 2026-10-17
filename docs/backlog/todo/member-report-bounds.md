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

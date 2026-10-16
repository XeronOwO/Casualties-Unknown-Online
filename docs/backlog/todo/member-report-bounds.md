# The member reports that still have no bound

- Status: Todo — opened 2026-10-16 by the survey behind `docs/architecture/member-report-validation.md`
  (every row of its census marked `unbounded:` points here). S2b landed the block-break carrier's building
  drops; this ticket is the rest of the same question, one surface per row, each with its own evidence.
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

## A. A report that names another member's own body or hands

The transport sender is the trusted fact, and this tree already knows the pattern
(`CraftSyncService`: `msg.OwnerSteamId = sender; // the transport sender is the trusted fact`). Four
surfaces still key a write on the payload's own identity:

- **`WireCommandKind.ItemPickup`** — the domain commits `ItemLocation.Carried(command.NewOwner)` with no
  comparison against the sender (`ItemDomainModule.DecidePickup`), and the seam's eligibility family covers
  a destroy report, a state report and a container sync but not this one
  (`KernelCommandGateway`: `command is DestroyItemCommand destroy && !MayReportOn(...)` /
  `command is UpdateItemStateCommand state && !MayReportOn(...)`). A report can therefore put a world item
  into another member's hands. The fix belongs in the same eligibility family the S1a stage built, so the
  verdict shape (ignore, answer nothing, let the item snapshot converge the reporter) is already decided.
- **`NetMsg.LimbStateEvent`** — the write is keyed by the body the payload names
  (`CharacterDataStore.ApplyLimbStateEvent`: `if (_savedCharacters.TryGetValue(msg.OwnerSteamId, out var data))`)
  with no sender comparison in the handler, so a report can latch another member's limb.
- **`WireCommandKind.RecordEnemyBite` / `RecordEnemyLunge` / `RecordEnemyEffect`** — the entity domain
  accepts all three outright (`EnemyDomainModule`: `RecordEnemyBiteCommand c => DomainDecision.Accept(new
  EnemyBiteResultEvent(c.VictimSteamId, c.Limb, c.VenomTotal, c.Adrenaline, c.Happiness))`), so the victim,
  the limb and the effect magnitudes are the reporter's own. The design intent is that the AFFECTED side's
  client is the judge (`todo/health-simulation-and-host-authority.md`), which is exactly why the victim
  identity has to be the sender rather than a field.
- **`NetMsg.SpeechMsg`** — the bubble is replayed under the speaker the payload names
  (`if (msg.SpeakerSteamId == 0) { ReplayOnTrader(msg); return; } ReplayOnClone(msg.SpeakerSteamId, msg.Text);`)
  with no sender comparison and no length cap.
- **`NetMsg.SceneState`** — the reporter is the body the payload names
  (`var reporter = msg.SteamId != 0 ? msg.SteamId : sender;` in `SceneStateHandler.Handle`), and the
  membership table is the only check: a report can move ANOTHER member's scene state and have the host send
  that member the whole entry fanout. The relay case the comment describes (the host re-sending another
  member's change) is the reason the field exists, so the fix is a direction-scoped comparison — on the host,
  an inbound frame the local member did not send is the member's own report and must name itself.

## B. A number the host could derive, taken from the report

- **`NetMsg.BuildingEntityDamaged` (51)** — the only check is that an entity stands at the position;
  `entity.health -= damage;` takes the magnitude as reported, so one report destroys any building. The
  anchor is the damaging item's own identity and its damage table (`Body.Attack`'s melee write is the
  native call site), which the message does not carry today.
- **`NetMsg.BuildingEntityOpened` (52)** — same shape: `entity.health = 0f;` after a presence test only. The
  lockpick and keypad minigames run on the opener's own client, so the open is its result. Half the anchor
  already exists host-side: the keypad code is the host's (`NetMsg.KeypadCode` = 57), and the entity's own
  type says which lock it is; what is missing is a host-side model of the progress a lock requires.
- **`NetMsg.TraderAction` (73)** — the trader is re-located from the reported position and the stock side is
  re-derived (`TradeStockMachine.TryPurchase` / `.TryGiveItem`), but the item id, the price, the reputation
  offsets and the flags ride the payload. The host holds the trader's own state, so the action can be
  re-priced host-side instead of trusted.
- **`NetMsg.RecipeUnlock` (77)** — `RecipeUnlockReceived?.Invoke(recipeIndex);` applies and the handler then
  relays, with no range check against the host's own recipe table (the SNAPSHOT path does check it:
  `if (!known.Add(index)) { continue; }` behind `CaptureUnlockedRecipes()`).

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

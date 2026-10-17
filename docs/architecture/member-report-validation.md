# Member-report validation

- Status: Spec — one rule for every state a member REPORTS, and the census that makes the rule answerable
  per wire member
- Scope: every wire member a member's own client can author (`NetMsg` members a `GuestToHost` /
  `Bidirectional` handler serves, and every `WireCommandKind` the authority policy does not declare
  `HostOnly`); enforced by `ReportValidationCensusGateTests`

## The rule

A host is authoritative over the world, but a member's own client runs the game's native action first and
reports the result — a block break and its drops, an item move, a limb latch, an open, a minigame's
progress. The report is a CLAIM. The rule is:

1. **Every claimed fact is bounded by what the host can establish itself** — the world it simulates, the
   record it holds, the table it can read. Never by the reporter's own word, and never by a re-derivation
   the host cannot reproduce (an unseeded roll, another client's frame).
2. **A surface with no such anchor says so**: the row names the anchor it would need, and the gap carries a
   ticket. "Nothing checks this" is not a state a surface is allowed to be in quietly.
3. **A bound refuses BY NAME and the refusal is ANSWERED**, so the refusing side and the refused side end up
   agreeing. A refusal with no way back to agreement is worse than no check.
4. **Never invent the identity a message cannot carry.** If an action needs a limb, a target or an owner the
   report does not name, the right answer is to refuse the request or ask the user — not to substitute "the
   most injured limb", "the first match" or "the nearest entity", and not to ship the fallback as a
   documented limit.
5. **The affected client judges its own experience**; the host arbitrates and owns the world; latency is
   never a parameter of a judgement. A bound is a check on a CLAIM, not a second simulation of the
   reporter's screen.

The point is the game's own baseline, not an anti-cheat: ordinary play must never meet a refusal. Where an
honest bound would refuse a legitimate roll, the bound is the wrong tool — see `DropBoundPolicy` for the
worked case (the host bounds an outcome instead of re-rolling it, because the roll is unreproducible).

## The three kinds of member-authored surface

| Class | What it is | What it owes |
|---|---|---|
| **Request** | The member asks the host to do something (`PlayerCarryStartRequest`, `LayerAdvanceRequest`, `ModCommandRequest`). The host executes it against its own simulation, so its own preconditions ARE the decision. | The preconditions it checks, listed per row. Nothing is taken from the reporter except the intent. |
| **Fact** | The member's client already ran the native action and reports the outcome or the resulting state (`BlockDamaged`, `ItemUpdateState`, `MedicalOperationEndRequest`). The host cannot un-run it. | A bound: the record, table or announcement the claim is checked against — or the missing anchor plus a ticket. |
| **Presentation** | Only a transient presentation is written (a sound, a decal, a pose, a ping). No durable state. | Nothing to bound by design; the row states that, so the absence is a decision rather than an oversight. |

A **request** can still leak a claim: `WorldTimeRequest` is named a request but the member's clock already
moved locally, so the host bounds the speed against its own whitelist. Rows are classified by what actually
crosses the wire, not by the member's name.

## The census

One row per member-authored wire member. A row that starts with `unbounded:` names an anchor the surface
would need and MUST point at an existing ticket under `docs/backlog/`, so a gap cannot be parked silently.

| Wire member | Class | Bound, or the anchor it lacks | Detail |
|---|---|---|---|
| `WireCommandKind.ItemPickup` | Fact | `bound: the seam's member-identity rule` | The seam binds the actor to the sender and the domain checks existence, terminal state, an installed item and the revision (which the handler re-stamps from the host's record), and the destination is now tied to the sender as well: the admission seam compares `NewOwner` with the transport sender before the item rules, because that comparison needs no fact the host has to look up. A report naming another member is IGNORED, unanswered — the reporter's own copy is not the fact a refusal would settle. The cost is the reconciliation budget rather than silence: the pickup kind is re-reported (`GuestCommandReconciliation`, `IntervalMs = 5_000`, `MaxReports = 12`), so one forged report reads as up to thirteen host audit lines over about a minute, while the three enemy kinds are single-shot. |
| `WireCommandKind.ItemDrop` | Fact | `bound: the host's own record of the item's location` | The domain refuses a drop of an item the record does not put in the dropping actor's hands, plus the target-location and container rules. |
| `WireCommandKind.ItemDestroy` | Fact | `bound: the seam's eligibility rule` | A destroy report may name world-rooted content or the sender's own carried, installed or contained item, never another member's. |
| `WireCommandKind.ItemUpdateState` | Fact | `bound: the seam's eligibility rule` | Same rule for a state report; the domain then checks existence, terminal state and the revision, which the handler re-stamps from the host's record. |
| `WireCommandKind.ItemContainerSync` | Fact | `bound: the seam's eligibility rule, parent and children` | The parent must be reportable by the sender and every listed child must be allowed to enter the sender's own container. |
| `WireCommandKind.ItemApplyLimb` | Fact | `bound: MayInstallOnOwnLimb` | The item must be the sender's own, and the limb the report names must be the one the record already holds. |
| `WireCommandKind.ItemRemoveLimb` | Fact | `bound: MayTakeOffOwnLimb` | Both the applied owner and the destination owner must be the sender, and the limb must match the record. |
| `WireCommandKind.ItemDataWrite` | Fact | `bound: the host's own rule for the verb + the eligibility rule` | `IMemberCommandRules.AllowsItemDataWrite` (default off) is read per submission and the refusal is answered; an id the host never judged is refused rather than healed. |
| `WireCommandKind.ItemSpawn` | Fact | `unbounded: a creation cause` | Nothing anchors a spawn report: it names an item this host has never seen, and the report carries no creating cause to bound it against (the item-creation half of the same conversation). Ticket: `docs/backlog/todo/member-report-bounds.md`. |
| `WireCommandKind.ItemTransfer` | Fact | `n/a: host-only` | The authority policy declares it `HostOnly`, so a member's submission is refused by name before any domain sees it; the host's own commit is the fact. |
| `WireCommandKind.RecordEnemyBite` | Fact | `bound: the seam's member-identity rule` | The victim must be the sender: the affected side's client is the judge of its own body (`todo/health-simulation-and-host-authority.md`), and every production sender stamps its own id (`EnemyCombatReplay`, `EnemyProximitySync`). The limb and the venom/adrenaline/happiness are that body's own reading and are never re-derived — they are admissible because the body is the reporter's. A report naming another member is IGNORED, unanswered. |
| `WireCommandKind.RecordEnemyLunge` | Fact | `bound: the seam's member-identity rule` | Same rule as the bite and the same verdict: the victim must be the sender. |
| `WireCommandKind.RecordEnemyEffect` | Fact | `bound: the seam's member-identity rule` | Same rule: the effect, its level and its magnitudes are the affected body's own reading, so they are admissible only while the body is the reporter's. |
| `NetMsg.BlockPlaced` | Fact | `bound: the generation stamp + the first-writer cell rule` | A reach bound was attempted and WITHDRAWN (reverted in d2d2b3f): it compared block indices with world positions (WorldToBlockPos offsets by at least 128 cells), sat on the air write while the break lands through BlockDamaged, and selected itself on a flag the reporter writes. Do not read this row as closed. A report for another layer is refused before it can claim a regenerated cell, and a cell whose own state matches the claimed write is answered with the host's cell instead of applied. The REPORTER is not part of that bound — the cell is applied as named, with no comparison against the sender's own body — which is filed with its live reading in `docs/backlog/todo/member-report-bounds.md`. |
| `NetMsg.BlockDamaged` | Fact | `bound: the generation stamp + the per-sender damage ledger + the break arbitration + the destroyed block's own drop table + the dying buildings' own tables` | The cell's break must be the sender's first-writer claim; the drops are bounded against the block the host itself recorded destroying and against the tables of the buildings that lost support there. |
| `NetMsg.BlockDamageReport` | Fact | `bound: the same per-sender ledger` | The absolute re-report is applied only for the difference this host has not accounted for from that sender. |
| `NetMsg.BuildingEntityDamaged` | Fact | `unbounded: the damage magnitude` | A reach bound was attempted and WITHDRAWN (reverted in d2d2b3f): this channel also carries explosion and trap damage, which a reach bound would refuse, and it could not suppress the relay either. The only check is that an entity stands at the reported position; the damage itself is taken as reported, so one report can destroy any building. The anchor would be the damaging item's own identity and damage table. Ticket: `docs/backlog/todo/member-report-bounds.md`. |
| `NetMsg.BuildingEntityOpened` | Fact | `unbounded: the opener's own minigame result` | A reach bound was attempted and WITHDRAWN (reverted in d2d2b3f): it could not suppress the handler's relay, so a refused open was still broadcast to every other guest. The only check is that an entity stands there; no lock, keypad or progress state is consulted, so a lockpick or keypad can be opened by report. The code the host already holds (its own keypad authority) is the anchor half of this. Ticket: `docs/backlog/todo/member-report-bounds.md`. |
| `NetMsg.EntityEvent` | Fact | `unbounded: the trigger's own cause, and the drops that ride it` | The host applies the report against its own world but takes the trigger as a fact, and the building-death drops riding a destructive trap event are not bounded (the block-break carrier's sibling). Ticket: `docs/backlog/todo/member-report-bounds.md`. |
| `NetMsg.EntitySpawned` | Fact | `bound: the generation stamp + the creation key + the host's own materializability` | A duplicate creation key binds instead of creating, a stale generation is refused, and a creation this host cannot materialize is rejected back to its reporter. |
| `NetMsg.DynamiteExplosion` | Fact | `bound: the one-shot item id` | A repeated detonation of the same item is dropped as a duplicate. |
| `NetMsg.FluidInteraction` | Fact | `bound: the kind whitelist + the grid clamp + the empty-cell guard` | Only the drink kind is served, the cell is clamped into the grid, and an already-empty cell is a duplicate. |
| `NetMsg.WorldTimeRequest` | Request | `bound: membership + the start gate + the speed whitelist` | The member's own clock already moved, so the host answers with its own authoritative speed and refuses a value outside the policy. |
| `NetMsg.LayerAdvanceRequest` | Request | `bound: handshaken member + the generation identity + the advance policy` | A report from another generation is refused, so one layer cannot advance twice. |
| `NetMsg.LocationPing` | Presentation | `n/a: presentation only` | A marker is dropped when it claims another sender, echoes this side's own, or names an unknown kind; nothing durable is written. |
| `NetMsg.WorldBloodSpawn` | Presentation | `n/a: presentation only` | A transient decal with no snapshot and no durable state. |
| `NetMsg.CharacterData` | Fact | `bound: the transport sender is the save key` | The report body is not validated (it is the member's own body by design), but it is stored under the transport sender, and at restore the host's own transfer table wins over the guest's report. |
| `NetMsg.LimbStateEvent` | Fact | `bound: the transport sender names the owner (host-side)` | The stored write is keyed by the body the payload names, and on the HOST an inbound frame must name the member that sent it — a report naming another member's body is dropped before anything durable is written or relayed. Host-side only: the host's relay carries the owner's id while its transport sender is the host. Everything the named owner's own report carries stays accept-first. NAMED as left open: the message is `Bidirectional`, so a forged frame addressed straight at a GUEST is still applied to that guest's clone fact table — presentation on that client, overwritten by the owner's own next report, with no durable record and no host write. |
| `NetMsg.CharacterSound` | Presentation | `n/a: presentation only` | A clip is replayed on the owner's clone; nothing durable. |
| `NetMsg.CharacterAttackAnim` | Presentation | `n/a: presentation only` | A prefab is instantiated from the report; only a zero direction and a missing prefab are dropped. |
| `NetMsg.CharacterLandingVisual` | Presentation | `n/a: presentation only` | A ground animation and dust; nothing durable. |
| `NetMsg.CharacterRagdoll` | Presentation | `n/a: presentation only` | The clone's pose is forced; the authoritative body state rides the character data. |
| `NetMsg.PlayerColorUpdate` | Presentation | `bound: the sender's own member id` | A report naming another member is ignored; the colour itself is presentation. |
| `NetMsg.PlayerPushRequest` | Request | `bound: the pair, both in-world, the carry and consciousness gates, distance, and a per-pusher cooldown` | The force is computed from the host's own entity positions; only the reach is judged on the pusher's client. |
| `NetMsg.PlayerCarryStartRequest` | Request | `bound: the pair, both in-world, the mode gate on both bodies, and the duplicate-carrier guard` | The host commits the carry state; line of sight is the requester's own view. |
| `NetMsg.PlayerCarryStopRequest` | Request | `bound: the sender must be the carrier or the carried` | The relation is resolved from the host's own record, not from the message. |
| `NetMsg.PlayerInventoryTakeRequest` | Request | `bound: the host rule flag + both in-world + the game's own take rule + the ownership fact + the worn-item exclusion` | The item must be found in the host's own snapshot of the source; no distance or rate check exists. |
| `NetMsg.PlayerHealRequest` | Request | `bound: the healer and target gates + the item's presence in the healer's own snapshot + the profile table` | The effect and the condition cost are host-side tables, never the report's. |
| `NetMsg.PlayerItemUseRequest` | Request | `bound: the user and target gates + ownership + the family admission from the item's own data + the limb requirement + the host-side liquid cap` | The item's own data decides the family; a limb tool without a limb is refused, and a drink is capped at what the item really carries. |
| `NetMsg.PlayerItemActionOutcome` | Fact | `bound: the host's own admission record for that target, item, user and limb` | The outcome is matched against an admission the host recorded when it executed the use, not against the report's own claim. |
| `NetMsg.TraderAction` | Fact | `unbounded: the credit a member's giveaway reports, which the wire cannot anchor` | The PURCHASE half is the host's own already: it validates the id against its own stock and prices that entry itself (`trader.ItemPrice`), so no number of the report's is trusted there. The GIVE half cannot be derived: the credit is `min(item.Stats.GetValue(item), MAX_VALUE_GIVEN - totalValueGiven)` (`TraderScript.GiveItem`), and `ItemInfo.GetValue` reads the item INSTANCE — its `condition`, a magazine's remaining `rounds/maxRounds`, a container's liquid `stack` — none of which the type id in `ItemId` carries, so the anchor this row needs is the item instance's identity (or its state), and the wire does not have it. The cap half IS aligned on both sides: `TradeStockMachine.TryGiveItem` clamps to the remaining headroom exactly as the game does. Ticket: `docs/backlog/todo/member-report-bounds.md`. |
| `NetMsg.TraderRecruitRequest` | Request | `bound: the pair + the host rule + both in-world + the trader re-located + the recruit policy + one recruit per trader` | Range and sight are judged on the requester's client only. |
| `NetMsg.Chat` | Fact | `bound: the shape cap and the sender identity` | A line longer than the policy, or one claiming another sender, is dropped; this handler has no rate cap (the abuse bound lives outside it). |
| `NetMsg.SpeechMsg` | Presentation | `bound: the transport sender names the speaker (host-side)` | The bubble is replayed on the clone of the speaker the payload names, so on the HOST an inbound frame must name the member that sent it — the identity rule a chat line claiming another author already meets, dropped unanswered. Host-side only (the relay's transport sender is the host), and the trader shape (`SpeakerSteamId == 0`) is host-only by construction, so a member claiming it is refused too. NAMED as left open: a forged frame addressed straight at a GUEST is still replayed there as a bubble. The missing length cap is NOT bounded here: it belongs with the abuse bound the chat handler's rate cap is also waiting for, and no reading of a native bubble's length exists to bound it with. |
| `NetMsg.CraftReport` | Fact | `bound: the host's own id tables decide what the report's ids mean` | The policy is accept-with-adopt and never rejects, because the sender's consumption is irreversible: the report's ids are classified against the host's world and transfer records, and its products are adopted to the transport sender. |
| `NetMsg.RecipeUnlock` | Fact | `bound: the host's own recipe table` | On the host a member's reported index must be one entry of the table THIS host's world built (`INativeWorldFacts.CaptureRunFields` carries it entry by entry), because every side writes `Recipes.recipes[index].INT = 0` onto its OWN table — an index this host has no recipe for is refused by name and neither applied nor relayed. The host's own unlock and every guest-side apply are not this rule's business, and a read that FAILED (no live world, no registered reader) applies the report as before and says so rather than refusing on a table it could not read. NAMED as left open: the index space is per-process, so in a session whose two tables differ in LENGTH (admitted today, with a warning, by the content check) an honest unlock by the shorter-table side is refused and that member's set never converges — the pre-change outcome for such an index was already a per-side refusal by `RecipeUnlockApply`, so the bound removes the relay rather than the unlock; a host whose table exists but whose mod recipes are appended later refuses inside that window; the refusal is unanswered and the reporter converges on its own bounded budget (`MaxRecipeSetReports` = 3 windows), the family's normal shape; and on a GUEST the report is applied as before, the gap every `Bidirectional` row of this census carries. |
| `NetMsg.RecipeUnlockSnapshot` | Fact | `bound: the host's live recipe table + a monotone union` | The set is merged only while the host can read its own table, an index is never re-locked, and the reporter's own side caps its re-reports. |
| `NetMsg.ModCommandRequest` | Request | `bound: the shape caps + a token bucket + handshaken membership + mod and command resolution + the permission check` | The rate cap is the abuse half of the same request. |
| `NetMsg.MedicalOperationStartRequest` | Request | `bound: the pair + in-world + the operator's consciousness + the claim guards + the start validators + the liquid available + the kind whitelist + THE TARGET'S OWN VERDICT` | The operation opens only after the target's own client answers, and the host re-checks its own facts after that answer. |
| `NetMsg.MedicalOperationUpdate` | Fact | `bound: the operation and operator guard + the value sanity + a clamp against the host's own remaining liquid` | The operator's progress can never commit more than the item this host recorded really carries; the shrapnel half adds piece ownership, position clamps and a grip-slip comparison. |
| `NetMsg.MedicalOperationEndRequest` | Fact | `bound: the terminal total is clamped to the session's own available liquid` | The "exact total" is never taken on trust: it is clamped, the operator must own the session, and only the shortfall is applied. |
| `NetMsg.MedicalOperationCancel` | Request | `bound: the operation and operator guard` | Committed progress stays; no reason or permission is needed to stop one's own operation. |
| `NetMsg.MedicalOperationTargetCheckAnswer` | Fact | `bound: a live parked request, the asked target, and a three-second liveness bound` | The host does not re-derive the target's body verdict, and it re-checks its own facts after the answer arrives. |
| `NetMsg.RemoteInventoryIntentRequest` | Request | `bound: the host rule flag + both in-world + the ownership fact + the per-kind operand validation + a first-writer arbiter` | Line of sight is judged on the requester's client. |
| `NetMsg.ItemIdWatermark` | Request | `bound: the host's own id table` | The member reports the counter it allocated up to; the host answers with the grant its own table allows, so a restarted counter cannot collide with ids the host still holds. |
| `NetMsg.CarriedInventory` | Fact | `bound: the transport sender keys the registration` | The entries are the member's own inventory by design; the host registers them in that sender's transfer table. |
| `NetMsg.HandshakeAckAck` | Request | `bound: the host's own handshake state` | The host marks a member handshaken on this alone, and nothing in the payload can change that. |
| `NetMsg.KernelEnvelope` | Fact | `bound: per kind — see the WireCommandKind rows` | The envelope is only a carrier: every command kind it can hold is judged by its own row above (and refused by name when its authority is host-only). |
| `NetMsg.SceneState` | Fact | `bound: the transport sender names the reporter (host-side)` | On the HOST an inbound frame is the member's own report and must name itself; the old fallback (`msg.SteamId != 0 ? msg.SteamId : sender`) took the payload's word for whose state this is, so one report could enter another member and earn it the whole entry fanout. Direction-scoped: on a guest the host's relay legitimately names the third member. NAMED as left open: a forged frame addressed straight at a GUEST still moves that guest's own presence row and fires its remote-scene edge — local presentation, no durable record and no host write. |
| `NetMsg.Handshake` | Request | `bound: the host's own handshake and protocol bookkeeping` | The protocol number is the compatibility boundary, and nothing in the payload can grant a member more than the host's own session state allows. |
| `NetMsg.Ping` | Request | `n/a: a diagnostics echo` | The host echoes the ticks back; nothing durable is written. |
| `NetMsg.Pong` | Request | `n/a: a diagnostics echo` | The answer to a ping; nothing durable is written. |
| `NetMsg.ModMessage` | Fact | `n/a: the mod's own payload contract` | The framework routes the frame to the local mod with the carried id and drops an unknown one; the payload's meaning is a third-party contract the host cannot bound. |
| `NetMsg.TraderSwing` | Presentation | `n/a: presentation only` | A one-shot animation replayed on the trader at the reported position; nothing durable. |

## Four worked examples

- **S1a — a report may not name another member's own item.** One rule (`MayReportOn`) covers a destroy
  report, a state report and a container sync's parent and children, instead of one check per kind
  (`KernelCommandGateway`, the authority table in `WireCommandAuthorityPolicy`).
- **S2a — a block break's drops are bounded by the block that break destroyed.** The destroyed block is the
  HOST's own read, taken before the air write was applied; the bound is per drop and the report is matched
  as a set (`DropBoundPolicy`), and the table is the game's own loot switch or a CUO tile's authored drops
  (`GameBlockDropTable`).
- **S2b — a dying building's drops are bounded by the building's own prefab table.** The capture point is
  the support-loss MARK, the only moment the entities still exist, and the key is the CELL the report
  already carries — no new wire field, no invented identity (`GameBuildingDropTable`,
  `WorldBuildingEntitySync.MarkSupportLossRemote`).
- **S3 — a report may not name another member's own body or hands.** The transport sender is the trusted
  fact and the identity the payload carries is a claim: one rule in the admission seam (a pickup's
  destination and the enemy-combat victim must be the sender) and a direction-scoped comparison in three
  handlers (a limb latch, a scene report and a speech bubble must name the member that sent them — on the
  host only, since the host's relay legitimately names a third member). Every census row that said
  unbounded: about an identity now names the sender instead; no magnitude, distance or rate moved with
  them.

## How the rule reaches a new surface

`ReportValidationCensusGateTests` derives the census's reach from the code, not from this page: every
`NetMsg` member a `GuestToHost` or `Bidirectional` packet handler serves, and every `WireCommandKind` the
authority policy does not declare `HostOnly`, must have a row here, and every `unbounded:` row must name a
ticket that exists. A new member-authored wire member therefore cannot ship without answering the question.
The review asks it too (`docs/development/review-prompt.md`), and the guard list carries it as
`Report validation completeness` (`docs/architecture/guards.md`).

## Limits

- **The census answers "what does the host check", not "is that enough".** A `bound:` cell names the checks
  that exist; whether they are the right ones is a per-surface judgement, and the `unbounded:` cells are the
  ones where the answer is already known to be "no".
- **A `bound:` cell is asserted by the review, not by the gate.** The gate can see that a row carries a
  verdict, a live member name and (for `unbounded:`) an existing ticket; it cannot see whether the checks a
  `bound:` row names are really there. That half is the adversarial review's, and the twelve rows it
  spot-checked when this page landed all held — which is evidence, not a proof.
- **A bound is not a proof of honesty.** A bound refuses a claim that contradicts the host's own knowledge;
  it cannot detect a member who reports only things that are individually plausible (an item that exists, a
  drop the table allows, a bite on its own body). Reading this page as "reports are verified" would be
  wrong.
- **The reach is the handlers and the authority table.** A member-authored message that no handler declares
  and no wire kind wraps (a payload only the relay path reads) is outside the gate's derivation and has to
  be added by hand; the `CharacterData` row above is the shape to copy.
- **Frequency and rate are not part of most bounds.** Only `ModCommandRequest` and `PlayerPushRequest` carry
  a rate cap today; the rest bound content, not repetition.

## Related reading

- `docs/architecture/current.md` §7.1 — the admission seam and the kernel's authority model
- `docs/architecture/guards.md` — the mandatory guard list this rule belongs to
- `docs/evidence/sync-coverage-matrix.md` — the other per-surface census (event and fallback policy)
- `docs/backlog/todo/validation-and-source-checks.md` — the stages that landed the bounds above
- `docs/development/review-prompt.md` — the adversarial review that asks this question

# Remote interaction gates are judged by the host

- Status: Review
- Priority: Medium-High
- Category: Network / sync coverage / player interaction (gate authority)
- Source: User ruling 2026-09-18 (design alignment session): the line-of-sight / precondition gates of remote interactions must be judged locally by the two clients involved; a host verdict on stale streamed positions refuses interactions the actor's own screen shows as valid.
- Related: `review/enemy-hit-determination-local.md` (the same authority rule for hits), `todo/concurrent-medical-operations.md` (the reservations half of the same services), `review/remote-medical-panel-acceptance-issues.md`

## Problem (evidence)

Two host-side judgment sites decide whether a guest may interact, using data that is
stale by the peer's own latency:

- Line of sight: `src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionVisibility.cs`
  resolves both players' positions from the entity stream (the local body only for the
  local player) and runs a Ground linecast between them. It is consumed by host-side
  services — `OtherMedicalOperationSessionService`, `MedicalOperationSessionService`,
  `ShrapnelOperationSessionService`, `PlayerCarryService`, `PlayerPushService`,
  `PlayerInventoryTakeService`, `PlayerRemoteInventoryService`, `PlayerItemUseService`,
  `PlayerHealService` — so a guest-vs-guest interaction is gated by the HOST's picture of
  where both players stand. **FIXED in stage 1 (2026-09-19) — see "What landed".**
- Operation preconditions: `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/OtherMedicalOperationStartValidator.cs`
  validates the target's limb state and the operator's item from `CharacterDataMsg` inside
  `OtherMedicalOperationSessionService` ("Host-authoritative Stage 3 medical operation
  domain"), so a refusal can be produced from a report the target's own client would
  contradict. **FIXED in stage 2 (2026-09-19) — see "What landed".**

The policy is already deliberately forgiving (missing evidence does not block, only a
confirmed wall does), which limits the damage but does not change whose eyes decide. The
user's rule for this family is that the actor's own screen decides reachability, and the
target's own body decides what its body allows.

## Goal

The actor's client judges "can I reach/see the target" from its own view; the target's
client judges "does my body currently allow this operation" from its own body. The host
stops being the judge of either and keeps what is genuinely its own: conflicting-claim
arbitration and the commit/broadcast of the effect.

## Design direction (decide at implementation)

1. Visibility moves to the ACTOR's client: the scene it renders is the scene it acts in.
   The host-side services stop calling the visibility oracle for a remote actor and take
   the actor's own verdict (reported with the request), or the request is only formed
   after the actor's client has passed its local gate.
2. Preconditions move to the TARGET's client for anything about the target's body (limb
   state, existing splint/tourniquet, dislocation, shrapnel count) and to the ACTOR's
   client for anything about the actor's own items. A refusal is then a fact the refusing
   client actually observed, not an inference from a 1 Hz report.
3. The host keeps the exclusive-claim arbitration and the effect commit; the answer to a
   losing claim stays a precise, immediate refusal (accepted tradeoff, user ruling
   2026-09-18: conflict arbitration stays host-authoritative).
4. Audit the whole family, not the reported case: every `IPlayerInteractionVisibility`
   caller and every precondition validator moves in the same cycle, or is recorded with
   its reason for staying.

## What landed (2026-09-19, stage 1: the reach half)

- **The gate sits at the actor's REQUEST-FORMATION seam, not on the host's receive path.**
  Every `Send*Request` runs `HasLineOfSight(LocalSteamId, target)` on the client that forms
  the request — take, remote inventory (which is also the remote-backpack Tab transfer and
  the held-remote-item use), carry/piggyback/on-back, heal, consumable use, push, and the
  three medical start sends — and the host handlers no longer call the oracle for a remote
  actor at all. Twelve in-handler judgments were retired: nine Runtime services
  (`PlayerItemUseService` held three of them — the use path, the held-remote-item path and the
  shared execution helper — and `PlayerPushService` two, visibility plus reach) plus
  `TraderRecruitCoordinator`'s host re-check. The host's own player is covered by the same
  seam, because a host's send goes through the same method.
- **Push REACH moved with it**: the `MaxPushDistanceSq` comparison is judged by the pusher's
  own client (`HasPushReach`); what stays host-side is the degenerate-distance guard (two
  bodies at one point have no push direction) and the push cooldown, which is
  effect-frequency arbitration — uniform for every player and not a tolerance window — and
  is therefore explicitly kept rather than silently inherited.
- **Trader recruit and the remote-backpack view** already used the oracle on the actor's own
  side (`TraderRecruitCoordinator`'s send path, `RemoteBackpackCoordinator.Open`) and stay;
  the host's re-check of a guest's recruit request was deleted with the rest.
- **Refusal parity**: a medical start the local gate refuses answers with the same
  `MedicalOperationStartAckMsg` (`Accepted = false`, `RejectReason = "No line of sight."`)
  the host used to send, fired on the actor's own client, so the operator's UI sees one
  refusal shape either way. The other families never had a refusal message on the wire —
  the host logged and returned — so their local refusal is a log line too.
- **Evidence**: `InteractionGateAuthorityTests` pins both directions — a host whose own
  oracle reports "blocked" must NOT refuse an actor whose client sees a clear line (and the
  request must reach the host), and an actor whose oracle reports "blocked" must refuse
  before anything leaves the client (`CaptureHostMessages` asserts the host received no
  request frame). The pre-existing `LineOfSightRefusalTests` (both sides blocked) pass
  unchanged.
- **Prerequisite extraction**: the medical reservation bookkeeping (`_reservedItems`,
  `_reservedTargetLimbs` and the cross-service "operator busy" lambda chain) now lives in
  `MedicalOperationClaims`, one owner for the family, which is what the architecture
  watchlist demanded before anything else could land in the two 600-line services.

## What landed (2026-09-19, stage 2: the target's body half)

- **The target's own client answers for its own body.** A start whose only open question is the
  target's body is PARKED by the host and asked over a new pair of messages: the host validates
  what it owns (participants, the operator's item facts, the claims), sends
  `MedicalOperationTargetCheckRequest` (137) to the target, and the target runs the target half of
  the preconditions against its OWN live body and answers with `MedicalOperationTargetCheckAnswer`
  (138) — accepted, or its own refusal reason, plus its live shrapnel count. The host then commits
  or refuses the operator with the target's own reason. Protocol 24 → 25
  (`ProtocolVersion.Current`); no released compatibility surface exists, so a mixed-version session
  is still refused by the handshake.
- **The pending table's liveness bound is not a judgment parameter.** `MedicalTargetBodyGate` parks
  a request under the ticket the answer correlates on, and abandons an unanswered request after
  3000 ms with an explicit refusal ("Target did not answer the body check.") so the operator's UI
  cannot hang; a target that leaves first is answered the same way ("Target left before answering
  the body check."), and an operator that leaves drops the parked request. No measured latency,
  window guess or tolerance enters any verdict.
- **One target half for the whole family.** `MedicalTargetBodyValidator` is the single target half
  (limb exists, dismembered, splint/tourniquet component, dislocation, infection, live shrapnel
  count, and the per-family responsive-patient rule); the operator halves keep their own homes —
  `OtherMedicalOperationStartValidator` (item id), `ShrapnelStartValidator` (operator body,
  tweezers, claims) and the new `InjectionStartValidator` (item found + injectable). Every refusal
  reason string is carried verbatim from the pre-split code.
- **The window the answer takes is re-checked.** `MedicalStartRecheck` is the one home for "do the
  host's own facts still hold" when the verdict arrives (participants in world, operator free, item
  and limb unclaimed). A shared shrapnel session already holds its limb, so a second operator
  JOINING that session is exempted from the limb-claim half — that path is the family's
  multi-operator join, not a raced start.
- **The piece layout comes from the target's own count.** `InitializePieces` is seeded from the
  shrapnel count carried by the answer, so a session opened from a stale report no longer lays out
  the wrong number of fragments.
- **Adapter seam**: `ILocalCharacterCapture.CaptureLocal()`; `LocalCharacterCapture` (GameAdapter)
  captures the live body through the same `CharacterDataCapture` helper the save cut uses, and
  `PluginDependencyRegistrar` replaces the composition default with it exactly like the visibility
  oracle, for the same constructor-cycle reason. The default
  (`UnavailableLocalCharacterCapture`) captures nothing, and the gate then answers from this side's
  own latest snapshot; when there is none either, the verdict is a refusal ("Target body is
  unavailable.") rather than a host judgment made on the target's behalf.
- **Ordering, changed on purpose**: the checks the host can decide alone (participants, claims, the
  operator's item facts) now run BEFORE the target is asked, so a refusal the host already knows
  does not cost a round trip. Where the old code judged the target limb before the item, the item
  refuses first now — a precise refusal either way, pinned by the tests.
- **Prerequisite extractions**: the injection family's start path moved to
  `InjectionStartCoordinator` (which took `MedicalOperationSessionService` from 595 to 510 lines)
  and the shrapnel session's operator/item bookkeeping moved to `ShrapnelOperatorBookkeeping`
  (`ShrapnelOperationSessionService` 597 → 552) — the target-body verdict would otherwise have
  pushed the facade past the 600-line architecture gate and left the shrapnel service within three
  lines of it. The lifecycle after a session exists stays with each service.
- **Evidence**: `InteractionGateAuthorityTests` (13 → 18 cases) pins the target half in both
  directions — a target whose own body refuses overrides a host report that says it is fine, a
  target whose own body is fine overrides a host report that says it died, a target that cannot be
  judged is refused rather than judged by the host, the shrapnel layout follows the target's own
  count (4 live pieces where the host's report said 1), and the liveness bound abandons an
  unanswered request with its own refusal. The medical family's own suites pass with the target node
  seeded with its own body (`SeedOwnBody`).

## What remains (recorded, not silently inherited)

Both halves of the authority rule now live on the two clients involved: the actor judges its own
reach (stage 1) and the target judges its own body (stage 2). What is left is what the two stages
recorded on purpose rather than half-migrating:

- **(a) The operator's own ITEM facts stay host-side** — the item authority is the host's own table
  and the kernel gates the commit, so this is arbitration over host-owned state rather than a
  judgment about the actor's body or reach.
- **(b) The apply-time re-checks inside the appliers** are the effect's resolution against the
  authoritative state, not an authorization gate.
- **(c) Refusals for take / remote-inventory / carry / trader-recruit target-body facts** are a
  follow-up family rather than half-migrated here.
- **(d) The operator's own body facts (conscious/alive) stay with the participant gate**: the
  actor's client cannot form the request while its own body is out, and a client that lies about it
  is the anti-cheat non-goal this ticket already records.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | High-latency actor whose own screen shows a clear line to the target | The interaction is not refused for visibility |
| 2 | The actor's own screen shows a wall between the two | The interaction is refused (the actor's own view is the judge) |
| 3 | Target limb state differs between the target's body and the stale report | The target's own body decides the answer |
| 4 | Two actors claim the same exclusive unit | The host still arbitrates; the loser gets an immediate precise refusal |
| 5 | Host-local interaction | Unchanged (native path) |
| 6 | Third-party view | Unchanged: the effect and its report are the shared truth |

## Non-goals

- Removing host arbitration of conflicting claims (user ruling: it stays).
- Changing the native UI of any interaction.
- Anti-cheat hardening (a client that lies about its own reachability is out of scope; since
  the host no longer re-judges reach, a frame crafted around the client gate reaches the host
  ungated — the accepted trade of judging on the actor's client, not a gap to close here).

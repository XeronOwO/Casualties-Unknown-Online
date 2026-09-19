# Remote interaction gates are judged by the host

- Status: Todo
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
  contradict.

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

## What remains (stage 2: the target's body half)

Still open, and the ticket stays in `todo/` until it lands:

- **The target-body preconditions are still judged by the host from the target's last 1 Hz
  report** (acceptance-matrix rows 3). The settled seam for them: the host validates what it
  owns (participants, claims), parks the request, and asks the TARGET's client — the target's
  client runs the target half of the preconditions against its own LIVE body and answers; the
  host then commits or refuses with the target's own reason. That needs a new host→target /
  target→host message pair (protocol 24 → 25), a pending-verdict table with a liveness bound
  (which abandons an unanswered request and is NOT a judgment parameter), a split of
  `OtherMedicalOperationStartValidator` into an operator half and a target half, and one
  adapter seam that captures the local character on demand (`CharacterDataSync.CaptureLocal`).
  The shrapnel family must take its PIECE COUNT from that answer, because the whole session's
  piece layout is derived from it today (`InitializePieces(shrapnel, targetLimb.Shrapnel)`).
- **Recorded with a reason, not moved**: (a) the operator's own ITEM facts stay host-side —
  the item authority is the host's own table and the kernel gates the commit, so this is
  arbitration over host-owned state rather than a judgment about the actor's body or reach;
  (b) the apply-time re-checks inside the appliers are the effect's resolution against the
  authoritative state, not an authorization gate; (c) refusals for
  take/remote-inventory/carry/trader-recruit target-body facts are recorded as a follow-up
  family rather than half-migrated here.

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

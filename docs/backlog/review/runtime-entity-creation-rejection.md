# Runtime entity creation the host cannot represent must be REJECTED (and the creator's copy destroyed)

- Status: Review
- Priority: High
- Category: Network / sync coverage / world entities
- Source: user review 2026-09-09 — the round-3/round-4 handling of an unmaterializable creation was rejected as a spec violation; spec corrected in `AGENTS.md` (accept-first precondition) + decision 161
- Related: `review/runtime-entity-spawn-backfill.md` (its finding-3 fix is SUPERSEDED by this ticket), `AGENTS.md` "Accept-first sync arbitration — only for state the host can represent", `docs/decisions/active.md` #161

## Problem (evidence)

`AGENTS.md` now states the accept-first precondition explicitly: accept-first exists to avoid
hard validation and player-blocking corrections, **never to accept state the host cannot own**.
A report the host cannot represent is rejected, and the rejection must be visible.

The current code violates that. `RuntimeEntityChannel.ReportEntitySpawnUnmaterialized`
(`src/CasualtiesUnknownOnline.Runtime/Session/World/RuntimeEntityChannel.cs`, called from
`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs` in
`OnRemoteEntitySpawned`'s failure branch) **relays the creation to every member without
recording it** — neither accept nor reject:

- peers that have the prefab materialize an entity the host can never own, back up or retract;
- the reporter's own echo clears its pending entry, so the divergence is silent;
- the E3 ticket carries a "a member joining after the creation never receives it" limitation
  that only exists because of this half-state;
- a creation can therefore exist for some peers and not others with no owner responsible for
  it — exactly the unowned-accept the corrected rule forbids.

## Required behaviour (decided — do not re-litigate)

1. **Host rejects.** A reported creation the host cannot materialize is neither recorded nor
   relayed. Log at Warn with the concrete mismatch: prefab id, creation key (id + cell +
   creator + sequence), reporter SteamId, and the fact that it was rejected.
2. **The rejection is visible.** The host answers the reporter with a dedicated message
   (`NetMsg.RuntimeEntityRejected`, host → guest, creation key + reason code) so the
   reporter's `PendingEntityReportTable` entry is dropped and the fallback stops; the
   guest logs the reason ("host lacks prefab X").
3. **The creator destroys its local copy** (user decision 2026-09-09: this is the obvious
   answer — nobody keeps an entity the host cannot own). Destroy through the same death
   funnel the rest of the mechanism uses, so the pending/accepted tables are cleaned in the
   same step; locate the copy by its stamped `RuntimeEntityCreation` key (never by position),
   and make it idempotent when the copy is already gone. Log the removal with the reason.
   The round-trip window is real (the entity may have moved or been used) — the destruction
   still happens, and the log is what makes it understandable to the player.
4. **Third parties receive nothing.** A member that happens to have the prefab must not
   materialize a creation the host rejected; the session's content sets are inconsistent and
   the rejection log is the signal to fix that, not a partial relay.
5. **The accepted path is unchanged**: when the host CAN materialize the creation, today's
   accept-first flow (materialize → record → relay to every member, source included) stays.

## Acceptance matrix

| # | Scenario | Expected | Covered by |
|---|---|---|---|
| 1 | Host lacks the prefab; guest creates and reports | Host records nothing, relays nothing, logs the mismatch; no third-party materialization | `RuntimeEntityRejectionTests.UnmaterializableGuestCreation_IsNeitherRelayedNorEchoed`; `RejectedCreation_AnswersTheReporterAndStopsTheFallback` |
| 2 | The reporter receives the rejection | Pending entry dropped; the local copy destroyed via the death funnel; warning logged | `RejectedCreation_AnswersTheReporterAndStopsTheFallback` (pending + the removal request raised with the exact key); the adapter's Unity removal is game-typed — code-reviewed, unified dual-client pass |
| 3 | The rejection message is lost | The fallback re-reports; the host rejects again (idempotent); the state converges on the next successful rejection | `LostRejection_TheFallbackReReports_AndTheHostRejectsAgainIdempotently` |
| 4 | The rejection arrives after the local copy already died | No-op (key gone) | `RepeatedRejection_IsIdempotent`; the adapter's `FindByCreationKey` null path is code-reviewed |
| 5 | The local copy moved before the rejection arrived | Found by creation key and destroyed (position-independent) | the wire carries the creation key, never a position (`NetPacketTests.RuntimeEntityRejected_CreationKeyAndReason_RoundTrip`); the adapter locates by `FindByCreationKey` (code-reviewed — the test host cannot bind the Unity members) |
| 6 | The host HAS the prefab | Unchanged accept-first path: materialize + record + relay | `GuestEntityReportRecoveryTests` (16 cases, unchanged) |
| 7 | A member joins after a rejected creation | Nobody has it (converged) | the host table stays empty (`UnmaterializableGuestCreation_IsNeitherRelayedNorEchoed`), so no world-entry snapshot can carry it |
| 8 | A non-reporter receives the rejection message | Ignored (direction-locked host → guest; only the reporter's own key matches) | `RejectionForAnotherCreatorsKey_ChangesNothing`; `HostToGuestDirectionTests` direction row |
| 9 | A hand-built report names another member in its creation token | Still answered and acted on: the pending table is the proof THIS member reported it | `RejectionForAKeyThisMemberReported_IsActedOnEvenWhenTheTokenNamesAnotherCreator`; `PendingEntityReportTableTests.Contains_SeesExactlyThePendingEntries` |

## Landed (2026-09-17)

- **Wire.** `NetMsg.RuntimeEntityRejected = 135` (host → guest),
  `RuntimeEntityRejectedMsg` (the creation `RuntimeEntityKeyMsg` + a
  `RuntimeEntityRejectReason` code), `RuntimeEntityRejectedHandler` direction-locked at
  `NetMessageDirection.HostToGuest`, and `ProtocolVersion.Current` 20 → 21 with its
  compatibility note.
- **Host.** `RuntimeEntityChannel.ReportEntitySpawnUnmaterialized` now logs the concrete
  mismatch (prefab id, creation key, reporter SteamId, the rejection) and answers THAT
  reporter; nothing is recorded in `RuntimeEntityRegistry` and nothing is broadcast.
- **Reporter.** `RuntimeEntityChannel.FireRuntimeEntityRejectedReceived` acts only when the
  creation is this member's own: the creation token's creator half equals the local SteamId,
  OR the pending table still holds that report — the proof that this member really reported
  it (a mod-built report through the public send surface can name another creator in its
  token, and the creator half alone would then leave that entry re-reporting forever). It
  drops the matching `PendingEntityReportTable` entry (so the fallback stops) and
  raises `RuntimeEntityRejectedReceived` with the key and reason.
- **Adapter.** `EntitySpawnSync.OnRuntimeEntityRejected` locates the local copy by its
  stamped creation key — never by position — and hands it to the same death funnel every
  remote death uses: `RemoteEntityDeath.Mark` plus a health below the death threshold, so
  the game's own `BuildingEntity.Update` removes it without rolling a second drop set, and
  the creation record is dropped in the same step. A copy that is already gone is a logged
  no-op. The marker helper moved onto `RemoteEntityDeath` itself, so one rule serves every
  remote death (behaviour-preserving extraction).
- **Tests.** New `RuntimeEntityRejectionTests` (6 cases), one `PendingEntityReportTableTests`
  case, one `NetPacketTests` round-trip, one `HostToGuestDirectionTests` row; the superseded
  `UnmaterializableCreation_IsRelayedAndAcknowledgedWithoutBeingRecorded` is deleted (its
  subject no longer exists) and `UnmaterializableReport_OnAGuest_IsNotRelayed` stays.
- **Evidence.** Matrix row E3 (description, evidence, loss semantics), the wire vocabulary
  index, the closed-gap row and six new `sync-coverage-evidence.json` anchors;
  `review/runtime-entity-spawn-backfill.md` finding 3, round-4 finding 2, acceptance row 11
  and the deleted late-join limitation.
- **What the tests cannot prove.** Every `RuntimeEntityRejectionTests` case asserts the
  channel's half: replacing the body of `EntitySpawnSync.OnRuntimeEntityRejected` with an
  early return leaves all of them green, because the adapter's `FindByCreationKey`,
  `RemoteEntityDeath.Mark` and the `health` write reach Unity InternalCall members and the
  method is therefore un-JIT-able in the test host. Requirement 3's removal is in the
  code-reviewed + unified dual-client column, like the rest of the adapter shell; it is
  spelled out here so a "N cases" summary is never read as covering it.

## Red before green (recorded)

`RuntimeEntityRejectionTests.UnmaterializableGuestCreation_IsNeitherRelayedNorEchoed` was
written and run against the unmodified tree first (it uses only pre-existing API, so the red
is a real assertion failure, not a compile error):

```text
[xUnit.net] CasualtiesUnknownOnline.Tests.World.RuntimeEntityRejectionTests.UnmaterializableGuestCreation_IsNeitherRelayedNorEchoed [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 0
  Actual:   1
  at RuntimeEntityRejectionTests.cs
```

That is the "no relay to a third member" half (the third-party guest received the creation
the host could not materialize). The "reporter's local copy is destroyed" half is asserted
through the rejection event, which needs the new message type, so it arrives with the
implementation; the whole 6-case matrix then runs green (affected families 91/91, full suite 3202 + 32 gates, `dotnet format` exit 0).

## Implementation notes

- New wire message: `NetMsg.RuntimeEntityRejected` (host → guest), carrying the creation key
  (`RuntimeEntityKeyMsg`) + a reason code; `ProtocolVersion.Current` bump; the matrix's wire
  vocabulary index entry + a `NetPacketTests` round-trip; the evidence anchors for the new
  members. **Done.**
- The adapter owns the destruction: `EntitySpawnSync` (or its channel bridge) finds the copy
  by `RuntimeEntityCreation` key and routes it through the death funnel; `BuildingEntity`'s
  own death hook then drops the pending entry as usual. **Done.**
- Reason codes: start with one (`PrefabUnavailable`); extend only when a second real cause
  appears. **Done — one code.**
- Adjacent case deliberately left alone: a GUEST that cannot materialize a creation the host
  DID accept (its own content set lacks the prefab) keeps the existing behaviour — the shared
  `RuntimeEntityFactory` logs a Warn per failed materialization and the host's absolute
  snapshot keeps re-offering the record. A guest-side refusal would be a second protocol
  direction and is not part of this ticket's frozen scope.
- Residual found by the adversarial pass and recorded, not fixed: a rejected ANIMAL creation
  is destroyed without passing through `EnemySyncCoordinator.OnEnemyRemoved`, so the guest
  enemy domain's `_runtimeAnimalCopies` set keeps the destroyed reference until its next
  `Reset`. Its only consumer filters LIVE `FindObjectsOfType` instances, so behaviour is
  unaffected — a bounded reference leak, not a desync, and not worth a cross-domain call in
  this cycle.

## Docs updated (2026-09-17)

- `review/runtime-entity-spawn-backfill.md`: finding 3 and round-4 finding 2 marked
  superseded (reject, not relay), the "late joiner never receives it" limitation replaced by
  its resolution, acceptance row 11 re-pointed.
- `docs/evidence/sync-coverage-matrix.md`: row E3's description/evidence/loss-semantics
  cells and the wire vocabulary index; the closed-gap row.
- `docs/evidence/sync-coverage-evidence.json`: anchors for the new message, handler, DTO,
  channel entry point and adapter removal entry point.
- `docs/backlog/README.md`: index entry moved to the Review section.

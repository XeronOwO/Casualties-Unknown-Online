# A `/heal` reset cannot restore the host-owned vitals

- Status: Review — fixed by the applied authority event (`HealResetApplied`), waiting for the
  acceptance batch
- Priority: High
- Category: Player state / cross-player sync / authority
- Source: found by acceptance batch `20261018-x` while accepting
  `done/health-simulation-and-host-authority.md` (`0.1.0+85ff4f76`); the batch's heal probe left a
  member at about -99.7 blood on both sides after `/heal @a`, although the reset table sets
  `BloodVolume = 100`.
- Related: `done/health-simulation-and-host-authority.md` (the host-owned values and the report
  filter this ticket must preserve), `done/heal-cheat-command-state-reset.md` (the `/heal` surface),
  `done/validation-and-source-checks.md` (the authority split), decision 184 (the affected client
  judges its own body), decision 265 (data-anchored validation)

## What happens

A member whose host-model blood volume is negative (or otherwise outside what a `SaveReportedCharacterData`
report may state) runs `/heal`. The reset reaches the target's own client and applies the full
table — `HealResetState.BloodVolume = 100f` among it — and the client looks reset locally. Its
re-report, however, is a plain 1 Hz character snapshot: `CharacterDataStore.SaveReportedCharacterData`
calls `ContinuousVitals.Preserve`, which keeps the HOST's blood volume, total bleeding speed and
per-limb bleed. The host's saved snapshot therefore stays at the pre-heal values, the 10 Hz
`PlayerVitals` row keeps pushing them, and the target's body falls back to the negative/old values
within one row. The reset's other fields (limbs, consciousness, brain health, the hollow) do reach
the host through the report because they are not host-owned; only the three owned values are lost.

Evidence: batch `20261018-x` artifact `63-host-heal-all.json` (the host's `/heal @a` answer says the
instruction was sent), `66-host-health-healed2.json` and `67-guest-health-now.json` (host saved and
guest local both about -99.7 after the reset), and `70-host-force-heal.json` + `72/73` (the same
host-side authority write recovered both sides to 100). The batch had to rescue its own session
with a test-only `SaveCharacterData` write to continue.

## Why the obvious fix is not enough

Weakening `ContinuousVitals.Preserve` is not acceptable: it is what stops every plain report from
stating the output (the ticket's Stage 1 review B1). Writing the target's reset result through the
report path again would be the same bug. The reset must travel as an authority event, the way a
revive, an amputation and the owner's injury event already do: the target's own client is still the
judge that APPLIES the reset, and the host is the writer that records the resulting owned values.

## Fix direction

- The target's client, after `ILocalBodyReset.Reset()` succeeds, captures the post-reset full
  health/limb state and sends it as an authoritative event (a dedicated message or an
  event-shaped extension of the existing heal-reset flow), bound to the target's own identity.
- The host handles that event through the character data's AUTHORITY path (`SaveCharacterData`'s
  in-place event-merge sibling `ApplyHealResetEvent`, not `SaveReportedCharacterData`); it keeps the
  existing finite guard, refuses the event whole by name when non-finite, and bounds its three owned
  values against the reset table the host itself ordered (the implementation detail the review added).
- The host's clone recognizes the write through `ContinuousVitals.Differs` as it does for a revive
  and re-seeds the body; the next 10 Hz row carries the healed values back to the owner.
- The plain report filter stays exactly as it is: a plain report must still be unable to raise or
  lower the three values. A heal is an event, not a report.
- Check the mirrors of this family: `TraderRecruitPolicy.PrepareRevive` and the save restore already
  write blood 100 through the authority path; `/heal` is the one medical action whose result rides
  only a report today. Any other command that resets a member's body (the native `heal` on a remote
  body, a future respawn) must be checked the same way.

## Acceptance

- A member whose host-model blood is negative (the batch's setup) is reset with `/heal`; both the
  host's saved snapshot and the target's local body read 100 (and the reset's limb/consciousness
  values) after the row cadence, and the 10 Hz row does not revert them.
- The same with a permitted member running `/heal` on another member; the host's permission checks
  are unchanged.
- A plain report sent right after the reset still cannot state the three owned values; the
  reconciliation may warn once about the sampling skew, and the host's values stand.
- A non-finite reset event is refused whole by name, and the previous snapshot stays.

## Fix landed (2026-10-18)

- **Wire.** `HealResetInstructionMsg` carries the host's `InstructionId`; the new
  `HealResetAppliedMsg` (NetMsg 155, reliable target → host) carries the target's `OwnerSteamId`,
  the echoed `InstructionId`, the post-reset `Health` and the full limb list. It names only the body
  half — items, skills, position and native fields stay the host's stored copy. Both are declared
  in the sync-coverage matrix row I13 (seven evidence anchors) and the protocol tables; the census
  row in `docs/architecture/member-report-validation.md` states the bound.
- **Target side.** `ILocalBodyReset.Reset(ulong? instructionId)` takes the host's ticket for a host
  instruction and null for this client's own console/host-self path. After the reset is applied,
  `LocalBodyReset` captures the POST-reset body (not the table) and sends the applied event first,
  then the ordinary snapshot: the host's copy must hold the reset's blood volume/bleeds before the
  report filter runs on the snapshot. A host self-reset sends no event — its own snapshot is
  already the authority write.
- **Host side.** `HealResetAppliedHandler` → `PlayerHealResetService.HandleApplied`. The result is
  accepted only when its owner is the transport sender AND the echoed instruction id is the one
  this host issued to that member and has not resolved; an unsolicited, another-owner, superseded or
  expired result is dropped by name and writes nothing. The outstanding instruction lives in
  `PlayerHealResetService` (target-keyed, one id each, 30 s liveness bound that only abandons an
  answer that never comes; dropped on session end through `ISessionReset`).
- **Store.** `CharacterDataStore.ApplyHealResetEvent` (the merge rules live in the extracted
  `CharacterEventMerges`) refuses a result with no health record, no limbs, any non-finite field or
  any of the three owned values that is not the reset table's healthy constant BY NAME (the previous
  snapshot stays). The constant check is the host's own data anchor: `HealResetState` is the table the
  host's instruction ordered, so a member can only state the reset it was told to apply — not an
  arbitrary blood volume (200) or bleed. An accepted result replaces the saved snapshot's health and
  limb set, bumps `LocalRevision` and syncs the kernel projection — the same in-place merge shape the
  limb event uses, except the three host-owned values are now KEPT as written. The host clone's
  `ContinuousVitals.Differs` sees them and re-seeds the body; the next 10 Hz row carries the healed
  values back to the owner. The event-merge seams (the enemy terminal states, the limb/injury event
  and the reset result) moved out of the store into `CharacterEventMerges` in the same change, which
  keeps the store under its recorded aggregate-line ceiling; the store keeps one forwarding call per
  event and owns no interpretation of the payloads.
- **Transport honesty.** The event is the only carrier of the three values, so its send is not
  fire-and-forget: `TrySendHealResetApplied` returns the transport's verdict and
  `HealResetReportRetry` re-offers a frame the transport did not accept on the report cadence (five
  attempts, a warning per failed attempt and an error when the budget is spent — a named limit, not a
  guarantee). The retry is dropped with the body or session it belongs to (`Clear`), so a result
  captured from one body can never be written onto a later one, and a backwards clock is rebased.
  The host's own instruction uses `TrySend` too: when the transport refuses it, no pending entry is
  recorded and the operator reads "the transport did not accept the reset instruction, so nothing was
  sent" instead of "sent". An accepted-then-lost frame is Steam's reliable channel's problem; the
  host's instruction expiry is where that surfaces.
- **Plain report unchanged.** `SaveReportedCharacterData`/`ContinuousVitals.Preserve` are untouched:
  a plain report still cannot raise or lower the three values; a heal is an event, not a report.
- **Red, recorded before the fix.** The defect was found and made visible by acceptance batch
  `20261018-x`: host saved and guest local both read about -99.7 after a sent `/heal @a`
  (`63/66/67`), and the host-side `SaveCharacterData` rescue recovered both sides to 100
  (`70/72/73`) — the authority shape this fix uses. The new tests pin the fixed behaviour:
  `CommandConsoleHealResetTests.Host_HealsAMember_StoresTheAppliedResultThroughTheAuthorityPath`,
  `.AnAppliedReset_IsNotRevertedByThePlainReportThatFollows`,
  `.A_NonFiniteAppliedResult_IsRefusedWholeByNameAndTheSnapshotStays`,
  `.AnAppliedResultClaimingValuesTheResetTableDoesNotWrite_IsRefusedWhole`,
  `.AnAppliedResultWithNoOutstandingInstruction_WritesNothing`,
  `.AnAppliedResultNamingAnotherOwner_IsDropped`,
  `.AnAppliedResultForASupersededInstruction_WritesNothing`,
  `.AnAppliedResultPastTheInstructionLivenessBound_IsRefused`,
  `.AnInstructionTheTransportRefuses_IsAnsweredAndLeavesNoPendingReset`,
  `.Host_SelfReset_AsksTheBodyWithNoInstructionId`, and
  `HealResetReportRetryTests` (the retry cadence and the budget).
- **Remaining.** The four acceptance rows above are not yet judged; the deployed artifact must be
  re-read once for a `[Vitals] cost` line after the deploy.

## Independent review (2026-10-18, frozen tree)

The change set was reviewed adversarially in a fresh context before the commit (full tier: the
protocol, the cross-player write and the player-visible verb). Everything below was fixed in the same
change unless it says otherwise.

- **Major — the three owned values were taken on the member's word.** The receiver does hold the
  anchor: `HealResetState`'s constants are in the same assembly. Fixed with the table check above
  (`health.BloodVolume`/`health.TotalBleedSpeed`/`limb N.BleedAmount` against the table, whole-event
  refusal by name), and the census row now states this bound instead of claiming "no second
  simulation".
- **Major — a locally suppressed send was logged as success and never retried.** Fixed with
  `TrySendHealResetApplied` + `HealResetReportRetry` on the report cadence, and with the host's
  instruction now answering a refused send honestly. The residual (a frame the transport ACCEPTED
  but Steam did not deliver) is named in the census row, the matrix and the limits below.
- **Minor — `TotalBleedSpeed` was not the post-reset value.** Fixed: the table now writes
  `TotalBleedSpeed = 0`, and the host's anchor requires it.
- **Minor — a stale `NetMsg` paragraph still said the result rides the ordinary snapshot.** Fixed.
- **Minor — four places said the host writes through `SaveCharacterData`, which the in-place merge
  never calls.** Fixed in `NetMsg.cs`, `HealResetAppliedMsg.cs`, `ILocalBodyReset.cs` and this
  ticket's fix direction; the wording now names the authority-merge seam (`ApplyHealResetEvent`).
- **Minor — the instruction was consumed before the write, contradicting "first accepted result".**
  Fixed: it is removed only after an accepted merge; a refused result leaves it outstanding until it
  expires.
- **Minor — "expiry is logged" is conditional.** The matrix and the limits below say when it is
  logged: the next heal/result prunes it. No pump was added for a state that normally lives for one
  round trip.
- **Nits.** The liveness-bound wording no longer claims it can never abandon a (very late) legitimate
  result; the internals pages say a MEMBER target reports twice; the message docs name the
  superseded/expired grounds; the send seam stamps `OwnerSteamId` itself, so the event cannot claim
  another body even from a future caller.
- **Mirror audit (the ticket's own "check the mirrors" item).** `PlayerHealService`,
  `TraderRecruitCoordinator`, `RespawnCoordinator`, `OtherMedicalRemovalApplier`,
  `OtherMedicalOperationApplier`, `ShrapnelSessionStateWriter` and `WorldCharacterBinder` already
  write through `SaveCharacterData`; `/heal` was the only medical writer riding the report path, and
  the applied event closes it.

### Delta review (the fixes above)

A second, narrowed review read the patch only. No blocker and no major; six minors were fixed in the
same change:

- The two code comments and the ticket still attributed the write to `SaveCharacterData`; corrected.
- The retry called every non-send "transport suppression" and outlived its session. Fixed: the log
  and the contract say "not accepted", the non-guest/inactive case is named in the contract, and the
  retry is cleared on session end and on body leave.
- The retry comment claimed the pump runs without a body; it is body-gated. Fixed: the comment tells
  the truth and a body leave drops the pending result (so a stale capture cannot land on a later
  body).
- `ICharacterDataControl.ApplyHealResetEvent` now names the empty-limb and table-mismatch grounds.
- The instruction-timeout comment no longer claims it only drops answers that never come (a >30 s
  legitimate result is dropped too; the bound is liveness, not judgement).
- The refused-instruction test now asserts the pending table is empty through an observability seam
  instead of relying on the instruction id's start value; the retry tests were extended for `Clear`
  and a backwards clock.

### Limits

- The four acceptance rows are judged in the batch record, not by the unit suite.
- A result the transport accepted but the Steam channel did not deliver leaves the host's three
  values unhealed until the next reset. The target retries only a send the transport did not accept;
  the host logs the instruction when the next heal/result prunes it. Steam's reliable channel makes
  this rare, but it is not impossible (peer gone).
- The accept-first content (the non-owned body/limb readings) is the target's own body: the host
  cannot verify the body was really mutated, only that the claimed values are the reset it ordered.
- Two heals racing: the earlier result is dropped by name (superseded) and the newer one writes.

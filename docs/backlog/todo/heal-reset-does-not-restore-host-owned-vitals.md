# A `/heal` reset cannot restore the host-owned vitals

- Status: Todo
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
- The host handles that event through `SaveCharacterData` (the authority path), not
  `SaveReportedCharacterData`; it keeps the existing finite guard and refuses the event whole by
  name when non-finite.
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

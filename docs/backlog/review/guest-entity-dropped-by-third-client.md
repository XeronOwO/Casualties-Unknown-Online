# A third client drops another member's entity as "left" while the host still has it

- Status: Review — the CAUSE is established and FIXED on 2026-11-01 (`SessionPeerMaintenance`: a member is removed only once `MemberMissingChecks = 3` consecutive lobby reads omit it, and a reading that names nobody is skipped), with four new tests, three updated ones, and a live reading on the deployed build (two "absent … not removing it yet" lines, then the removal when a client really was killed). What this ticket still holds is the remaining half: a member whose session row IS removed while its transport stays up has no path back except a fresh handshake.
- Priority: High — it makes a third member invisible to another member's medical view and to any feature that gates on presence.
- Related: `concurrent-medical-operations` (row 7 needs exactly this fixture), `remote-medical-stage-3-other-actions`.

## What was observed

A three-client session (host, guest, alt). The guest's round-trip time read `rtt=449` in the host's `/players`, the alt's `19`. The alt stood next to the guest and asked the host-authoritative chart for the guest:

    [MedicalView] refused open for <guest>: not in-world.

The alt's own log, in order:

    Roster join: member <guest> …
    Peer <guest> scene state: InWorld        (line 1317)
    …
    Member <guest> left (PlayerLeave …)      (line 2851, ~9 lines before the refusal)
    [MedicalView] refused open for <guest>: not in-world.   (lines 2860, 2880)

So the alt's presence row for the guest was `InWorld = true`, then a `PlayerLeave` from the host cleared it, and nothing re-created it. The host kept simulating the guest the whole time: the host opened the same chart successfully, treated the guest's limb, and the guest's own client answered every probe.

The two-client direction is fine: the guest opened the HOST's chart in the same session (`opened=True`), so this is specifically about one MEMBER's view of another MEMBER.

## First suspects (to be read, not guessed)

1. **`PlayerLeave` sent for a still-present member.** The host's `PlayerRosterAnnouncer.AnnounceLeave` runs when a member leaves; something decided the guest left. The guest re-entered the scene during the session (its own log shows a `SceneState: InWorld` later), so a leave/join pair around the re-entry is the first thing to read: whether the host announced a leave and failed to announce the join to the OTHER members.
2. **A timeout on the observing side.** The guest's RTT (449 ms) is the outlier in that session; if the alt's entity sync drops a member whose stream is slow, the drop would look exactly like a leave.
3. **The presence row is rebuilt without its scene state.** `PlayerJoinHandler` stores the display name and colour on `GetOrCreateMember` but not `InWorld`; a row that is removed and re-added (a leave/join pair, a reconnect) starts `InWorld = false` and only a fresh `SceneStateMsg` fixes it. `EntitySyncService` sets `InWorld = true` for the HOST on the same message, which is the precedent for the fix if this is the shape.

## Why it matters

`ISessionControl.IsRemoteInWorld` gates the remote medical view, and any other feature that reads presence would gate the same way. A member who cannot be seen by another member is a co-op defect, not a diagnostic quirk: in a three-player session the third player cannot treat the second one's wounds at all.

## Next reading

- The host's log for the session, around the guest's scene re-entry: whether an `AnnounceLeave` was sent and whether the matching `PlayerJoin` reached the alt.
- `EntitySyncService`'s member-timeout path: its threshold and what it does to the presence row.
- Then the fix, with a test that a member who re-enters keeps a truthful `InWorld` on every other member's table.

## Design

Read before touching this: [`docs/design/model/session-lifecycle.md`](../../design/model/session-lifecycle.md) (lobby, run, rejoin —
the table whose membership this rule maintains) and [`docs/design/mechanics/medical-view.md`](../../design/mechanics/medical-view.md)
(the third-party gesture that refuses when presence is wrong). The rule this ticket exercises: a member's presence is a SESSION fact
owned by the host's table and mirrored to every member, so a mirror that can lose a member who is still playing is a defect in the
mirror — never in the features that read it.

## What was fixed (2026-11-01, same day)

The host's removal rule was the guest side's rule without its safeguard: ONE lobby read that omitted a member removed it
(`Member <guest> removed: left the lobby`), while the guest branch had already learned that one reading is not a verdict
(`HostMissingChecks`, measured 2026-10-28). `SessionPeerMaintenance` now applies the same corroboration to members:
`MemberMissingChecks = 3` consecutive readings, a reading that names NOBODY is skipped entirely, and each intermediate reading
logs `Member … is absent from lobby read N/3; not removing it yet` — so the next investigation starts from a line rather than a
guess. Four tests pin it (`AHost_KeepsAMemberWhenOneReadingOmitsIt`, `…WhenTheReadingNamesNobody`,
`…RemovesAMemberOnlyOnceConsecutiveReadingsAgree`, `…ForgetsTheAbsenceWhenTheMemberComesBackInBetween`), and the three tests that
encoded the old one-reading rule were updated to the new contract.

The remaining half — a member that IS removed and then resumes traffic without a fresh handshake — stays open: members are
re-added by a handshake, and a member whose session row was dropped while its transport stayed up has no path back. That is the
next reading here.

### The corroboration, read live (2026-11-01, 11:20)

The deployed build, three clients, then the alt killed on purpose (its own sandbox pid, so the host and guest kept running). The host's log, in order:

    Member 76561199526807662 is absent from lobby read 1/3; not removing it yet.
    Member 76561199526807662 is absent from lobby read 2/3; not removing it yet.
    Member 76561199526807662 removed: left the lobby.

So the rule still removes a member who really left (a kill is exactly that), only later — and the two intermediate lines say why the earlier false removal cannot recur in one snapshot. A two-client session with all three present kept every row (`/players` answering all three `handshake=True inWorld=True`).
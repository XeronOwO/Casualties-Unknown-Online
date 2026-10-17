# Acceptance record — cheat permission, `/op` and `/deop`

- Ticket: `cheat-permission-and-op-commands` — verdict: **stays in `review/`** — rows 1-5 pass, row 6
  (IP-direct) is `unproven` for a dependency this batch does not have.
- Batch: `20261017-q` — tickets `give-item-to-a-player-command`, `item-and-entity-data-commands`.
- Commit: `bb2350e7` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+bb2350e760208d6e3cce3effd5917caef141bae4` (verified by `tools/verify-deploy.ps1`, exit 0).
- Run: 2026-10-17 21:46:18 → 21:52:14 · Host: physical machine · Member: `Steam1` sandbox.
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts`.
- Artifacts: the `a*` readings and `r2-host-log.txt`/`r2-guest-log.txt` in the directory named by
  `acceptance-artifacts-dir`. Steam IDs are written `<host>`/`<member>` below; the artifacts carry the exact
  values.

The session's starting state, stated from the clients themselves: host rules `allowMemberCheatVerbs:false`
(`q2-host-rules.json`), the member's announcement present and `forbidsMemberCheatVerbs:true`
(`q3-guest-rules.json`), the roster empty. Every unit sets the two rulings explicitly before judging.

| # | Row (the ticket's own) | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Switch closed, grant present: the member's `/data set` reads the SWITCH's sentence; the host's `/data get id` still shows the old value | machine | **pass** | `a1-host-op.json`: `Granted the cheat verbs to <member>…` (the grant is present); `a2-guest-set-switch.json`: `Refused locally: the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run a cheat verb (the host announces its rules to this client). Nothing was done here.`; `a3-host-read.json`: `condition 1`, `revision 1` (unchanged) |
| 2 | The host opens the switch and grants nobody: the same command reads the ROSTER's sentence | machine | **pass** | `a4-host-deop.json` (roster emptied), `a5-host-rules-open.json` (`Updated 1 host rule(s).`), `a6-guest-rules.json`: `allowMemberCheatVerbs:true, forbidsMemberCheatVerbs:false`; `a7-guest-set-roster.json`: `Refused locally: the host's cheat roster (announced to this client) does not name you, so /data set cannot land. The host grants it with /op. Nothing was done here.`; `a8-host-read.json`: still `condition 1` |
| 3 | The host runs `/op <member>`: the same write lands and BOTH peers read the new value | machine | **pass** | `a9-host-op.json` (granted); `a10-guest-set.json`: `condition 1 -> 0.3 — written here and reported to the host…`; `a11-host-read.json` and `a12-guest-read.json` both: `condition 0.3`, `revision 2` |
| 4 | The host runs `/deop <member>`: the same write is refused on the member's own console and both peers still read the previous value | machine | **pass** | `a13-host-deop.json` (revoked); `a14-guest-set.json`: the roster's `Refused locally:` sentence; `a15-host-read.json` and `a16-guest-read.json` both: `condition 0.3`, `revision 2` |
| 5 | The member types `/op <itself>`: host-only; the host types `/op <its own id>` and `/op 999999`: each refused by name with its own sentence | machine | **pass** | `a17-guest-op-self.json`: `Permission denied: this command is host-only.`; `a18-host-op-own.json`: `The host's own cheat permission is the online switch: it is on while the session runs, and the roster does not change it.`; `a19-host-op-unknown.json`: `999999 is not a handshaken member of this session.` |
| 6 | IP-direct: the host's `/op` is refused with the IP-direct sentence and the member's cheat verb is refused too | machine | **unproven** | no transport lever in this batch: the session was created over Steam (the lobby is a Steam lobby), and the driver's vocabulary has no switch to an IP-direct transport. The refusal paths are unit-covered (`CheatPermissionsTests.AnIpDirectSession_GrantsNobody_AndSaysWhy`, `CheatPermissionReplicationTests.AnIpDirectHost_RefusesTheOpAndKeepsTheRosterEmpty`), which is not acceptance evidence and is not claimed here |

## What the run adds beyond the rows

- **The refusal names which of the two rulings is missing**, and the two sentences are distinct — the
  switch's (`a2`) and the roster's (`a7`, `a14`) — which is the ticket's own "the refusal names which of
  the two is missing" claim, read on the member's console.
- **A refused write leaves the record untouched on BOTH sides** (`a3`, `a8`, `a15`, `a16`), not only on the
  member's own client: the host's row is the same value before and after.
- **The grant/revoke announcement reaches the member within one command round trip**: every row's local
  verdict matches the host ruling written in the previous command (`a5` → `a6`, `a9` → `a10`, `a13` →
  `a14`), with no retry and no repair cycle needed.
- **The host's own verb needs neither ruling**: row 3's grant is what admits the member, but the host's own
  `/data set` in the `/give` unit's row 1 (`c2-host-set.json`) landed with the switch closed at that time —
  the host is exempt exactly as the ticket states.

## Residuals for the user

None. Every judged row is a console answer read from the client that printed it.

## Limits

- **Row 6 is unproven, not weakened** (above). The ticket keeps its `review/` seat until an IP-direct
  transport can be driven.
- **One member.** "A member cannot op another member" is unit-covered; the batch could not give it a second
  member to try.
- **The switch's persistence across a restart was not re-read.** The ticket records that the config value
  survives a restart while the grants do not; this run set the value explicitly and states the starting
  value instead of inheriting it.
- **The run did not exercise the repair-cycle healing path** (a stale CLOSED switch healed by an entry or a
  repair group); it read the immediate announcements only.

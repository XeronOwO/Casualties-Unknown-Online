# Acceptance record — A member out of the world writes one Warning per clone per frame, unbounded

- Ticket: `remote-clone-warning-storm-on-member-dropout` — verdict: **pass, moved to `done/`**: on a member
  that is out of the world the line fires three times per (failure, member) subject and then reports one
  summary per run, and every client's growth read as a size is a fraction of a megabyte against the
  ticket's 8.796 MB reading.
- Batch: `20261007-c` — tickets `remote-clone-warning-storm-on-member-dropout` and
  `layer-change-member-dropout` (whose fixture drove the state this row needs; its own record is
  `layer-change-member-dropout-20261007-c.md`)
- Commit: `870caead` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+870caeadd3a3b0e91987a71f9ec4585eb9700bcd`
- Run: 2026-10-07 11:21 → 11:26 local · Host: physical machine (the operator) · Guest: sandbox `Steam1` ·
  Third client: sandbox `Steam2`
- Dependencies: the eleven the preflight reported present
- Artifacts: `20261007-c/` in the directory named by `acceptance-artifacts-dir` — the mark
  `m2-before.txt`, the change probe `pB-skiplayer.json`, the state reads `tB1-*` / `tB2-*`, the runbook's
  fixture reads `s0-*`, and the excerpts `ev-guest-remote-body.txt` / `ev-alt-remote-body.txt`

## The row as planned before the run

The ticket's *Required work* 4: read the storm's own cost as a SIZE, not as a line count, expecting at most
six lines per member — three per subject — plus at most one summary per run that ended, against the 58,148
lines of batch `20261007-a`'s reading.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Driven OUT of the world, each client's clone-failure line is bounded per (member, failure) subject and reports its swallowed volume once per run | machine | **pass** | Attempt B, census since `m2-before.txt` (11:24:50 → 11:25:21, ~31 s): `no Body component` — guest **8**, alt **8**, host **0**; `not found in scene` 0 everywhere. The 8 lines are two subjects × (3 repeats + 1 summary), quoted below |
| 2 | The same clients' log growth read as a size | machine | **pass** | guest 0.207 MB / 1,234 lines, alt 0.206 MB / 1,226 lines, host 0.039 MB / 207 lines over the same ~31 s window — ~0.40 MB/min against the ticket's 7.25 MB/min (1.212 MB / 10 s) and 8.796 MB / ~90 s |
| 3 | *(control, both members held in the world)* The line does not fire when no member is out | machine | **pass** | Attempt A (both members parked through the change, `layer-change-member-dropout-20261007-c.md`): `no Body component` **0**, `not found in scene` **0** on all three clients |

## The state the row needs, and what the lines looked like

Attempt A drove the change with both members' inbound parked; attempt B drove the same command with no park
and put the members out of the world again, which is the state this producer repeats in:

- `tB1-guest-state.json` / `tB1-alt-state.json` read `inWorld: false` at +6–9 s with
  `container-read mode=local` answering `localCount: 0`; `tB2-*` read `inWorld: true` for both at +30 s, so
  the out-of-world window the line repeated in was about 8 seconds.
- The guest's log (`ev-guest-remote-body.txt`) carries exactly two subjects, each three lines plus one
  summary — the member it renders is named in every line, which the storm's own log could not do:

```text
[WRN] […RemotePlayerRenderer] Remote body: no Body component in "Experiment" clone for <member-id> (repeat 0).
[WRN] […RemotePlayerRenderer] Remote body: no Body component in "Experiment" clone for <member-id> (repeat 1).
[WRN] […RemotePlayerRenderer] Remote body: no Body component in "Experiment" clone for <member-id> (repeat 2).
[WRN] […RemotePlayerRenderer] Remote body: no Body component in "Experiment" clone for <member-id> — 156 identical line(s) suppressed while the clone could not be built.
```

- The two subjects are the OTHER member (first run, 11:24:52.385 → 11:24:52.603, 156 suppressed on the
  guest and 108 on the alt) and the HOST (second run, 11:24:59.153 → 11:25:00.029, 207 suppressed on the
  guest and 221 on the alt). The second run starts at `repeat 0` again, which is the re-armed window the
  ticket claims.
- **What the bound swallowed is the size of the storm.** 363 lines on the guest and 329 on the alt were
  refused inside ~8 seconds — the pre-fix shape would have written them, which is what batch
  `20261007-a` measured as 58,148 lines at 639–866 lines/s.
- **The two failure wordings stay separate subjects**: `not found in scene` never fired in this window, so
  the run cannot say that the two keys separate in practice; it says only that the `no Body component` key
  is bounded and names its member.
- **Nothing throws**: `[ERR]`, `[ERR][Unity:Exception]` and any `Exception` line read 0 in all three
  clients over both attempt windows, and `[LayerMod] baseline divergence` read 1 per member (the corrected
  5-second keyframe cadence).

## Residuals for the user

None: every row above is a machine row.

## Limits

- **The out-of-world window was ~8 s**, because the members returned on their own at +30 s; the row's
  per-subject bound is what the window proves, and the MB figures are read over ~31 s rather than the
  minutes batch `20261007-a` measured. A member that never returns is `layer-change-member-recovery`'s
  shape, not this row's.
- **The census is a read of a live file.** Each client kept writing while the census ran, so the byte
  figures are this run's reading at 11:25:21 and drift upward; the per-subject line counts are the stable
  half.
- **One direction and one fixture.** The state was produced by the host's `skiplayer` change with both
  members as sandbox guests; the mirrored direction, an ordinary elevator descent and a member that stays
  out for minutes were not driven.
- **The clone is still built and destroyed per attempt** — the ticket's own non-goal: the bound is on the
  volume the state produces, and the retry cadence was left alone.

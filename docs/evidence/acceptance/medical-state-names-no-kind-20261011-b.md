# Acceptance record — a medical operation state that names no kind is read as an injection

- Ticket: `medical-state-names-no-kind` — verdict: **back to `todo/`** (`- Status: Todo — Rejected (row 4:
  a Stage-3 action's outcome never lands on the target's own body)`)
- Batch: `20261011-b` — tickets: `medical-state-names-no-kind` (rows 1-4) and
  `cross-player-item-use-random-determinism` (row 1's open half); scope and the pre-declared row criteria:
  `docs/evidence/acceptance/20261011-b-scope.md`
- Commit under test: `c6a35bb9` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+c6a35bb9…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0). The example mod was
  refreshed from this tree's build output before the clients started.
- Run: 2026-10-11 12:24 → 12:34 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient) · Third client: alternate sandbox (the third-party view)
- Dependencies: `preflight.txt` and `session-environment-status.txt` in the batch directory — 11 present, "a
  full two-client run is possible; the alternate third client is configured", `active=cuo`,
  `game-running=false`, `swap-needed=false` (both files were re-captured after the run; the preflight is
  read-only and the machine was left in the same state, and the run itself used the same readings).
- Artifacts: the batch directory under `acceptance-artifacts-dir`; every file cited below lives there

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | a driven grip slip on a guest leaves the guest's OWN limb 1 reading the values the authority committed, read back within a second of the slip | machine | **pass** (with the timing caveat below) | the authority committed `skin -5.783163 bleed +0.9278045 pain +11.24498` at 12:31:26.570/.571 (`host-slip-session-3.log.txt`). The patient's own limb 1 reads `skinHealth 94.217, bleedAmount 0.928` (`run2-guest-limb1-after-slip.json`): 100 − 5.783163 = 94.216837 and 0 + 0.9278045 = 0.9278045, exact at the precision the driver prints. Its own log carries the write 414 ms after the commit — `[MedicalOps] local target body updated (terminal=False).` at 12:31:26.985 and again at .986 (`guest-slip-session-3.log.txt`); the same line at 12:31:26.461 is 109 ms BEFORE the commit and is therefore the session's opening state, not this slip's write. Pain is not compared as a fixed number because the native limb drives it every frame (the ticket's own analysis); it read 6.003 on the patient and 5.706 on the other two clients, both falling toward the limb's own target, which is the decay the ticket names |
| 2 | one report interval later the AUTHORITY's copy still holds those values | machine | **pass** | `limb-read source=saved` on the host for the guest's limb 1: `94.217 / 0.928` at 12:31:33.460 and again `94.217 / 0.928` at 12:31:50.500 — two reads ~17 s apart, i.e. many 1 Hz reports, with no revert (`run2-host-saved-guest-limb1-after-slip.json`, `-2.json`). The guest body's copy is report-driven, so this is the reading that failed before the fix |
| 3 | a third client's view of the same limb advances from the session's state, without waiting for the terminal | machine | **pass** | the third client's displayed limb 1 for the patient (remote medical view open on that peer) reads `94.217 / 0.928` at 12:31:32.931 (`run2-alt-display-limb1-after-slip.json`, `limb-read source=display` on `hotrepl-alt-url`). The session's terminal for that operation is later: `session 3 timed out after 15031 ms idle` / `terminal TimedOut` at **12:31:41.596** (`host-slip-session-3.log.txt`) — so the third client held the authoritative values 8.7 s BEFORE any terminal existed |
| 4 | the family regression: a Stage-3 action (bandage or splint removal) still writes the target's own body through the same receiving path | machine | **fail** | the driven SplintRemoval reached the receiving path (`[MedicalOps3] started SplintRemoval operation 4`, `awarded removed splint (id 9223372037175756739) to <host> in slot 0`, `terminal Completed` — `host-removal-operations-4-and-5.log.txt`; and `[MedicalOps] local target body updated (terminal=True)` on the guest at 12:33:51.563 — `guest-slip-and-removal.log.txt`) and wrote the limb's flag — but the removal's own outcome did not land: the guest's limb 2 reads `splinted=false, components=SplintLimb` (`run2-guest-local-limb2-after-removal.json`) and the authority's stored copy reads the same (`run2-host-saved-guest-limb2-after-removal.json`), i.e. the component the host removed came back through the target's own report. A second drive was accepted again (`request 5 answered by <guest>: this body allows it.`) and awarded a SECOND splint (`id 9223372041470724035`, slot 1) |

**Row 1's timing caveat.** The scope declared the read-back "within a second of the slip". The slip's
authority commit is 12:31:26.571 and the patient's probe read is 12:31:32.324 — **5.75 s**, and the run does
not claim otherwise. The reading still decides the row because both compared values are frozen while the
limb carries fragments (`SkinHealRate` and the bleed line are both multiplied by zero under that factor,
the ticket's own analysis), the probe read is on the patient's own client, and the direct write evidence on
that same client is the 12:31:26.985 log line 414 ms after the commit. The window as written was not met;
the criterion's failure condition ("the patient's body still reads its pre-slip values") was not observed.

Rows 1 and 2 close `cross-player-item-use-random-determinism`'s row 1: the patient's body and the
authority's copy now agree with what the authority committed (that ticket's own record:
`docs/evidence/acceptance/cross-player-item-use-random-determinism-20261011-b.md`).

## What row 4's failure is, and what it is not

The row's stated failure mode is a sentinel regression in the Stage-3 family. **That regression did not
happen**: the operation routed, the target's own client took the write, and the family's log carries the
whole exchange. What failed is the removal's effect on the target's body — a pre-existing defect the row's
drive exposed, where the receiving path has no component-removal half and the target's next report restores
what the host removed.

**Attribution of "pre-existing"** (the run checked it rather than asserting it): `git log` puts
`OtherMedicalRemovalApplier` in `a1a6a970` ("add remote medical Stage 3 native minigames/actions"),
`LimbComponentStateCodec` in `4bcaf65a` ("add cross-player component-bearing limb tool use") — one commit,
so its `Apply` has been additive-only since it was written — and the fix under acceptance (`87b3643a`)
touches neither file, nor `CharacterDataSync.ApplyHealState`, nor `MedicalTargetBodyValidator`; its diff is
the `None` sentinel and the receiver's warning, the five message defaults, two injection builders,
`ShrapnelSessionStateWriter` and the Stage-3 start guard.

The defect is now its own ticket (`docs/backlog/todo/medical-removal-limb-component-not-applied.md`), and
this ticket returns to `done/` when that defect is fixed and row 4 re-runs.

## Findings this run opened

1. **A removed limb component is never taken off the target's own body** — row 4's failure, twice
   reproduced in the run, with a second awarded item as its consequence:
   `docs/backlog/todo/medical-removal-limb-component-not-applied.md`.
2. **A peer's limb component on a display clone runs the game's own `Start` and throws every frame** — the
   operator's client flooded with `NullReferenceException` (stack: `OnLimbWound.Update` →
   `SplintLimb.Start`'s icon predicate → `GetComponent`) as soon as a splinted peer existed while its
   medical panel was open. In the first client lifetime: 4473 lines before the panel was closed (last
   12:28:07.530), **0 while it was closed**, 1592 after it was reopened (first 12:28:19.544) —
   `host-splint-icon-nre-storm-archive.log.txt`, recovered from the rotated archive
   `2026-10-11-8.log.gz`; and 391 lines in a 25-second window during the real removal drive —
   `host-splint-removal-and-storm.log.txt`. The two sandbox clients logged **zero** in the same runs
   (`guest-alt-exception-census.txt`). Ticket:
   `docs/backlog/todo/display-clone-limb-component-runs-native-start.md`.

## What the run had to learn first (the drive's own gates)

The scenario needs six things in order; the first five were known from batch `20261011-a`, the sixth is new
and cost this run two cycles:

1. the seeded limb (`seed-shrapnel limb=1 count=3`, an explicit limb and count);
2. the native panel open before the focus, or the coordinator's pump cancels the session;
3. the two participants in sight of each other (this world spawned both bodies at one point, (0, 490.615) —
   `run2-host-body.json`, `run2-guest-body.json`);
4. `start` and `break` in separate calls (the ack that assigns the operation id is asynchronous);
5. opening the remote view in one call and starting the session in the NEXT one: a minigame created in the
   same call as the open holds a limb of a display body the renderer replaces milliseconds later, so
   `BreakGrasp` dereferences a dead limb and throws. The run's first two drives failed there
   (`run2-host-break-grasp-2.json`: `step-failed / step=break-grasp / System.NullReferenceException`), and
   what located it was the step name plus the host log's `Remote body created for <guest>` at 12:26:47.162,
   85 ms after `[MedicalView] started remote shrapnel minigame` at 12:26:47.077 — both in
   `host-splint-icon-nre-storm-archive.log.txt`. The recipe's added scene fields (`minigameLimbNull`,
   `limbBodyNull`, `minigameLimbIsDisplayLimb`, …) were written AFTER those failures and no failing drive
   has exercised them yet;
6. for the Stage-3 removal: the same two-call shape (`remote-wound-special mode=open` then `mode=special`),
   because the family dispatches on the DISPLAY limb's own state and the coordinator applies the peer's limb
   components to the display body only on its tick — a dispatch in the open's own call read
   `components=""` and refused (`run2-host-wound-special-limb2.json`), while the next call dispatched
   (`run2-host-wound-special-limb2-2.json`, `started: true`).

Three recipes were added for this batch (`limb-read`, `seed-splint`, `remote-wound-special`), and
`grip-slip-drive` gained step-by-step failure reporting.

## Residuals for the user

None: every row above is judged from this run's own readings, and the two findings are filed as tickets.

## Limits

- **The operator's own screen was read through the driver, not through a frame**: `grip-slip-drive` reports
  the deltas the native call left on the displayed limb (5.783 / 0.928 / 11.245, all inside the native
  ranges) and the authority logged `both sides derived the same values`; no frame was captured for this row.
- Row 4's failure is proven for the splint kind; the tourniquet kind is the same applier and codec but was
  not driven (its rows live on the new ticket).
- **The two sandbox clients cannot be compared to the host's exception storm from the same probe**: the
  census in `guest-alt-exception-census.txt` covers their current logs plus their own rotated archives, in
  which the window is present, and both read zero.
- **An unreconciled contradiction inside the artifacts**: the host's own member probe reports
  `inWorld: false` for both peers (`run2-host-probe-members.json`) while each client's `state` reports
  `inWorld: true` and the host's item keyframe line counts "2 in-world member(s)". The run did not resolve
  which of the two the field means; nothing in this batch's rows depends on it, and it is named here so the
  next reader does not take the probe's flag for the session's state.
- **One unrelated startup exception**: the host logged a single `TypeLoadException` at 12:24:43.821
  (`System.ComponentModel.DataAnnotations.RequiredAttribute` in `System.ComponentModel.Annotations,
  Version=5.0.0.0`) before any of this batch's work. It is not the storm (which is 6065
  `NullReferenceException` lines), it did not prevent the session, and this run did not investigate it.
- The session ran with 267 world items and ~10 s item keyframes at 250-350 KB/s; the run's own messages were
  not delayed by it, but a quieter world was not compared.
- The world was fresh each restart: the first attempt (a splint already seeded before the shrapnel drive) was
  abandoned for a clean world, which is recorded here rather than presented as one continuous run.

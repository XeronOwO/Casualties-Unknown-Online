# Acceptance record — the `/heal` reset restores the host-owned vitals

- Ticket: `heal-reset-does-not-restore-host-owned-vitals` — verdict: moved to `done/`
- Batch: `20261018-y` — tickets: `heal-reset-does-not-restore-host-owned-vitals` (the batch the
  session handoff opened for this ticket)
- Commit: `d47a9195` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+d47a9195e293c4f5cf34fad12a09af1ae6f71e38` (verified against this tree's build output)
- Run: 2026-10-18 20:53–21:00 · Host: physical machine · Guest: Sandboxie sandbox · Third client:
  the alternate Sandboxie sandbox (row 2)
- Dependencies: game, steam, sandboxie, sandbox-alt, dotnet, capture, logs, artifacts, hotrepl,
  input — the preflight reported every required dependency present
- Artifacts: the directory named by `acceptance-artifacts-dir`, batch `20261018-y`; probe dumps and
  log windows are cited by id

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A member whose host-model blood is negative is reset with `/heal`; both the host's saved snapshot and the target's local body read 100 (and the reset's limb/consciousness values) after the row cadence, and the 10 Hz row does not revert them | machine | **pass** | the host model was forced to blood -40 on the guest (`10-host-force-guest-40.json`); before the reset the host saved read -39.732 and the guest local -39.711, both unconscious (`11`, `12`); the host's `/heal @a` answered with the guest among the instructed targets (`14`); after the row cadence the host saved and the guest local both read `bloodVolume=100, alive=true, conscious=true, brainHealth=100, consciousness=100` (`15`, `16`); limb 0 on both sides read skin 100 / muscle 100 / bleed 0 / no components (`17`, `18`); the 10 Hz row kept the cached HP at 100 for the following 10+ seconds (`70-host-row1-lines.txt`); the host log carries `stored <guest>'s reset result through the authority path (instruction 2, blood 100.0, total bleed 0.0, 15 limbs)` and `re-seeded from an authoritative write (blood 100.0)`, and the traffic row shows `Receive HealResetApplied=982B` (wire evidence) (`70`) |
| 2 | The same with a permitted member running `/heal` on another member; the host's permission checks are unchanged | machine | **pass** | the host enabled the switch (`/hostrules {"AllowMemberCheatVerbs":true}` → `Updated 1 host rule(s).`, `50`) and granted the guest (`51` → `Granted the cheat verbs to …; the roster is announced`); the guest's announced rules read `announced=true, allowMemberCheatVerbs=true, forbidsMemberCheatVerbs=false` (`52`); the alternate member's host model was forced to -40 (`53`; `54` host saved -39.878, `55` alt local -39.858); the guest's `/heal @a` reached the host (`56`), whose answer the member's console shows: `Reset <host>: full heal applied. Reset <third-client>: sent to that player's own client…` (`57`); the host log carries `<guest> reset the host's own body`, `instructed <third-client> … (instruction 8)`, `stored <third-client>'s reset result … blood 100.0` and `applied the reset for instruction 8` (`73`), and the member's own log carries the request it sent (`74`); the alt local and the host saved both read 100 after (`58`, `59`) |
| 3 | A plain report sent right after the reset still cannot state the three owned values; the reconciliation may warn once about the sampling skew, and the host's values stand | machine | **pass** | the guest set its local blood to 42 and forced a report (`20`); a plain report carrying `blood 42 / totalBleed 2 / limb 0 bleed 0.5` was sent through the production `ICharacterDataControl.ReportCharacterData` seam (`23`); the host log named all three discrepancies in one warning: `blood volume model 100.00 reported 42.00 (-58); total bleeding speed model 0.00 reported 2.00 (+2); limb 0 bleed model 0.00 reported 0.50 (+0.5)` (`71`); the host saved stayed 100 (`24`) and the 10 Hz row restored the guest local to 100 (`25`) |
| 4 | A non-finite reset event is refused whole by name, and the previous snapshot stays | machine | **pass** | the host model was forced back to -40 (`30`; `31` -39.882, `32` -39.863); the guest's `eyePanicTime` was set to NaN and read back as `NaN` (`33`, `34`); the host's `/heal @a` (`35`) made the guest apply the reset, whose captured result carried the non-finite field: the host refused it by name — `refusing a non-finite heal reset event for <guest>: health.EyePanicTime — the previous snapshot stays` and `refused … instruction 4; the previous snapshot stands and the instruction stays outstanding until it expires` — while the alt's finite result in the same command was stored (`72`); the host saved stayed negative (-39.469, `36`) and the 10 Hz row reverted the guest local (-39.45, `37`) — nothing was written; the guest's plain report was refused whole for the same field (`72`) and, after the probe cleared it, the host logged `report is finite again` (`38`, `72`); a following `/heal @a` then restored both sides to 100 (`40`, `41`) |

**Frame cost (post-deploy `[Vitals] cost` read).** A steady window with TWO remote clones, `timeScale=1`,
`fixedStep=20.00ms` (`70-host-row1-lines.txt`):

`bodies=2 steps=1000 passes=1000 stepAvg≈81-89us passAvg≈73-81us frameCost=0.161-0.179ms frameBudget=0.80-0.89%`

The probe is still wired after the change: the cost line appears every ten seconds, and the
re-seed line is the authoritative write being adopted by the model.

## Limits

- The three host-owned values were read as blood volume and per-limb bleed through `health-read` /
  `limb-read`; the total bleeding speed is evidenced by the authority log line
  (`total bleed 0.0`) and the divergence warning, not by the read recipe.
- Row 4's non-finite field was injected through a probe (`eyePanicTime`) because a genuine NaN is
  not otherwise reachable in a live body; the handler, the finite guard, the reset-table anchor and
  the merge are production code, reached through the real instruction and the real wire frame.
- Row 3's report was built by hand through the production report seam so the divergence could be
  observed deterministically; its empty item list produced unrelated `[CharSync]` item-divergence
  lines, noted as a probe artifact.
- Row 2 used the alternate sandbox third client; the host's two-sided permission path (announced
  switch + roster, then its own judgement of the arriving request) was exercised end to end.
- The retry of a transport-refused send was not exercised on the wire (the run's transport accepted
  every frame; no `[HealReset] … did not accept` warning appears). Its cadence, budget and
  body/session drop are pinned by `HealResetReportRetryTests`.
- One session, no artificial delay/loss link. The run proves the four rows once, not the absence of
  a rare race.

## Residuals for the user

None: every row was judged from probe dumps and log windows this run produced against the deployed
artifact.

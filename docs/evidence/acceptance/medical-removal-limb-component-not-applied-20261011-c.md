# Acceptance record — a removed limb component is never taken off the target's own body

- Ticket: `medical-removal-limb-component-not-applied` — verdict: **moved to `done/`** (rows 1-6 pass)
- Batch: `20261011-c` — tickets `medical-removal-limb-component-not-applied`,
  `display-clone-limb-component-runs-native-start`, `medical-state-names-no-kind` (row 4); scope and the
  pre-declared row criteria: `docs/evidence/acceptance/20261011-c-scope.md`
- Commit under test: `7c5646bd` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+7c5646bd…`, verified against `<game-dir>` (`tools/verify-deploy.ps1`, exit 0)
- Run: 2026-10-11 13:24 → 13:31 · Host: physical machine (the operator) · Guest: first sandbox (the
  patient) · Third client: alternate sandbox (the third-party view)
- Dependencies: `preflight.txt` and `session-environment-status.txt` in the batch directory — 11 present,
  "a full two-client run is possible; the alternate third client is configured", `active=cuo`,
  `game-running=false`, `swap-needed=false`
- Artifacts: the batch directory under `acceptance-artifacts-dir`; every file cited below lives there

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | a driven splint removal leaves the target's OWN limb without the component, read on the target's client | machine | **pass** | the authority committed `started SplintRemoval operation 1` / `operation 1 (SplintRemoval) ended with progress 0.000` / `terminal Completed` at 13:27:56.150-.158 (`c-host-medical-log.txt`, the host's window from the run mark). The patient's own limb 2 then reads `splinted=false, components=""` (`c-guest-local-limb2-after-removal.json`) — the component is GONE, where batch `20261011-b` read `SplintLimb` beside the same flag |
| 2 | one report interval later the AUTHORITY's copy still shows the limb without it | machine | **pass** | `limb-read source=saved` for the guest's limb 2 on the host: `splinted=false, components=""` (`c-host-saved-limb2-after-removal.json`), read after the target's own reports had gone through (`[MedicalOps] local operator awarded splint …` at 13:27:56.164 and later 1 Hz reports); the before-reading was `splinted=true, components=SplintLimb` (`c-host-saved-limb2-before.json`), so this is the same limb changing, not an empty read |
| 3 | the operator receives the removed item exactly once, with the component's own condition | machine | **pass** | exactly one award for the removal — `awarded removed splint (id 9223372037175756739) to <host> in slot 0` (13:27:56.157) and the operator's own `local operator awarded splint (id …) in slot 0` (13:27:56.164); no second line for that operation, and the second drive (row 4) produced none at all |
| 4 | a second removal on the same limb is refused by the target's own body check | machine | **pass** (with the shape noted in *Limits*) | the second dispatch answered `started: false` on the same limb and display copy (`c-host-dispatch-removal-2.json`), and the host's log window since that mark is EMPTY — no `MedicalTargetCheck`, no operation, no award |
| 5 | a driven tourniquet removal behaves the same on both surfaces | machine | **pass** | `seed-tourniquet` on the guest's limb 3 (the game's own `Item.cs` write shape) → the family dispatched `TourniquetRemoval` (not the splint kind: `started remote TourniquetRemoval removal`, 13:28:48.481, `c-host-medical-log.txt`) → the patient's own limb reads `components=""` (`c-guest-local-limb3-after-tourniquet-removal.json`) and the operator's display copy reads the same (`c-host-display-limb3-after.json`), with exactly one `awarded removed tourniquet (id 9223372041470724035) to <host> in slot 1` in the same window. The authority's stored copy was read as `components=""` too, but that one call's artifact was not written (see *Limits*) |
| 6 | the display surfaces lose the removed component without running native behaviour on a clone | machine | **pass** | the display copy lost both: `limb 2 splinted=false, components=""` and `limb 3 components=""` (`c-host-display-limb2-after.json`, `c-host-display-limb3-after.json`), read on the operator's client while the panel stayed open; the same client logged **zero** `[Unity:Exception]` and zero `OnLimbWound` lines across the whole session window |

**The receiving half in one client** (the probe, run twice — before and after the fix, same call shape):
`tools/acceptance/recipes/limb-component-apply.cs` captures the caller's own body, edits ONE limb row to
the authority's removal shape (`Components` empty, `Splinted=false`) and applies it through
`CharacterDataSync.ApplyHealState`. On the unfixed build (batch artifact directory of `20261011-c`,
`red-message-clear-limb3.json`, taken before the fix was built) it read

```
componentsBefore="SplintLimb"  componentsAfter="SplintLimb"   splintedBefore=true  splintedAfter=false  messageComponents=0
```

and on this build, same recipe, same shape (`c-green-message-clear-limb2.json`):

```
componentsBefore="SplintLimb"  componentsAfter=""            splintedBefore=true  splintedAfter=false  messageComponents=0
```

## Limits

- **One reading was not filed.** The authority's stored copy after the TOURNIQUET removal (limb 3) was read
  in the run and returned `components=""`, but the driver did not write that call's `-JsonPath` file (the
  same call shape wrote fifteen others in this batch; the cause is not established, and the client was
  already gone when this was noticed, so it could not be re-read). Row 5's verdict therefore rests on the
  two readings that ARE filed — the target's own body and the operator's display copy — plus the single
  award line; the splint kind's authority copy IS filed (row 2) and shows the same outcome.
- **Row 4's refusal happened at the dispatch, not at the target's body check.** The row's own failure
  condition ("it is accepted and awards another item") was not observed: with the removal now landing, the
  display limb carries no splint, so the family refuses to dispatch a second removal before the target is
  ever asked. The target's check (`MedicalTargetBodyValidator`: "Target limb has no splint.") is the same
  rule that accepted the second removal in batch `20261011-b`, and the component it reads is now gone from
  that body (row 1) — but this run did not drive a message past the dispatch to exercise it directly.
- The run drove the splint and tourniquet kinds; the chilled kind was seeded and displayed (the display
  ticket's row 4) but no removal was driven for it (it has no removal path in the game: an icepack's
  `ChilledLimb` expires on its own).
- Pain is read on both limbs but not compared as a number: the native limb drives its own pain every frame.

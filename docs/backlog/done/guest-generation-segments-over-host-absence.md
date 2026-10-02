# A member's layer generation that spans the host's absence counts host-wait yields as segments

- Status: Done (batch `20261002-p` judged rows 1–5 pass on the deployed fix, 2026-10-02)
- Acceptance record: `docs/evidence/acceptance/guest-generation-segments-over-host-absence-20261002-p.md`
- Priority: High
- Category: Network / sync (world generation, layer-modifier identity)
- Source: agent attribution during batch `20261002-m`'s acceptance run of `reenter-baseline-adoption`
  (2026-10-02); not a user report
- Related: `done/layer-mod-baseline-divergence-on-continue.md` (the same warning line, a different
  cause), `done/world-determinism-world-fingerprint.md` (the comparison that would catch the effect),
  `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/WorldGenRandomIsolation.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/GenerationYield.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/WorldGen/LayerModifierSync.cs`

## Problem (evidence)

Batch `20261002-m` left the host out of the world while both members were still generating their layer
(host `leave-world` at 19:03:05; the members' generation finished 19:03:11.478). The members' generation
wrappers kept draining the game's host-wait yields as generation segments after the world was complete:

| Client | Generation | Segments | Last recorded segment start |
|---|---|---|---|
| host | layer 1, 19:02:43.700 → 19:02:56.990 | 19 | `692FA3173CE7973949303E6E7C7FD415` |
| guest | layer 1, 19:02:58.341 → 19:03:11.478 | 51 | `D6848B6D0B46332A4A24DBC1E3CB786B` |
| alt | layer 1, 19:02:58.341 → 19:03:11.410 | 43 | (same shape) |

The real segments matched the host's byte for byte through `692FA317…` — the world itself was the host's —
but the empty tail's last segment start replaced the recorded one, so the layer-modifier replay decided
from a different state: the members' `[LayerMod] guest replay index=-1 … entryState=D6848B6D…` against
the host's `[LayerMod] enter state=692FA317… chance=40 depth=1`, `picked=none` against the host's
`picked=3 prefix=寒冷`, and both members warned `[LayerMod] baseline divergence — local segment start
D6848B6D… vs host's 692FA317… (world effects may diverge)` from 19:03:12 (repeating on the 10 s snapshot
cadence). The host's snapshot then applied the modifier authoritatively (`[LayerMod] applied host
modifier 3`), but the layer's world effects had already been generated without it on the members.

A second pass with the host present for the whole member generation reproduced `19/19/19` segments,
`entryState=E5D3D723B117268DC2F862A15C15068F` on all three clients and zero divergence — the tail is
what moves the decision.

## What landed (2026-10-02)

**Root cause.** `WorldGenRandomIsolation.Drive` treated every `IEnumerator` as a nested generation
coroutine and drove it recursively. UnityEngine's `CustomYieldInstruction` (WaitUntil/WaitWhile)
implements `IEnumerator`, so `FinishWorldGeneration`'s
`yield return new WaitUntil(() => !GlobalDark.main.IsDarkening())` (WorldGeneration.cs:3625) was driven
frame by frame: the recursion's loop body ran `Save()` once per waited frame, each waited frame was
counted as a "generation segment", and its first save captured the state after the pre-wait work
(`DistributeMiniBarrels`' single `Random.Range` draw). That moved `LastSegmentStart` off the generation's
own last segment; `LayerModifierApplyPatch` rewinds the modifier decision to that recorded start, so the
member rolled from the wrong stream position while the host's own wait resolved immediately (its
`Darken()` is skipped inside the gate window), which is why the host never recorded a post-generation
segment.

**Fix.** `GenerationYield` (Runtime, pure) classifies one yield from the two type facts the wrapper can
observe: a wait instruction is `Wait` (checked first), a nested enumerator is `Nested`, anything else is
`Plain`. `Drive` hands a `Wait` to the engine whole — the engine polls it and resumes once — and neither
counts it nor moves the recorded start; the generation stream is still sealed across it. `Nested` and
`Plain` behave exactly as before. The rule lives in Runtime so its decision is unit-pinned; the wiring
stays adapter shell.

**Batch `20261002-p` red (pre-fix, one generation, host and member).** Host `[GenStream] done — 19
segments`, decision entry `4AFD152F…`; the member whose generation spanned the host's absence
`done — 41 segments`, replay entry `D70305E2…`. The member's first 19 segment lines are byte-identical to
the host's and the extra 22 are waited frames. Artifacts: `20261002-p/p-host-segments-2.txt`,
`p-guest-segments-2.txt`, `p-guest-tail-context.txt` in the directory named by `acceptance-artifacts-dir`.

## Acceptance matrix

| # | Scenario | Expected |
|---|---|---|
| 1 | A member's layer generation spans the host's absence | `[GenStream] done — N` equals the host's own count for the same generation; no `[LayerMod] baseline divergence` |
| 2 | The same run, the decision itself | The member's `[LayerMod] guest replay … entryState=` equals the host's `[LayerMod] enter state=` without the host's snapshot correcting it |
| 3 | Host present for the whole member generation (regression) | 19/19/19 segments, the three entry states agree, zero divergence (the batch `20261002-m` second-pass shape) |
| 4 | Third-party view | The alternate client's `[GenStream]`/`[LayerMod]` agree with the host's |
| 5 | The host's own path | Unchanged: its segment count and decision entry are what they were before the fix |

## Verification

- **Red, observed first.** `GenerationYieldTests.Classify_ACustomYieldInstruction_IsAWait_NotANestedCoroutine`
  failed on the behaviour-preserving extraction with `Expected: Wait / Actual: Nested`; the other cases
  passed in the same run, so it is a red rather than a compile error. Log: `20261002-p/p-red-generation-yield.log`.
- **Green.** The focused class 4/4 (`p-green-generation-yield.log`); normative gates 315/315; whole
  solution 4613 + 315, 0 failures, built; `dotnet build` 0 warnings / 0 errors; `dotnet format` exit 0.
- **Independent adversarial review** (fresh context, frozen tree): no blocker; the mechanism was not
  falsified. Its one open item is this ticket's own acceptance run (rows 1–5); the report is
  `20261002-p/p-review-report.md` in the local artifact area.

## Known coverage gaps (declared, not silently carried)

- The `Drive` wiring is not exercisable in the test host (it drives a live Unity coroutine): the unit
  test pins the classification rule, and rows 1–5 are the live-world evidence.
- A `Coroutine` handle (`YieldInstruction`, not `IEnumerator`) still takes the plain branch and is
  counted once; the generation chain never yields one, and the rule now states it.
- The red run did not pin WHY the member's `darkening` flag was set while the host's `Darken()` was
  skipped; the fix does not depend on it — both sides consumed the same single pre-wait draw, and only
  the member's wait frames captured the post-draw state.
- State comparisons are the 16-byte windows the runtime logs, which is exactly what the divergence
  warning and this ticket compare.

## Limits

- Observed once, on one staging (the host leaves while the members load). Whether a member's generation
  can span a host *reconnect* the same way is not staged.
- The census/health difference the same session showed is the enemy-binding family
  (`todo/enemy-snapshot-binding-recovery.md`); this ticket does not claim it as its own effect.

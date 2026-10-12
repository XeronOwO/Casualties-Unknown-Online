# A game-side throw inside a scope-entering patched frame latches CallContext off

- Status: Todo — split out of `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` on 2026-10-12,
  when that ticket's content defect (stage 1) landed in `22ae381c` and its own *Required work* left this half
  as a decision. It is its own cycle because it touches call-identity architecture rather than content binding.
- Priority: High — a single game-side exception can end a client session. In batch `20261011-k` the client was
  unusable for 92 seconds and had to be restarted, and what kept breaking was not the game's own path but
  CUO's: every scope-entering patch in that process threw on its own account.
- Category: Runtime robustness / call identity
- Related: `docs/backlog/review/mod-item-with-no-declared-tags-throws.md` (stage 1, the content defect whose
  throw was the trigger), `src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs` (the depth guard and the
  scope stack), `src/CasualtiesUnknownOnline.GameAdapter/Patches/BurpSoundPatches.cs` (the fitting entry shape:
  a `Prefix` that enters and a `Postfix` that disposes), `docs/decisions/active.md` (the call-identity model
  this guard exists for)
- Source: batch `20261011-k`'s storm census — `f3-host-session1-storm-census.txt`, a re-derivation of the
  rotated host log, holds 136,445 lines / 15,706,048 bytes with **19,973** `InvalidOperationException:
  CallContext.Enter depth 16 exceeded — an Enter was never disposed (leaked scope)` lines, the first at
  22:27:20.779 and the last at 22:28:52.907 (92.128 s), alongside 2,174 `ItemInfo.HasTag` frames.

## What is measured, and what is only read

**Measured**: the latch, the storm's size and shape, and its trigger. `CallContext.Enter` throws once
`_depth >= MaxDepth` (16) and nothing recovers the depth, so the first sixteen undisposed enters turn every
later `Enter` in that process into a throw. The census's own sample stack is a scope-entering prefix throwing
on its own account (`PantSoundPatches.PantSoundUpdatePatch.Prefix` → `PantSound.Update`) — an unrelated path
paying for the exhausted depth.

**Only read**: which frame ENTERED a scope and never disposed it. No kept artifact names it. The mechanism that
fits is a patched frame whose scope is opened in a `Prefix` and disposed in the `Postfix` — `BurpSoundPatches`'
`Body.HandleVisuals` pair is exactly that shape (`Prefix` → `CallContext.Enter`, `Postfix(IDisposable? __state)
=> __state?.Dispose()`, no finalizer) — and the trigger state is a game-side throwing original, which skips
every postfix. That is a reading of the source plus the shape of the storm, NOT an observation, and an earlier
draft of the parent ticket named two frames the artifacts never support; its own review caught that.

## Required work

1. **First: produce the missing stack.** A probe that throws inside each candidate frame, one at a time, on a
   live client, recording which frame leaves a scope open. The ticket's fix direction is not chosen before that
   artifact exists — the candidates are every patched frame that enters a scope in a `Prefix` and disposes in a
   `Postfix`, plus the coroutine path (`ScopedCoroutine`) where the disposal rides a step rather than a method
   boundary.
2. **Decide the fix direction with the artifact in hand**, and case it. The two named directions are: (a)
   disposal that survives the original's exception (the scope's owner releases on the throwing path), or (b) a
   guard that recovers the depth instead of latching — refusing the new enter, reporting the leak, and restoring
   a usable stack. They are not equivalent: (b) restores the client but can mis-classify mutations until the
   stack settles, and (a) is per-site. Whatever is chosen needs its own case for "the original throws with a
   scope open" plus a case that the recovery does not fire on a legitimate deep stack (real nesting is 2-3
   levels; the bound is 16).
3. **Sweep the family**: every `CallContext.Enter` call site that is not inside a `using` (the patches that pass
   the disposable through `__state`, and `ScopedCoroutine`), and say for each how it releases when the frame it
   wraps throws. A site whose release depends on a postfix runs is a site with this defect.
4. **Report honestly**: the depth guard's message is what an operator sees; whatever recovery lands has to name
   the leak it recovered from, at a level an operator reads, without flooding the log (the trigger is per-frame
   while the state persists).

## Acceptance rows

| # | Row | Class |
|---|---|---|
| 1 | A probe makes a game-side frame throw while its CUO scope is open, and the process keeps working: later `CallContext.Enter` calls still succeed and no depth-exceeded line appears | `machine` |
| 2 | The same probe names the frame that leaked the scope, so the fix is aimed at a measured site rather than a candidate | `machine` |
| 3 | A legitimate deep stack (2-3 nested scopes) still classifies correctly — the recovery does not fire, and `Current` / `IsWithin` answer what they answered before | `machine` |
| 4 | After a recovered leak the client stays usable on both peers for the rest of the session, with the recovery reported once rather than per frame | `machine` |

## Non-goals

- Not the content defect that triggered this storm (stage 1, landed).
- Not a change to what a scope MEANS: the origins, `Current`, `IsWithin` and `IsReplayedRemoteFact` keep their
  present semantics.
- Not a general try/catch around patches: swallowing a game-side exception would hide the game's own failures,
  which the log exists to show.

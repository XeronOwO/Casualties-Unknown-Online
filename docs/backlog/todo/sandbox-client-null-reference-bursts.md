# Sandboxed clients log NullReferenceException bursts and an instantiate-null ArgumentException

- Status: Todo
- Priority: Low
- Category: Runtime diagnostics / sandbox
- Source: observed during agent acceptance runs — noted unjudged in `docs/acceptance/lessons.md` (2026-10-01), re-captured with context in batch `20261002-c` (`docs/evidence/acceptance/20261002-c-scope.md`)
- Related: `docs/acceptance/lessons.md`, `docs/evidence/acceptance/20261002-c-scope.md`

## Problem

Both sandboxed clients (the Steam1 guest and the Steam2 alternate) log bursts of
`[ERR] [Unity:Exception] NullReferenceException` while the host's log carries none, and the alternate
also logs `System.ArgumentException: The Object you want to instantiate is null.` One captured stack
points at the GAME's own code (`GroundBlood.Start ()`), so the throw is not CUO's — what is unknown is
the state that leads into it and whether CUO's resets or the sandbox's environment are part of it.

## Evidence (batch `20261002-c`, artifact ids in the directory named by `acceptance-artifacts-dir`)

- Alternate (Steam2), during its re-entry into the world: a burst of `NullReferenceException` lines,
  one carrying the stack `NullReferenceException: Object reference not set to an instance of an
  object` / `GroundBlood.Start () (at <ab887c07f51841c98ea015c9c29ae1af>:0)`, interleaved with the
  world's generation stream (`[GenStream] restore …`), plus ten `System.ArgumentException: The Object
  you want to instantiate is null.` lines — artifact `c-sandbox-instantiate-alt.log` (read from the
  full-log tail).
- Guest (Steam1), later while out of the world: ~50 `NullReferenceException` lines inside ~5 ms,
  interleaved with `[WRN] [RemotePlayerRenderer] Remote body: no Body component in "Experiment"
  clone.` — artifacts `c-sandbox-instantiate-guest.log`, `c-guest-exception-window.log`.
- The host's log carries no matching line in the same windows.
- No user-visible failure was observed in the batch; every acceptance row was judged on its own
  evidence.

## Limits

- The guest's full `LogOutput.log` is very large and the searched tail did not reach its burst, so only
  the alternate has a stack; the two bursts may or may not be one family.
- No reproduction recipe is known yet: the bursts were observed, not staged. The alternate's re-entry
  is one observed window, not a confirmed trigger.

## Next step

- Stage one burst (a world re-entry is the cheapest observed window) and read the full `LogOutput.log`
  from its tail immediately, so the throwing object and its state are named; then decide whether the
  family needs a CUO fix, a guard, or only a log-level note.

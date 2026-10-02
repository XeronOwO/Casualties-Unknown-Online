# Sandboxed clients log NullReferenceException bursts and an instantiate-null ArgumentException

- Status: Todo (batch `20261002-e`: the burst did not reproduce on any client — zero NREs and zero
  `Item.DMD<Item::Update>` frames in a whole three-client session; the guide below now names the
  instrument that has to be read first)
- Priority: Low
- Category: Runtime diagnostics / sandbox
- Source: observed during agent acceptance runs — noted unjudged in `docs/acceptance/lessons.md` (2026-10-01), re-captured with context in batch `20261002-c` and again with a rolling-log stack frame in batch `20261002-d` (`docs/evidence/acceptance/20261002-c-scope.md`, `docs/evidence/acceptance/20261002-d-scope.md`)
- Related: `docs/acceptance/lessons.md`, `docs/evidence/acceptance/20261002-c-scope.md`, `docs/evidence/acceptance/20261002-d-scope.md`

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
- Guest (Steam1), during its world RE-ENTRY in batch `20261002-d`: a burst of `NullReferenceException`
  that the CUO ROLLING log carries with a stack FRAME —
  `(wrapper dynamic-method) Item.DMD<Item::Update>(Item)` — plus
  `System.ArgumentException: The Object you want to instantiate is null.` lines; the host logged
  neither. Artifacts `d-guest-nre-burst.log`, `d-guest-exception-window.log` (read at the re-entry
  mark). This is the first guest-side anchor: it names `Item.Update` as the throwing frame, and a world
  re-entry as the window that produced it.
- The host's log carries no matching line in the same windows.
- Batch `20261002-e` (2026-10-02, `docs/evidence/acceptance/20261002-e-scope.md`): a whole three-client
  session logged ZERO `NullReferenceException` lines and zero `Item.DMD<Item::Update>` frames on every
  client, while each sandboxed client logged 176 `System.ArgumentException: The Object you want to
  instantiate is null.` lines at world entry. Those 176 are NOT this family: their stacks name CUO's own
  contained materialization failure (`(wrapper dynamic-method) Utils.DMD<Utils::Create>` under
  `RuntimeEntityFactory.TryCreate`, `[Enemy] cannot create trader …`), and they now have their own
  ticket, `review/enemy-runtime-spawn-classification.md`. The two shapes must be read apart.
- No user-visible failure was observed in the batch; every acceptance row was judged on its own
  evidence.

## Limits

- The guest's full `LogOutput.log` is very large and the searched tail did not reach its first burst, so
  that capture has no stack; batch `20261002-d` changed the picture for the re-entry shape — the rolling
  log carries one stack frame (`Item.DMD<Item::Update>`) — but only the alternate's full-log capture has
  a complete native frame set. The two bursts may or may not be one family.
- No reproduction recipe is known yet: the bursts were observed, not staged. The alternate's re-entry in
  batch `20261002-c` and the guest's re-entry in batch `20261002-d` are two observed windows, not a
  confirmed trigger.

## Next step

- Read the DEDUPED diagnostic first: `ItemUpdateDiagnosticPatch`
  (`src/CasualtiesUnknownOnline.GameAdapter/Patches/ItemUpdateDiagnosticPatch.cs`, a patch on
  `Item.Update`) reports the reason (`rb` null / no `WorldGeneration.world`) and dedupes per object, so a
  repeat burst stays silent — the batch-`20261002-c`/`-d` windows carry no `[BrokenItemUpdate]` line for
  that reason. A staged burst is read for `[BrokenItemUpdate] … (reason) …` FIRST, on a fresh client,
  before the shape is re-derived from the bare stack.
- The source names every nullable dereference the bare frame can be: `Item.Update`
  (`reversing/Assembly-CSharp/Assembly-CSharp/Item.cs`, lines 145-180) reads `WorldGeneration.world` on
  its first line, then `this.rb` and `this.affect`; its `ArgumentException` shape can only come from
  `Resources.Load("ItemBreakParticle")` in the break branch. A burst whose stack names `Utils.Create` /
  `RuntimeEntityFactory` instead belongs to `review/enemy-runtime-spawn-classification.md`.
- Then stage one burst (a world re-entry is the cheapest observed window) and decide, with the object and
  its state named, whether the family needs a CUO fix, a guard, or only a log-level note. Batch
  `20261002-e` is the control that shows not every entry produces one.

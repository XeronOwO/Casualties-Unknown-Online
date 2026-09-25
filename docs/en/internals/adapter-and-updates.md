# The adapter and a game update

[Documentation](../README.md) > [Internals](README.md) > The adapter and a game update

---

**After this page** you can say what a game update is allowed to break, what the adapter promises the
layers above it, and why a hook that failed to install never runs silently. Read
[The shape of CUO](architecture-overview.md) first — this page is the game-facing half of that split.

## One place to break

The game is a moving target: a patch renames a type, moves a method, changes a field. CUO is built so
that this churn lands in exactly one project.
`src/CasualtiesUnknownOnline.Runtime/GameAdapter/IGameAdapter.cs` states the boundary: it is "the only
layer that knows the game's private types. One implementation per game build; the Runtime defines the
contracts, the adapter project (`CUO.GameAdapter`) implements them."

Everything above the adapter — the protocol, the session, the kernel, the mod API — compiles without a
reference to a single game assembly. That is not a convention: `GameAssemblyReferenceGateTests` reads
the solution and fails the build if a project outside the adapter references one.

## The boundary is a set of capability ports

The adapter interface declares no members of its own. It is the composition of twelve capability ports
(plus `IDisposable`, which is lifetime rather than capability), and a consumer resolves the one whose
capability it uses:

```text
IGameAdapter : IGameIntegrationLifecycle, IAdapterCapabilityQuery, IWorldPresenceQuery,
               IStartGateState, ILocalHealItemQuery, ITraderRecruitRequest, INativeInputBlocker,
               IRemoteInventoryPresentation, IRemoteMedicalPresentation, IPlayerAnchorQuery,
               IJoinFlowPresentation, ICarryPresentationPump, IDisposable
```

The type comment says why it is shaped that way and why it is frozen: "A version adapter therefore
implements per capability, and a test double implements only the capability it stands in for… adding a
member here fails `AdapterCapabilityPortShapeTests`. A new capability is a new port plus its entry in
this list, never a widening of the aggregate."

The same rule reaches the patches. They read the runtime through a bridge, and one domain's seam can be
split off it rather than widening the shared one — `IFluidPatchPort` is the worked example: the
aggregate in `src/CasualtiesUnknownOnline.GameAdapter/IPatchBridge.cs` neither declares nor composes it,
so a call written against the aggregate cannot reach those members at all. A member added to the
aggregate is a build failure, not a review note.

## The patch set installs all-or-nothing

At startup the adapter applies its Harmony patches, installs the dynamic ones, and then **verifies that
every declared target actually landed**. Quoted from
`src/CasualtiesUnknownOnline.GameAdapter/Patches/PatchInstallLifecycle.cs`:

```text
Never let a failed patch silently run: verify every patch class
actually landed on its target (a game update that breaks a target
must fail loud — a silently missing hook is how sync bugs hide).
```

A blocking failure refuses the install as a whole and unpatches before returning, "so a half-applied set
never runs". The attempt's result is published as a capability report — which game types probed, how
many patch targets landed, which rows are missing — and the plugin logs it at startup. A game update
that moved a method therefore produces a loud, specific refusal instead of a session that mysteriously
stops syncing things.

## Absorbing a game update

The work is bounded by the boundary, and it goes in this order:

1. **Find out what changed.** The probe reads declared game types; a missing type or a patch target that
   no longer exists is reported by name, not discovered later as a sync bug.
2. **Fix the adapter only.** A renamed member, a moved method, a changed field is absorbed inside
   `CasualtiesUnknownOnline.GameAdapter`. The wire, the kernel and the mod API do not move.
3. **Prefer a feature-scan to a hardcoded offset.** The standing rule is to look the game's API up at
   runtime rather than hardcoding offsets or private fields, because a hardcoded offset breaks on every
   update — the decompiled tree is a research aid, not a source of stable addresses.
4. **Keep the failure loud.** If a capability genuinely cannot work on this build, the report says so;
   nothing degrades silently to a half-working session.

## What is promised, and what is not

- **`Abstractions` is the promise.** It is the only surface with a stability contract, and even there
  the public surface is a recorded baseline: adding or removing a member is a reviewed act
  (`ApiSurfaceGateTests` compares the surface against `docs/contracts/abstractions-api-baseline.txt`).
- **`Runtime` and `GameAdapter` are implementations.** A mod may patch them, and a patch that stops
  working after a CUO update is that mod's problem — not a broken promise.
- **The game's own code is not a promise at all.** What the adapter binds belongs to the game; a game
  update may break it with no CUO decision involved, which is exactly why the boundary exists.

## What keeps the boundary

`GameAssemblyReferenceGateTests` (only the adapter touches game assemblies),
`ProjectDirectionGateTests` (the layer table), `AdapterCapabilityPortShapeTests` (in the behaviour
suite, `tests/CasualtiesUnknownOnline.Tests`) and `PatchBridgePortShapeGateTests` (in the gate
project) — the seams do not widen — plus `SourceShapeGateTests` (the kernel stays free of game and
Unity references). The rule-to-gate map is `docs/evidence/normative-gates.md`.

## Related reading

- [The game behind the adapter](game-internals.md) — what the adapter actually binds to
- [The shape of CUO](architecture-overview.md) — the stable layers above the adapter
- [The life of a mod](mod-loading-lifecycle.md) — what a mod may declare and patch
- [Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md) — which project a file belongs to
- [Glossary](../reference/glossary.md) — adapter, runtime, native, patch, kernel

---

[Documentation](../README.md) > [Internals](README.md) > The adapter and a game update

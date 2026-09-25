# The modification policy

[Documentation](../README.md) > [Reference](README.md) > The modification policy

---

**After this page** you can tell which CUO surface is a promise and which is only an implementation,
what a mod may patch and what that buys it, and how a need turns into part of the API. The contract
itself is [The mod API contract](mod-api.md); where a NEW system belongs is
[Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md); the levels named here
are recorded, per entry, in [`../../contracts/abstractions-api-baseline.txt`](../../contracts/abstractions-api-baseline.txt).

## What is a contract

| Surface | Status |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` public API | **The contract.** It is the only assembly a mod may reference. |
| `CasualtiesUnknownOnline.Runtime` | **Implementation.** Public only because the plug-in composes it; no promise to a mod, and it may change shape in any commit. |
| `CasualtiesUnknownOnline.GameAdapter` | **Implementation.** It is the only project that may reference the game assemblies, so anything it exposes is coupled to a game build by construction. |
| Game assemblies (`Assembly-CSharp`, the Unity modules) | **Reachable through a declaration.** The contract never references them — the adapter is the boundary for everything the API offers — but a mod that needs the game's own code may bind it and declare that binding under the [tiers](#the-tiers) below. |

**The visibility rule.** A type or member defaults to the narrowest visibility its implementation
needs. Only a capability that is designed, documented and reviewed becomes a public third-party
contract — "it compiled because it was public" is not a contract, and `internal` plus
`InternalsVisibleTo` is the normal shape for everything the framework shares with its own tests. The
rule, its rationale and the gate that enforces it are
[Gates and binding rules](../contributing/gates-and-rules.md) #14.

## The tiers

Compatibility is layered rather than one-size-fits-all, and a mod takes the **narrowest tier that
expresses its feature**. The tiers exist so that "not in the contract" never has to mean "not
allowed":

| Tier | What the mod binds | What it gets | What it owes |
|---|---|---|---|
| 0 | The `Abstractions` public API | The contract: the [stability levels](#stability-levels) and the gated baseline | Nothing beyond the API's own rules |
| 1 | CUO's own implementation (`Runtime`, `GameAdapter`), [patched](glossary.md) by name | Allowed, and not treated as hostile | Accepting that a patch carries no promise |
| 2 | The game's own code | A **declared** [native binding](glossary.md): still a CUO mod, visible to the host and parity-checkable | The `[CuoMod]` `NativeBinding` declaration, and the game-update churn |
| 3 | Anything, as an unmanaged BepInEx plugin | No constraint at all | No visibility at all — no host can see it |

Tier 3 is not an enemy to defeat; it is the reason Tier 2 exists, because a mod that binds the game
outside CUO is invisible to every host. CUO does not detect an undeclared binding (see
[Patching CUO itself](#patching-cuo-itself)), so the declaration is opt-in honesty whose only force is
a host's parity policy: the declaration rides the session [handshake](glossary.md) and the host judges it against its
own with its `NativeBindingParity` rule — allow, warn (the default) or require
([Handshake consistency](mod-api.md#handshake-consistency)).

Whenever the curated native registry can express the feature it is the better answer — the registry is
part of the contract, a tier is not. Several mods binding the same thing is the promotion signal
described in [The promotion funnel](#the-promotion-funnel): that is an API addition, not a wider tier.

## Stability levels

Every public `Abstractions` surface has a level, declared with
`[ApiStability(ApiStabilityLevel.<level>)]` on the type — or on a single member when that member
differs from its type. **A surface without the attribute is `Stable`**, so a level is a declaration
someone made, never a default that merely happened.

| Level | What an author may rely on |
|---|---|
| `Stable` | The shape survives a CUO update. An addition, removal or change is a reviewed change: the public-surface baseline captures it, and a removal names its reason. |
| `Experimental` | Documented and usable, and allowed to move while it settles. A new surface starts here; the promotion funnel below is how it becomes `Stable`. |
| `Advanced` | Supported as documented, but its shape follows the **game** rather than CUO's own model, so a game update may move it with no CUO API decision. The native escape hatch is the canonical case: `IModNativeApi` (a curated operation registry whose available operations follow the adapter's registration) and `IModNativeLocalPlayerState` (a projection of the game's own body fields). |
| `Obsolete` | Still works, must not be adopted by a new mod, and may be removed once nothing uses it. |

## The public-surface baseline

`docs/contracts/abstractions-api-baseline.txt` is the reviewed record of the `Abstractions` public
surface: every public type, its base list, and every public member with its signature and its level.
`ApiSurfaceGateTests` re-derives the surface from the project's source and compares:

- an **addition** fails until the baseline is reviewed and the line is added;
- a **removal** fails unless the line is deleted **and** a `*REMOVED* <key> — <reason>` tombstone is
  added, so a removal is a deliberate act with a stated reason rather than a quiet deletion;
- a **change** to a recorded line — a signature, a declaration modifier, an accessor, a default value,
  a stability level — fails as `CHANGED`;
- a malformed, duplicated or contradictory line fails as such, and a census floor keeps a scan that
  silently finds nothing from passing.

When the gate fails it writes the candidate to `artifacts/api-surface/abstractions-api-baseline.txt`
(gitignored): review that file, copy the lines you mean to approve into the baseline, and commit both
with the change that altered the API. The normalization is part of the contract — type references are
recorded by their simple name (a `using` edit is not an API change), whitespace is collapsed, implicit
enum values are recorded as `implicit #<ordinal>` (so reordering an enum is an API change while
re-spelling a reference is not), a C# 14 `extension` block folds its receiver into each member's
parameter list, and an `[ApiStability]` argument the gate cannot resolve to a level is a failure rather
than a silent fallback to the default level.

## Patching CUO itself

Harmony patching of CUO's own code is **allowed and not treated as hostile**: `Runtime` and
`GameAdapter` are implementations, a patch is a supported way to extend them, and nothing in CUO tries
to detect or defeat one. There is no anti-cheat stance here, and no obfuscation.

What a patch does **not** buy is a promise. A Harmony patch binds to a method's identity and its
argument names, so it can break on any CUO commit — including one that fixes something else — and that
risk belongs to the patch, not to CUO. Concretely:

- Prefer the contract. If the API can express the feature, use it; a patch is the escape hatch, not the
  first choice.
- Bind as precisely as you can — the patch class's full name and the target signature are what the
  framework's own patch-inventory contracts check — and report only writes you verified.
- Do not expect an announcement when a patched method moves. The wire protocol version is the only
  compatibility boundary CUO enforces (see [Wire and save compatibility](#wire-and-save-compatibility)),
  and it covers the network, not your patch.
- If several mods end up needing the same thing, that is the promotion signal below — bring it to the
  API instead of maintaining parallel patches.

## The diagnostics an author can expect

CUO's diagnostics are log lines, deliberately: no debugger integration, no secret channel. A mod
author can rely on these, and their absence is a bug worth reporting:

- **Your own logger.** `IModContext.Logger` writes as `[Mod:<id>]`, so your lines are attributable in a
  shared log.
- **Discovery and validation.** `ModRegistry` logs `[Mods] discovered <Id> <Version> (<Mode>,
  permissions <Permissions>, namespace <Namespace>, binds <NativeBinding>) — <DisplayName>.` for every
  accepted mod (`binds -` when the mod declared no native binding) and a `[Mods] <Id> … — skipped.`
  line naming the reason for every rejected one: an empty id, a missing `NetworkMode`, an invalid
  SemVer, invalid permissions, a namespace conflict, a missing dependency, a dependency cycle or a
  duplicate id.
- **Lifecycle isolation.** A mod that throws is isolated: `[Mods] <Id> failed to load — skipped, the
  other mods continue.` at load and `[Mods] <Id> threw in <Stage> — isolated, the pump continues.` per
  frame stage. Other mods keep running.
- **Permission and shape refusals.** Every gate says what it refused and why in the same shape:
  `[Mods] <ModId> does not declare <Permission> — the call is refused.`, `… is already declared … the
  duplicate is refused.`, `… reached the <Cap>… cap — … refused.`
- **Command results.** A guest's host-command request is settled observably:
  `[Mods] <ModId>/<Name> result for <Requester> (request <RequestId>, success <True/False>).`, and a
  request with no answer settles on the requester's own deadline with a named failure.
- **Local console commands.** `[Mods] <ModId> registered local console command /<Name>.`
- **The framework's own health.** The Game Adapter prints `Game Adapter capability report:` once at
  startup — one line per capability with its Required/Optional class, its contract count and its
  failure reasons — at `Error` level when the report refuses the session. If the framework itself
  refuses multiplayer, that line names which gameplay system broke.

## The promotion funnel

1. **A patch.** Someone needs a surface the API does not have; a Harmony patch, or a private workaround,
   proves the need without paying for an API.
2. **Several mods need the same thing.** One mod's convenience is not an API. Two independent
   consumers, or one consumer whose patch keeps breaking, is the trigger.
3. **Experimental API.** The surface is designed against the real consumers, documented in
   [The mod API contract](mod-api.md), marked `[ApiStability(ApiStabilityLevel.Experimental)]`, and
   recorded in the baseline. It may move.
4. **Stable API.** Once the shape has survived a round of real use, the marker is removed — or changed
   to `Stable` — as a reviewed change. From then on it is frozen in the sense of
   [Stability levels](#stability-levels).

Promotion is a decision, not a drift: it lands as a ticket or a decision entry with the consumers
named, and never as "it was public for a while, so it is stable now".

## Wire and save compatibility

The compatibility boundary is the protocol-version check at the handshake: the host drops a
`HandshakeMsg` whose `Protocol` differs, and the guest ends the session on a mismatched
`HandshakeAckMsg.Protocol`. A mixed-version session therefore never exists, which is why a wire change
is never held back for compatibility's sake.

- A mod that adds wire behaviour bumps `ProtocolVersion.Current` in the same change. The constant's own
  doc comment is the wire-change log, and no live document restates the number — a record of a past
  state keeps the number it was written with, and `ProtocolNumberGateTests` fails a live governance
  document that restates it.
- A mod that touches only local or read-only surfaces and adds no wire change does not bump it.
- Mod versions are strict SemVer, compared by precedence for state-bearing modes; the handshake matrix
  in [The mod API contract](mod-api.md#handshake-consistency) is the contract.

## Related reading

- [The mod API contract](mod-api.md) — what a mod declares and what CUO enforces
- [Repository map and pitfalls](../contributing/repository-map-and-pitfalls.md) — where a new system belongs, and the six-question test
- [Permissions and what they protect](../internals/permissions-and-security.md) — what a declaration buys, and what CUO refuses to defend
- [Gates and binding rules](../contributing/gates-and-rules.md) — the visibility rule and the rest of the binding conventions
- [The shape of CUO](../internals/architecture-overview.md) — the layers these tiers talk about

---

[Documentation](../README.md) > [Reference](README.md) > The modification policy

# Native-binding mods declare it instead of hiding

- Status: Review
- Priority: Medium
- Category: Mod API / Policy
- Related: [Native-binding parity in the session handshake](../todo/mod-native-binding-handshake-parity.md)
  carries the declaration into the session.

## Why this exists

`docs/api/mod-api.md` §1 states the mod contract as "a mod never touches BepInEx, Steamworks, the
game assemblies, or CUO.Runtime". Read literally, that welds the ecosystem's ceiling shut: the mods
that need the game's own code — a native-UI extension such as the pinyin search, a quality-of-life
patch, a mechanic change — are told they cannot be CUO mods at all. They do not stop existing; they
just exist outside the framework, where no host can see them.

The owner's ruling (2026-09-20, decision 204) is that compatibility and extensibility are **tiered**,
not one-size-fits-all: the framework yields space instead of welding the ceiling, and a mod that
binds the game's own code owes a **declaration** in exchange for staying inside.

## The tiers (the policy lands here)

| Tier | What the mod binds | What it gets | What it owes |
|---|---|---|---|
| 0 | The `Abstractions` public API | The contract: `[ApiStability]` levels and the gated public-surface baseline | Nothing beyond the API's own rules |
| 1 | CUO's own implementation (`Runtime`, `GameAdapter`), patched by name | Allowed and not treated as hostile (`advanced-modification-policy.md` §4) | Accepting that a patch carries no promise |
| 2 | **The game's own code** (this ticket) | A declared binding: still a CUO mod, visible to the host and parity-checkable | The declaration, and the game-update churn |
| 3 | Anything, as an unmanaged BepInEx plugin | No constraint at all | No visibility at all |

Tier 3 is not an enemy to defeat; it is the reason Tier 2 exists. CUO does not detect an undeclared
binding, so the declaration is opt-in honesty whose only force is a host's parity policy. A mod
takes the narrowest tier that expresses its feature: the curated `IModNativeApi` registry
(`docs/api/mod-api.md` §4i) before a binding, and a declaration before silence.

## What the declaration is

- **A manifest field, not a permission.** `[CuoMod]` gains `NativeBinding`. `ModPermission` is the
  enum CUO **enforces** (`SendNetworkMessage`, `AccessNativeApi`, …); a permission CUO cannot
  enforce would be a false promise, so the binding is a declared fact with its own field.
  **Rejected**: a ninth permission flag — it would read as a grant CUO hands out, and CUO grants
  nothing here.
- **Discovery reports it.** The `[Mods] discovered <Id> <Version> (<Mode>, permissions …)` line
  gains the declaration, so a host's log answers "which mod binds the game's own code" without
  reading any mod's source.
- **It changes no promise.** A declared binding buys visibility, never stability: the shape it
  binds belongs to the game, a game update may break it with no CUO decision, and CUO neither
  sandboxes nor detects it.
- **It is the author's claim, in one checkable place.** The mod's documentation and its manifest
  must agree; a mod that binds the game without declaring it is out of contract even though nothing
  will catch it automatically.

## Stages

### Stage 1 — the field, the tier model and the visibility (landed, no wire)

- `[CuoMod]` gained `NativeBinding` (`src/CasualtiesUnknownOnline.Abstractions/CuoModAttribute.cs`):
  the game's own code the mod patches, named by the author, or nothing at all. Discovery trims it,
  normalizes a blank value (empty or whitespace-only) to "no declaration" instead of rejecting the
  mod, and carries
  it into `ModManifest.NativeBinding` — a declared fact, never a grant.
- The `[Mods] discovered …` line closes its parenthesis with `binds <declaration>` (`-` when the mod
  declared none), so a host's log answers "which mod binds the game's own code" without reading any
  mod's source.
- The tier table landed in `docs/api/advanced-modification-policy.md` §1.1 (decision 204), which is
  also where `docs/api/mod-api.md` §1's literal wording was corrected (referencing versus binding);
  §3 now documents the field.
- The reviewed `Abstractions` baseline gained the two members and the `ModManifest` constructor
  parameter (`docs/api/abstractions-api-baseline.txt`) — the contract change this ticket makes.
- Tests: `ModNativeBindingDeclarationTests` (10 cases) — discovered and carried on the manifest,
  reported in the discovery log, undeclared stays null, a blank value normalizes without rejecting,
  the declared name is trimmed, the declaration takes no permission/network contract/dependency, it
  is never a rejection cause, and the wire shape (`ModInfoMsg`) is unchanged. `ModDiscoveryTests`
  stays green (29 cases in the focused run).
- Decision 206 records why the declaration is a manifest field rather than a ninth `ModPermission`.
- Independent adversarial review (fresh context, frozen tree, report at
  `%TEMP%\cuo-review-native-binding-declaration.md`): 0 blocker / 1 major / 4 minor / 5 nit, all fixed
  here — the major was the moved ticket's own two outbound links, the minors the stale discovery-log
  contract in the policy document, decision 204's now-unverifiable quotation, two inconsistent gate
  figures, and the missing namespace+binding case. The round added
  `BacklogIntegrityGateTests.EveryRelativeDocumentLink_Resolves`, which resolves every relative link
  under `docs/` — the move that broke this file's links is exactly what it catches.

### Stage 2 — session parity

Its own ticket: [Native-binding parity in the session handshake](../todo/mod-native-binding-handshake-parity.md).

## Acceptance

- A mod can declare a native binding in `[CuoMod]`, is accepted, and the declaration appears in the
  discovery log and the registry.
- The policy documents state the tiers, and no live document still claims that a mod can never bind
  the game's own code.
- The declaration is presented as visibility, never as enforcement: the documents say plainly that
  an undeclared binding is undetectable.

## Limits / open questions

- **Declared ≠ detected.** CUO has no way to see a binding that is not declared, and this ticket
  deliberately does not build one — that would be an anti-cheat stance the project does not take
  (`advanced-modification-policy.md` §4).
- **What a host may do with the declaration** is Stage 2's design (allow / warn / require parity).
  The declaration alone must not silently change who can join.
- **Not a substitute for the curated API.** A binding several mods need is the promotion signal
  (`advanced-modification-policy.md` §6): the answer is a registered `IModNativeApi` operation, not
  a wider tier.
- **Why not higher priority**: the framework ships without this. Nothing is broken today — what is
  wrong is a ceiling that keeps legitimate mods out of sight, and that is design debt paid on
  purpose, not a defect holding a release.

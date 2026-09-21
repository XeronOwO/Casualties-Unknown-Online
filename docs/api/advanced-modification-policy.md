# Advanced modification policy: contracts, stability levels, and patching CUO itself

Status: binding for contributors and for mod authors. The mod-facing API itself is
`docs/api/mod-api.md`; this page answers the questions that document deliberately does not:
which CUO surfaces are a promise, which are only an implementation, and what an author is
entitled to when a promise is not given.

## 1. What is a contract

| Surface | Status |
|---|---|
| `CasualtiesUnknownOnline.Abstractions` public API | **The contract.** It is the only assembly a mod may reference (`docs/api/mod-api.md` §1, architecture §5.5). |
| `CasualtiesUnknownOnline.Runtime` | **Implementation.** Public only because the plugin composes it; no promise to a mod, and it may change shape in any commit. |
| `CasualtiesUnknownOnline.GameAdapter` | **Implementation.** It is the only project that may reference the game assemblies, so anything it exposes is coupled to a game build by construction. |
| Game assemblies (`Assembly-CSharp`, Unity modules) | **Reachable through a declaration.** The contract never references them — the adapter is the boundary for everything the API offers — but a mod that needs the game's own code may bind it and declare that binding (§1.1; ticket `docs/backlog/review/mod-native-binding-declaration.md`). |

**The visibility rule (binding).** A type or member defaults to the narrowest visibility its
implementation needs. Only a capability that is designed, documented and reviewed becomes a
public third-party contract — "it compiled because it was public" is not a contract, and
`internal` plus `InternalsVisibleTo` is the normal shape for everything the framework needs to
share with its own tests.

### 1.1 The tiers: compatibility is layered, not one-size-fits-all

A mod takes the narrowest tier that expresses its feature. The tiers exist so that "not in the
contract" never has to mean "not allowed" (owner ruling 2026-09-20, decision 204):

| Tier | What the mod binds | What it gets | What it owes |
|---|---|---|---|
| 0 | The `Abstractions` public API | The contract: `[ApiStability]` levels and the gated baseline | Nothing beyond the API's own rules |
| 1 | CUO's own implementation (`Runtime`, `GameAdapter`), patched by name | Allowed and not treated as hostile (§4) | Accepting that a patch carries no promise |
| 2 | The game's own code | A DECLARED binding: still a CUO mod, visible to the host and parity-checkable | The `[CuoMod]` `NativeBinding` declaration (`docs/api/mod-api.md` §3), and the game-update churn |
| 3 | Anything, as an unmanaged BepInEx plugin | No constraint at all | No visibility at all — no host can see it |

Tier 3 is not an enemy to defeat; it is the reason Tier 2 exists, because a mod that binds the game
outside CUO is invisible to every host. CUO does not detect an undeclared binding (§4's
no-anti-cheat stance), so the declaration is opt-in honesty whose only force is a host's parity
policy. Whenever the curated native registry (`docs/api/mod-api.md` §4i) can express the feature it
is the better answer, and several mods binding the same thing is the promotion signal in §6 — an
API addition, not a wider tier.

### 1.2 Which systems live where: four layers, one test

The tiers above say how a mod may bind CUO. This section says where a FEATURE belongs — the question
a contributor actually asks ("should this be its own mod?"). Four layers, in order of distance from
the framework:

| Layer | What it is | Ships with the plug-in? | Examples |
|---|---|---|---|
| Framework core | A capability the framework's own operation needs: its control plane, its administration and safety surface, its own results reaching the player, the shared simulation | Yes | the command console, the save layer, the session/world/entity domains, the mod loader |
| Satellite mod | A game-facing feature with no session vocabulary of its own; it works with CUO uninstalled | No — its own mod | pinyin search (`docs/backlog/todo/pinyin-search-standalone-mod.md`) |
| Repository tool | Needs neither the game at runtime nor the plug-in's dependency graph; it serves development and verification | No — and it is not a mod | the game-update contract toolchain (`tools/CasualtiesUnknownOnline.ContractTool`, decision 201) |
| Reusable component | Machinery several consumers can share, with no session vocabulary of its own | Depends on its consumers | the console's input/completion engine; the pinyin matcher core |

**The test** — six questions, in order; the first two decide on their own:

1. Does its vocabulary name session, authority, world, save or mod state? Yes → framework core.
2. Does the framework still work without it — its administration, its safety surface, its own
   results reaching the player? No → framework core.
3. Does it still make sense with CUO uninstalled? No → framework core; yes → keep asking.
4. Is it game-facing experience or session-facing capability? Session-facing → framework core.
5. Would extracting it create a two-way dependency, or force the framework to publish a large new
   contract? Yes → framework core, or a component rather than a satellite.
6. Does it need the game's own code? Yes → the satellite binds it through the declared tier (§1.1).

Worked examples, so the next reader does not re-derive this:

- **Pinyin search** — 1 no, 2 no, 3 yes, 4 game-facing → satellite (ticket above).
- **The command console** — 1 yes (its verbs and its 200-line output buffer carry the session's own
  results), 2 yes (a host without it loses the administration and save verbs and stops seeing the
  save/restore/starting-supply accounts), 3 no, 5 yes → framework core. Its input/completion engine
  is the component candidate, and §6's own rule says a component waits for its second consumer.

A split is never free: each shipped artifact adds its own build, deploy, verification and acceptance
surface, and a framework whose control plane is an optional add-on has made governance optional.

## 2. Stability levels

Every public `Abstractions` surface has a level. It is declared with
`[ApiStability(ApiStabilityLevel.<level>)]` — `src/CasualtiesUnknownOnline.Abstractions/ApiStabilityAttribute.cs` —
on the type, or on a single member when that member differs from its type. **A surface without
the attribute is `Stable`**, so a level is a declaration someone made, never a default that
happened.

| Level | What an author may rely on |
|---|---|
| `Stable` | The shape survives a CUO update. An addition, removal or change is a reviewed change: the public-surface baseline captures it, and a removal names its reason. |
| `Experimental` | Documented and usable, and allowed to move while it settles. It is where a new surface starts; the promotion funnel in §6 is how it becomes `Stable`. |
| `Advanced` | Supported as documented, but its shape follows the **game** rather than CUO's own model, so a game update can move it with no CUO API decision. The native escape hatch is the canonical case: `IModNativeApi` (a curated operation registry whose available operations follow the adapter's registration) and `IModNativeLocalPlayerState` (a projection of the game's own body fields, whose members cite the game's `Body` fields). |
| `Obsolete` | Still works, must not be adopted by a new mod, and may be removed once nothing uses it. |

The levels are also recorded, per entry, in the baseline (§3), so a level change is reviewed like
any other API change.

## 3. The public-surface baseline

`docs/api/abstractions-api-baseline.txt` is the reviewed record of the `Abstractions` public
surface — every public type, its base list, and every public member with its signature and its
level. `ApiSurfaceGateTests` re-derives the surface from the project's source and compares:

- an **addition** fails until the baseline is reviewed and the line is added;
- a **removal** fails unless the line is deleted **and** a
  `*REMOVED* <key> — <reason>` tombstone is added, so a removal is a deliberate act with a stated
  reason rather than a quiet deletion;
- a **change** to a recorded line (a signature, a declaration modifier, an accessor, a default value,
  a stability level) fails as `CHANGED`;
- a malformed, duplicated or contradictory line fails as such, and a census floor keeps a gate
  that silently finds nothing from passing.

When the gate fails it writes the candidate file to
`artifacts/api-surface/abstractions-api-baseline.txt` (gitignored): review that file, copy the
lines you mean to approve into the baseline, and commit both with the change that altered the API.
The normalization is part of the contract: type references are recorded by their simple name (a
`using` edit is not an API change), whitespace is collapsed, implicit enum values are recorded as
`implicit #<ordinal>` (so reordering an enum is an API change while re-spelling a reference is not),
a C# 14 `extension` block folds its receiver into each member's parameter list (so an extension
member is recorded like any other callable member), and an `[ApiStability]` argument the gate cannot
resolve to a level is a failure rather than a silent fallback to the default level.

## 4. Patching CUO itself

Harmony patching of CUO's own code is **allowed and not treated as hostile**: `Runtime` and
`GameAdapter` are implementations (§1), a patch is a supported way to extend them, and nothing in
CUO tries to detect or defeat one. There is no anti-cheat stance here, and no obfuscation.

What a patch does **not** buy is a promise. A Harmony patch binds to a method's identity and its
argument names, so it can break on any CUO commit — including one that fixes something else — and
that risk belongs to the patch, not to CUO. Concretely:

- Prefer the contract. If the API can express the feature, use it; a patch is the escape hatch,
  not the first choice.
- Bind as precisely as you can (the patch class's full name and the target signature are what the
  framework's own `PatchInventory` contracts check), and report only writes you verified.
- Do not expect an announcement when a patched method moves. The wire protocol version is the
  only compatibility boundary CUO enforces (§7), and it covers the network, not your patch.
- If several mods end up needing the same thing, that is the promotion signal in §6 — bring it to
  the API instead of maintaining parallel patches.

## 5. The diagnostics an author can expect

CUO's diagnostics are log lines, deliberately: no debugger integration, no secret channel. A mod
author can rely on these, and their absence is a bug worth reporting:

- **Your own logger.** `IModContext.Logger` writes as `[Mod:<id>]`, so your lines are attributable
  in a shared log.
- **Discovery and validation.** `ModRegistry` logs `[Mods] discovered <Id> <Version> (<Mode>,
  permissions <Permissions>, namespace <Namespace>, binds <NativeBinding>) — <DisplayName>.` for
  every accepted mod (`binds -` when the mod declared no native binding, §1.1) and a
  `[Mods] <Id> … — skipped.` line naming the reason for every rejected one (empty id, missing
  `NetworkMode`, invalid SemVer, invalid permissions, a namespace conflict, a missing dependency, a
  dependency cycle, a duplicate id).
- **Lifecycle isolation.** A mod that throws is isolated: `[Mods] <Id> failed to load — skipped,
  the other mods continue.` at load and `[Mods] <Id> threw in <Stage> — isolated, the pump
  continues.` per frame stage. Other mods keep running.
- **Permission and shape refusals.** Every gate says what it refused and why in the same shape:
  `[Mods] <ModId> does not declare <Permission> — the call is refused.`, `… is already declared …
  the duplicate is refused.`, `… reached the <Cap>-… cap — … refused.`
- **Command results.** A guest's host-command request is settled observably:
  `[Mods] <ModId>/<Name> result for <Requester> (request <RequestId>, success <True/False>).`, and a
  request with no answer settles on the requester's own deadline with a named failure.
- **Local console commands.** `[Mods] <ModId> registered local console command /<Name>.`
- **The framework's own health.** The Game Adapter prints `Game Adapter capability report:` once at
  startup — one line per capability with its Required/Optional class, its contract count and its
  failure reasons — at `Error` level when the report refuses the session. If the framework itself
  refuses multiplayer, that line says which gameplay system broke.

## 6. The promotion funnel

1. **A patch.** Someone needs a surface the API does not have; a Harmony patch (or a private
   workaround) proves the need without paying for an API.
2. **Several mods need the same thing.** One mod's convenience is not an API. Two independent
   consumers, or one consumer whose patch keeps breaking, is the trigger.
3. **Experimental API.** The surface is designed against the real consumers, documented in
   `docs/api/mod-api.md`, marked `[ApiStability(ApiStabilityLevel.Experimental)]`, and recorded in
   the baseline. It may move, and the policy doc's notice rule applies instead of the `Stable`
   freezing rule.
4. **Stable API.** Once the shape has survived a round of real use, the marker is removed (or
   changed to `Stable`) as a reviewed change. From then on it is frozen in the sense of §2.

Promotion is a decision, not a drift: it lands as a ticket or decision entry with the consumers
named, and never as "it was public for a while, so it is Stable now".

## 7. Wire and save compatibility

The compatibility boundary is the protocol-version check at the handshake: the host drops a
`HandshakeMsg` whose `Protocol` differs, and the guest ends the session on a mismatched
`HandshakeAckMsg.Protocol`. A mixed-version session therefore never exists, which is why a wire
change is never held back for compatibility's sake (decision 188).

- A mod that adds wire behavior bumps `ProtocolVersion.Current` in the same change; the constant's
  own doc comment is the wire-change log, and no live document restates the number
  (`ProtocolNumberGateTests` scans `AGENTS.md`, `docs/api/**`, the active decision register and the
  live architecture/development/evidence pages; a record of a past state keeps the number it was
  written with).
- A mod that touches only local or read-only surfaces and adds no wire change does not bump it.
- Mod versions are strict SemVer, compared by precedence for state-bearing modes; the handshake
  matrix in `docs/api/mod-api.md` §5 is the contract.

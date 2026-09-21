# Native-binding declaration (Stage 1 of the declared native-binding tier)

Date: 2026-09-21
Scope: `[CuoMod]`'s `NativeBinding` — the manifest field, its discovery path, the discovery log
line, the reviewed `Abstractions` baseline and the policy documents. Stage 2 (carrying the
declaration onto the handshake behind a host policy) is its own ticket
(`docs/backlog/todo/mod-native-binding-handshake-parity.md`), so this stage is wire-free on purpose.

## What landed

- **The field** (`src/CasualtiesUnknownOnline.Abstractions/CuoModAttribute.cs`): `NativeBinding` is
  an optional string naming the game's own code the mod binds. It is a declared fact, not a
  permission — `ModPermission` stays the enum CUO enforces, and nothing about the declaration is
  enforced.
- **The carry** (`ModManifest.NativeBinding`): discovery copies the declaration into the manifest the
  framework and the session actually use.
- **The normalization** (`ModRegistry.Discover`): the value is trimmed and a blank value (empty or
  whitespace-only) becomes "no declaration" instead of a rejection cause — a declaration must never become a new way
  to fail discovery.
- **The visibility** (the `[Mods] discovered …` line): the parenthesis now ends with
  `binds <declaration>` (`-` when the mod declared none), so a host's log answers "which mod binds
  the game's own code" without reading any mod's source.
- **The contract record** (`docs/api/abstractions-api-baseline.txt`): two member lines plus the
  `ModManifest` constructor parameter — the reviewed surface change this ticket makes.
- **The documents**: `docs/api/mod-api.md` §3 lists and explains the field;
  `docs/api/advanced-modification-policy.md` §1.1's Tier 2 row names it; decision 206 records why it
  is a manifest field rather than a ninth `ModPermission`.
- **Not in this stage**: the wire. `ModInfoMsg` is unchanged (asserted by a test), so a declaration
  stays local until the parity ticket carries it with its own protocol bump.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| `[CuoMod]` surface | New `NativeBinding` string property | `ModNativeBindingDeclarationTests.DeclaredBinding_IsDiscoveredAndCarriedOnTheManifest`; baseline line `CuoModAttribute.NativeBinding` |
| Manifest | New `ModManifest.NativeBinding` and constructor parameter | same test; baseline `ModManifest.ctor(... string? nativeBinding = null)` line |
| Discovery normalization | Trim; blank (empty or whitespace-only) becomes undeclared | `PaddedBinding_IsTrimmedToTheDeclaredName`, `WhitespaceOnlyBinding_NormalizesToUndeclared_WithoutRejectingTheMod` |
| Discovery log | `binds <declaration>` / `binds -` | `DeclaredBinding_AppearsInTheDiscoveryLogLine`, `UndeclaredMod_HasNoBinding_AndTheLogSaysSo` |
| Rejection surface | Unchanged — the declaration adds no rejection cause | `Declaration_IsNeverARejectionCause`; `ModDiscoveryTests` (19 cases) unchanged and green |
| Permission surface | Unchanged — the declaration grants nothing | `Declaration_TakesNoPermissionAndNoNetworkContract` |
| Wire surface | Unchanged — `ModInfoMsg` keeps its four properties | `Declaration_DoesNotMoveTheWireShape` |

## Verification design

- **Focused**: `dotnet test CasualtiesUnknownOnline.slnx --filter
  "FullyQualifiedName~ModNativeBindingDeclaration|FullyQualifiedName~ModDiscoveryTests"` → **29
  passed / 0 failed**.
- **Normative gates**: the API-surface gate failed first with its candidate written to the
  gitignored `artifacts/api-surface/` — the reviewed-baseline mechanism working as designed — and the
  reviewed file was updated with exactly the three expected line changes; the gate set then passed
  **84 / 84** (its two new cases are the relative-link gate added by the review round below).
- **Full suite** (build included): **3713 passed / 0 failed**, 47 s.

- **Independent adversarial review** (fresh context, frozen tree, report at
  `%TEMP%\cuo-review-native-binding-declaration.md`): 0 blocker / 1 major / 4 minor / 5 nit, all fixed
  in the same change set. The major was the moved ticket's OWN two outbound links — the family audit
  had fixed the fan-in and stopped there; the minors were the policy document's stale discovery-log
  contract, decision 204's now-unverifiable quotation, two inconsistent gate figures, and the missing
  namespace+binding case. The round also produced a rot guard:
  `BacklogIntegrityGateTests.EveryRelativeDocumentLink_Resolves` resolves every relative link under
  `docs/` (370 links measured, census floor 250), because nothing caught the silent break.

## Test coverage split (stated exactly)

- **Covered**: the field's discovery path (declared, undeclared, blank in both spellings, padded, namespace+binding), the log
  rendering in both states, the absence of a new rejection cause, the absence of a permission or
  network contract, and the wire shape staying put.
- **Not covered by a test**: whether a declaration is TRUE. CUO cannot see an undeclared binding and
  does not try — the claim is checked by a human against the mod's own documentation, which is the
  point of the tier (decision 204).

## What the user must verify (not provable by automation)

- Nothing beyond reading a log line: with a mod that declares a binding installed, that mod's
  `[Mods] discovered …` line in the BepInEx log shows `binds <declaration>`. This stage adds no UI
  and no gameplay surface.

## Limits (recorded, not hidden)

- **Declared is not detected**: an undeclared binding stays invisible; this ticket deliberately
  builds no detection (the project takes no anti-cheat stance).
- `docs/history/architecture-blueprint.md` still carries the pre-decision line "Mods never reference
  BepInEx, Steamworks, or the game's private assemblies". It is a HISTORY document, not a live
  contract; the live wording is `docs/api/mod-api.md` §1/§3 and
  `docs/api/advanced-modification-policy.md` §1.1, both corrected. Recorded here rather than
  rewritten, because `history/` is the record of what was believed then.
- What a host may DO with the declaration (allow / warn / require parity) is Stage 2's design; the
  declaration alone never changes who can join.

# Native-binding parity in the session handshake

Date: 2026-09-21
Scope: the declared native binding's journey onto the wire and the host's parity policy — the
`ModInfoMsg` field, the shared normalization rule, the three-level `HostRules.NativeBindingParity`
rule, the handshake judgement, the config/UI surfaces and the documents. Depends on the declaration
ticket (`docs/backlog/review/mod-native-binding-declaration.md`, decision 206) and completes the
Tier 2 promise of `docs/api/advanced-modification-policy.md` §1.1.

## What landed

- **The field on the wire** (`src/CasualtiesUnknownOnline.Runtime/Protocol/Messages/ModInfoMsg.cs`):
  `NativeBinding` (ProtoMember 5), null when the mod declared none. `ModRegistry.CurrentModInfos`
  fills it from the discovery manifest, so the handshake entry carries what the mod published
  locally. Wire change → `ProtocolVersion.Current` bumped in the same change (35 in the constant's
  own comment, which is the wire-change log).
- **One normalization rule, two callers**
  (`src/CasualtiesUnknownOnline.Runtime/Session/Mods/NativeBindingDeclaration.cs`): "blank = none",
  trimmed — the same rule discovery applied is now what the handshake applies to the received value,
  so a blank and an absent declaration are the same answer on both sides.
- **The judgement** (`HandshakeHandler.CheckNativeBindingParity`): per mod id, only for a mod BOTH
  sides list, evaluated after the NetworkMode contract checks and before the member is created. It
  never joins the NetworkMode contract and adds no rejection cause of its own.
- **The policy** (`HostRules.NativeBindingParity`, `HostRulesOptions`/`IHostRules`/
  `HostRulesService`): `allow` (silent) / `warn` (default: admitted, mismatch recorded in the host's
  log) / `require` (refused before creation, the log naming the mod and both declarations).
- **The surfaces**: BepInEx `[HostRules] NativeBindingParity` entry (unparseable hand-edited value
  falls back to warn), the console host-rule JSON property `nativebindingparity`, the Online UI
  admin row (three-level toolbar, read-only label for guests) and the en/zh localization keys.
- **The documents**: `docs/api/mod-api.md` §3 (the declaration rides the handshake) and §5 (the
  parity matrix row plus what parity proves and does not prove), `advanced-modification-policy.md`
  §1.1, the ticket, and decision 208.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Wire entry | New `ModInfoMsg.NativeBinding`, absent = null | `ModHandshakeProtocolTests.HandshakeWithMods_RoundTripsExactly`, `.ModInfoWithoutNativeBinding_DecodesToNull`; shape pinned by `ModNativeBindingDeclarationTests.Declaration_TravelsOnTheWireShape` |
| Carrier | `ModRegistry.CurrentModInfos` copies the manifest declaration into the handshake entry | `ModNativeBindingDeclarationTests.DeclaredBinding_RidesTheHandshakeInfo` — the real discovery → `CurrentModInfos()` path (the handshake's own tests replace the list provider with a stub, so this is the one case that would fail if the field were never filled) |
| Normalization | Blank/whitespace = undeclared, trimmed; one rule for discovery and the wire | `NativeBindingDeclaration`; `ModHandshakeTests.BlankGuestBinding_NormalizesToUndeclared_RequirePolicyAccepted`, `.BindingComparison_IsTrimmedAndCaseSensitive` |
| Granularity | Judged per mod id, only when both sides list the mod | `ModHandshakeTests.NoDeclarationOnEitherSide_Accepted`, `.EqualNativeBindings_Accepted`, `.ModOnlyTheGuestLists_IsNotJudgedByParity` |
| Difference detection | A declaration against none is a difference | `ModHandshakeTests.UndeclaredGuestBinding_RequirePolicy_Refused` (the log renders it as `none`), `.UndeclaredHostBinding_WarnPolicy_RecordsTheHostAsNone` |
| Policy — allow | Admitted, nothing recorded | `ModHandshakeTests.DifferingDeclarations_AllowPolicy_AdmitsWithoutRecording` |
| Policy — warn (default) | Admitted, mismatch recorded with mod + both declarations | `ModHandshakeTests.DifferingDeclarations_WarnPolicy_AdmitsAndRecordsTheMismatch`; `HostRulesPolicyTests.DefaultNativeBindingParity_IsWarn` |
| Policy — require | Refused before member creation, mod + both declarations named | `ModHandshakeTests.DifferingDeclarations_RequirePolicy_RefusesAndNamesTheModAndBothDeclarations` |
| Config text | `allow`/`warn`/`require`, case-insensitive, unknown → warn | `HostRulesPolicyTests.NativeBindingParityText_ParsesTheThreeLevelsAndRoundTrips`, `.NativeBindingParityText_UnknownValue_FailsAndFallsBackToWarn` |
| Service surface | `IHostRules.NativeBindingParity` exposed | `HostRulesPolicyTests.HostRulesService_ComposesNewFlagsAndRespawnFlags` |
| Protocol | `ProtocolVersion.Current` bumped in the same change | the constant's 35 entry; `SyncCoverageGateTests` quote anchors updated to the new line |

## Verification design

- **Focused** (`--filter "FullyQualifiedName~ModHandshakeTests|FullyQualifiedName~ModHandshakeProtocolTests|FullyQualifiedName~HostRulesPolicyTests|FullyQualifiedName~ModNativeBindingDeclarationTests"`):
  **65 passed / 0 failed** (2 s), build included — the final count after the review round below added
  the carrier guard, the case/trim theory and the host-side `none` case.
- **Build**: `dotnet build CasualtiesUnknownOnline.slnx` → **0 warnings / 0 errors**.
- **Normative gates**: **84 / 84** after two first-run failures, both real and both fixed in this
  change: `SourceShapeGateTests` (one top-level type per file — the parity enum and its text mapping
  now live in `NativeBindingParity.cs` and `NativeBindingParityText.cs`) and
  `SyncCoverageGateTests` (the protocol line quoted in `docs/evidence/sync-coverage-evidence.json`
  had to move to the new value — a bump's fan-out reaches the evidence anchors).
- **Full suite** (`dotnet test CasualtiesUnknownOnline.slnx`, build included): **3733 passed / 0
  failed** in the main project (51 s) plus **84 / 84** gates, the checklist-integrity case included.
- **`dotnet format`**: exit 0.
- **Independent adversarial review** (fresh context, frozen tree, report at
  `%TEMP%\cuo-review-native-binding-parity.md`): 0 blocker / 1 major / 6 minor / 3 nit, all fixed in
  this change. The major was a real false green — the discovery → wire carrier was covered only by a
  stub, so deleting `NativeBinding = d.Manifest.NativeBinding` would have kept every test green while
  this file claimed evidence for it; the fix is `DeclaredBinding_RidesTheHandshakeInfo` over the real
  `ModRegistry.CurrentModInfos()` path. The minors were wording that contradicted the implementation
  (the protocol entry's warn clause, §5's unqualified "every member" promise), the exact/case-sensitive
  comparison being undocumented and untested, log assertions that could not tell warn from require
  apart, the untested `none` rendering, and the stage-1 class summary still saying the declaration did
  not move the wire.

## Test coverage split (stated exactly)

- **Covered**: the wire round-trip and the absent-field decode; the shared normalization; every
  policy level (allow/warn/require) including the default; the per-mod-id granularity (guest-only mod
  not judged); the refusal/report content (mod id and both declarations, asserted through
  `RecordingLoggerFactory`); the text mapping and its fallback; the service exposure; the pinned wire
  shape.
- **Not covered by a test**: the Plugin-layer surfaces — the BepInEx entry binding, the Online UI
  admin row and the config editor's `nativebindingparity` write path (the Plugin project has no test
  host). Their logic is thin and their inputs are covered (`NativeBindingParityText`), but the row
  itself is a user-visible surface verified on the physical machine.
- **Not covered, by design**: whether a declaration is true. An undeclared binding stays
  undetectable (decision 204), so no check can exist for it.

## What the user must verify (not provable by automation)

- The admin page's new "Native binding parity" row shows the host's current level, switching it
  persists to the config, and a guest sees the read-only label.
- With two clients whose mods declare different bindings, the host log shows either the warn line or
  the refusal line (policy-dependent) naming the mod and both declarations.

## Limits (recorded, not hidden)

- **Parity is visibility, not proof**: an undeclared binding is invisible, and an equal declaration
  does not prove equal behaviour (the same name may cover different patches). `docs/api/mod-api.md`
  §5 states both.
- A host that chooses `allow` carries the risk knowingly; a warn mismatch leaves the log line as its
  record.
- The deployment to the physical machine is deliberately not part of this ticket: per the owner's
  queue instruction the artifacts are deployed once, before the unified acceptance pass
  (`tools/deploy.ps1 -GameDir <game-dir>` then `tools/verify-deploy.ps1`), so the deployed build
  trails the code tree by design during the queue.
- A refused member is not told why: the host drops the handshake and the guest keeps its 1 s retry,
  so the host log repeats the refusal line. That is the shape of every existing refusal (the
  identity-carrying refusal report is its own future ticket), not a regression of this change — but a
  `require` host that sees a member loop is seeing this, not a new fault.

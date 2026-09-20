# Adapter capability catalog and probe aggregation

- Status: Review
- Priority: High
- Category: Architecture / game-update adaptation
- Source: Loomi architecture review (2026-09-20), item 1; degradation ruling by the user, 2026-09-20
- Related: `review/game-update-contract-toolchain.md`, `future/adapter-shell-verification-harness.md`

## Problem (evidence)

`GameAdapter.ProbeGame()` is a constant true (see `review/game-update-contract-toolchain.md`), and
`GameAdapter.Install()` is deliberately all-or-nothing: `PatchInventory.VerifyMissing` returns a
non-empty list → `UnpatchSelf()` → `Install` returns false → CUO multiplayer is unavailable. The
comment states the intent ("a game update that breaks a target must fail loud"), and that intent is
worth keeping for the parts of the game the session cannot work without.

What is missing is the middle: nothing in the tree records WHICH gameplay system a given patch,
type or member belongs to. A failure therefore surfaces as a list of patch class names, and there is
no way to say "the medical panel is unusable, everything else is fine".

## Classification rule (user ruling 2026-09-20)

The yardstick is whether the vanilla game itself has the feature:

- **Required — anything the game itself has** (items, medical, crafting, world generation, the
  session itself). If its contract is broken, CUO must refuse multiplayer as a whole, and the
  refusal must be **visible to the player** — a dialog/notice that says why — never a silent
  degradation and never "the game will not start".
- **Optional — features CUO adds on top of the vanilla game** (KrokMP-derived gameplay, pinyin
  search, and every other addition of ours). These may degrade to off by themselves while the rest
  of the session keeps working.

## Stages

1. **Catalog (behaviour-preserving).** Give every group a stable capability id, the types, members
   and patch contracts it depends on, its Required/Optional class, its install/uninstall unit, and a
   probe result carrying the failure reason. Aggregate the existing `PatchInventory` contracts, the
   dynamic patches and `ProbeGame` into one report. Installation behaviour does NOT change in this
   stage.
2. **Degradation (gated on the update actually landing).** The Required set keeps the all-or-nothing
   rule and gains the player-visible refusal. An Optional capability that fails installs as off, is
   logged with its id and reason, and leaves the session running.
3. **Install units.** Installation and uninstallation move from "the whole assembly" to the
   capability unit, so a failed optional capability can be retried or removed on its own.

## Acceptance

- Stage 1: one probe prints every capability with its class, its contract count and its failure
  reason; a test pins that installation behaviour is unchanged; the existing suites stay green.
- Stage 2: a synthetic broken Optional capability yields a playable session with that capability
  off; a synthetic broken Required capability refuses with the player-visible notice (the notice is
  a user-acceptance item — what the machine can prove is the refusal and its reason).

## What landed (stage 1, 2026-09-20)

The catalog and the probe, with installation behaviour untouched. Self-check:
`docs/evidence/selfchecks/tooling/adapter-capability-catalog-selfcheck.md`; decision 202.

- **The catalog.** `src/CasualtiesUnknownOnline.GameAdapter/Capabilities/AdapterCapabilityCatalog.cs`
  declares 14 capabilities with stable kebab-case ids, a title and a Required/Optional class:
  `session`, `world`, `items`, `traps`, `medical`, `character`, `enemies`, `crafting`, `trading`,
  `save`, `tutorial` (Required) and `mods`, `pinyin-search`, `diagnostics` (Optional). Each entry owns
  its patch classes through compile-bound `typeof(...)` references — a container type brings its nested
  patch classes — plus, where they exist, the hand-declared dynamic rows (by `PatchInventory`'s
  `"(dynamic)"` pseudo name), the game types it reads and the game members it reads outside any
  contract. The install/uninstall unit is therefore declared per capability, ready for stage 3, while
  stage 1 does not install by it.
- **The totality gate.** `tests/.../Capabilities/AdapterCapabilityCatalogTests.cs` enumerates the
  adapter assembly's `[HarmonyPatch]` classes with its own expansion and demands exactly one claim per
  class and per dynamic row, no barren owner, no duplicate or malformed id, and the pinned census
  (205 attributed + 9 dynamic = 214 rows). It caught `WorldGen/LayerModifierApplyPatch` — a patch class
  living outside the `Patches/` namespace — during this cycle.
- **The report.** `PatchInventory.VerifyMissing` now returns failure FACTS (`PatchVerificationFailure`:
  patch class, full identity, the unchanged violation text, whether the install gate counts it);
  `DynamicPatchInstaller` declares its nine reflected targets ONCE and returns its misses the same way
  (non-blocking, as today, plus an explicit "the remaining targets were not attempted" fact when its
  original flow aborts); `PatchInventory.BuildContracts` derives the dynamic contract rows from that
  same table, so a guarded row cannot describe an unbound target; and `AdapterCapabilityProbe` projects
  contracts + failures + declared members + the `ProbeGame` line into one report, printed once from
  `GameAdapter.Initialize` — one line per capability with its class, its contract count and its reasons,
  plus a session verdict.
- **Behaviour preserved.** `GameAdapter.Install` still refuses all-or-nothing — now via
  `PatchInstallLifecycle`, which owns the Harmony instance after the 600-line split — with the decision
  read through `AdapterCapabilityReport.RefusesInstall` over those same facts and called by the report's
  own verdict, so stage 2 changes a rule instead of a call site; a THROW in the install path is recorded
  as a blocking fact, so a refused install can never print an all-OK capability list; `ProbeGame`'s
  verdict and exact text are unchanged (pinned) and its four types are declared as the session
  capability's game types; the dynamic miss wording is unchanged (pinned per shape); `PatchContract`
  gained the full patch-class identity so the contract-tool parity gate compares it too. Wire, protocol
  and save are untouched.
- **Reading recorded for review.** The ruling's yardstick makes the vanilla systems (medical included)
  Required, so stage 1's report names a broken system while the session still refuses as a whole; only
  CUO's own additions (mods, pinyin search, diagnostics) are candidates for the stage-2 "off alone"
  degradation. A class serving both a vanilla path and a KrokMP-style addition
  (`PlayerCameraDragUsePatch`) is classified by the vanilla path, and splitting it belongs to stage 3.
- **Not in this stage.** Stage 2 (degradation + the player-visible refusal) and stage 3 (install
  units) remain open, as does giving the constant-true `ProbeGame` a verdict that can fail.

## Notes

The Required/Optional split is decided by the rule above, not by how hard a capability is to fix: a
feature the game has must never be silently dropped from a session.

# Adapter capability catalog and probe aggregation

- Status: In progress — stage 1's acceptance row passed 2026-10-24 on the deployed artifact (the startup probe prints all 13 capabilities with their class and contract count, its report text is the one `ProbeGame_KeepsItsVerdictAndReportText` pins, both suites are green; record `docs/evidence/acceptance/adapter-capability-catalog-20261024-a.md`). Stage 2's RULE landed on the 1.0 build (2026-11-10): a blocking hook failure refuses the session only when its owning capability is Required (a class no capability claims keeps the as-a-whole rule — a catalog gap is a bug), an Optional capability's failure installs as off and the report names it (`AdapterCapabilityCatalog.IsRequiredOwner`, `PatchInstallLifecycle.Install`, `AdapterCapabilityReport.RefusesSession`/`Degraded`; pinned by `AdapterCapabilityReportTests`, gates 834/0, suite 6640/0). Still open: stage 3's retry half (see *Stage 3* below). The player-visible refusal notice landed 2026-11-18: the Home page names the broken systems in the reader's language (reading: `.acceptance/refusal-notice/host-menu-localized.png`, `CUO 暂时无法联机：这次游戏更新破坏了医疗、伤口与远端医疗界面。游戏其它部分照常。`), and stage 3's configuration half landed the same week (a configured-off Optional unit is removed as a whole and the report says so).
- Priority: High
- Category: Architecture / game-update adaptation
- Source: Loomi architecture review (2026-09-20), item 1; degradation ruling by the user, 2026-09-20
- Related: `done/game-update-contract-toolchain.md`, `future/adapter-shell-verification-harness.md`

## Problem (evidence)

`GameAdapter.ProbeGame()` is a constant true (see `done/game-update-contract-toolchain.md`), and
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
  declares 13 capabilities with stable kebab-case ids, a title and a Required/Optional class:
  `session`, `world`, `items`, `traps`, `medical`, `character`, `enemies`, `crafting`, `trading`,
  `save`, `tutorial` (Required) and `mods`, `diagnostics` (Optional). (`pinyin-search` is a standalone
  mod, not an adapter capability: its patches live in the mod assembly, so the catalog never claims
  them.) Each entry owns
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
## Stage 2's rule (landed 2026-11-10)

- `AdapterCapabilityCatalog.IsRequiredOwner(patchClass)` — the owning capability's class decides; an unclaimed class answers true (a catalog gap keeps the as-a-whole rule).
- `PatchInstallLifecycle.Install` — the missing set is split by that rule: a Required (or unclaimed) miss unpatch and refuses; an Optional miss logs "installed with {n} optional capability hook(s) off" and the session continues.
- `AdapterCapabilityReport.RefusesSession` — Required failures plus unmapped ones refuse; `Degraded` names the Optional rows; `Render` prints "session available — {n} OPTIONAL capability(ies) installed off: …".
- Pinned by `tests/CasualtiesUnknownOnline.Tests/Capabilities/AdapterCapabilityReportTests.cs` (`Render_AnOptionalBlockingFailureDegradesWithoutRefusing` and the existing Required/refusal facts).
- Not yet: the player-visible refusal notice (stage 2's user-acceptance half) and stage 3's per-capability install units.
- The notice's machine half LANDED (2026-11-10): `IAdapterCapabilityQuery.RefusedCapabilities` carries the failing REQUIRED capabilities' player-facing titles (`PatchInstallLifecycle.RefusedCapabilities` via `AdapterCapabilityCatalog.TitleOfPatchClass`), the Home page renders `home.adapter_refused` in red above the Steam block, and the port shape pins were updated (`AdapterCapabilityPortShapeTests`: the port's members and the composition's 34). A healthy install answers empty, so the line never shows.
- The notice's player-visible half LANDED (2026-11-18) with its reading — see "The refusal notice's reading" below. The port was later renamed to `RefusedCapabilityIds` and carries ids, not titles, so the line is in the reader's language. What stays open: stage 3 (per-capability install units).

## The refusal notice (design, 2026-11-10)

What the player must see when a REQUIRED capability's hook is missing: the CUO window's own session area says, in their language, that CUO cannot start multiplayer because this game update broke a system the vanilla game has, names the systems, and says the rest of the game still runs. No dialog box, no silent BepInEx "did not load" — the mod's own window, at the menu.

- The fact: `PatchInstallLifecycle.Install`'s refusal already holds the blocking failures. It publishes a small record — the failing capabilities' titles in the player's order (session first, then the rest) plus the first failure detail for the log — as `GameAdapter.Refusal` (empty when the install succeeded).
- The seam: the bridge the plugin already holds (`IGameAdapter` through `CuoBootstrap.Services`) answers it; the Online UI's Home page renders one line when it is non-empty (`home.adapter_refused` = "CUO cannot start this session: this game update broke {0}. The rest of the game runs without multiplayer." / zh sibling), and the line's detail goes to the log with the same text the report prints.
- The machine half: a unit test formats the record from synthetic failures (Required refused → named systems; Optional only → empty), pinned beside `AdapterCapabilityReportTests`.
- The user half: a frame at the menu with a forced broken Required capability — the wording and the placement are the player-visible acceptance row the ticket names.

## The refusal notice's reading (2026-11-18)

**Read at the menu on `0.1.0+eb0289d1096e881e9e0d12c5a9b8c85cafa2778f`, with a Required capability forced broken on purpose.**

- How to put a deployed build in the state: launch with `CUO_FAULT_CAPABILITY=medical`. The log prints
  `FAULT INJECTION reports Medical treatment, wounds and the remote medical view (medical) as broken on purpose; unset
  CUO_FAULT_CAPABILITY to restore the real install.` and the install refuses with
  `Game Adapter patch verification FAILED — 1 required target(s) not applied`; the variable is inert when unset.
- The frame (`.acceptance/refusal-notice/host-menu-localized.png`, 1298x767, captured from the window handle): the red Home
  line reads, translated, "CUO cannot start multiplayer right now: this game update broke Medical treatment, wounds and the
  remote medical interface. The rest of the game still runs." — with the system names rendered from the Chinese catalogue
  keys (the reader's language), above the Steam block, with the rest of the menu working (Steam initialised, no character,
  the create-lobby, Steam-network and IP-direct buttons visible) and the build line reading `0.1.0+eb0289d`.
- The first frame of this row caught a real defect, which is why the row existed: on `0.1.0+90607a2ad` the same sentence
  embedded the catalogue's English title verbatim (`Medical treatment, wounds and the remote medical view`) inside otherwise
  Chinese wording, because the port carried titles. The fix moves ids over the port
  (`IAdapterCapabilityQuery.RefusedCapabilityIds`) and the wording into the window
  (`OnlineUiCapabilityTitles.KeyFor` → `capability.<id>.title` in both catalogues); an unclaimed hook reads as the
  unclassified-system line instead of vanishing.
- A healthy install still answers an empty list and the line never shows, and an Optional-only failure degrades without
  the line.

## Stage 3 (2026-11-18) — the install unit, first half

**Read at boot on `0.1.0+fb1d1d733ba9c721c14ad3bb544901aff1518942`, with `[Adapter] DisabledCapabilities = diagnostics`.**

- The suite now reads this report on every run by name: `capability-report` (PASS 0.2 s on `.acceptance/capability-report3`,
  2026-11-18) requires one row per catalogue id, the report's own count line, the install-and-verify line before it, and an
  available verdict with no FAILED row — so a report that claims a unit is installed while its hooks are not can no longer
  pass unnoticed.

- The install names the unit and what it removed: `Game Adapter installed with 1 capability unit(s) off by
  configuration: diagnostics`, `Game Adapter removed 3 patch method(s) of 3 class(es) from the registry:
  ItemUpdateDiagnosticPatch, PickUpItemPatch, TryPerformWorldActionsPatch`, then `Game Adapter patches installed and
  verified (294 targets).`
- The report row now reads `[Optional] diagnostics — 3 contract(s) — OFF (configuration)` and the verdict reads
  `session available — 1 OPTIONAL capability(ies) off by configuration: diagnostics`. Before the change the row read
  `OK`, which says the unit is installed while its hooks are not — the reading that started this slice.
- A Required or unknown id is still refused by the existing warning and the install proceeds normally; the unit set is
  read once at boot from the configuration.
- Residual — **retry**: a refused install still ends the process (the adapter unpatches and multiplayer is unavailable),
  so "retry a failed optional capability on its own" has no runtime path yet. Options: (A) detect an update at runtime
  and re-run the install from a re-entry point that re-verifies without a restart; (B) treat relaunch-with-the-unit-off
  as the retry, since the configuration is already the switch; (C) drop retry and narrow stage 3 to removal.
  Recommendation: C now with B's wording on this ticket; A only if an update-while-running scenario appears.

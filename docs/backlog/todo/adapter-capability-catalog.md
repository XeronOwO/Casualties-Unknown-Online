# Adapter capability catalog and probe aggregation

- Status: Todo
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

## Notes

The Required/Optional split is decided by the rule above, not by how hard a capability is to fix: a
feature the game has must never be silently dropped from a session.

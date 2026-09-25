# Remote inventory native parity — stage 1 intent path self-check (2026-09-21)

Ticket: `docs/backlog/todo/remote-inventory-native-parity-rework.md` (stage 1). Design record:
`docs/architecture/remote-inventory-native-parity.md` (amended by stage 1). Decisions: 217 (stage 0),
218 (the window's shape). Cycle scope: the game's own drag release runs on the viewer, CUO captures the
native mutation call it makes, the host validates and forwards, and the owner replays that call on the
real items — the clone-edit path and the owner-side apply path are deleted, and the wire is bumped
(`ProtocolVersion.Current` 35 → 36, the handshake unchanged as the compatibility boundary).

## What landed (anchors are our own paths plus quoted text)

| # | Mechanism | Where it lives | What it does |
|---|---|---|---|
| 1 | Release window (call bracket) | `GameAdapter/Patches/PlayerCameraDragUsePatch.cs`, "The remote display-proxy release window" | opens for a display proxy, the patch's finalizer closes it, so a projection rebuild or any other callback cannot be captured |
| 2 | Window state machine | `Runtime/Session/PlayerInteraction/RemoteDragIntentCapture.cs`, "The release-window state machine" | coalesces the native pairs, names the rest, records what it could not name |
| 3 | Mutation seams | `GameAdapter/Patches/RemoteDragMutationPatches.cs`, "The mutation seams of the remote display-proxy release window" | one prefix per native entry point; the original is skipped whenever the window took the call |
| 4 | Predicate seams | `GameAdapter/Patches/RemoteDragPredicatePatches.cs`, "The predicate seams of the remote display-proxy release window" | the branch's own guards read the body the inventory ring shows |
| 5 | Owner replay | `GameAdapter/RemoteIntentApplier.cs`, "Owner side of the native inventory intents" | resolves the item by instance id, replays the native call with R9's sequence, reports the result |
| 6 | Host half | `Runtime/Session/PlayerInteraction/PlayerRemoteInventoryIntentService.cs`, "Host half of the native remote-inventory intents" | permission, membership, ownership fact, operands, destination body, first-writer-wins, forward |
| 7 | Arbitration | `Runtime/Session/PlayerInteraction/RemoteIntentArbitration.cs`, "first-writer-wins arbitration for the native inventory intents" | one peer per item for a bounded lease, clock injected |
| 8 | Wire | `Runtime/Protocol/Messages/RemoteInventoryIntentMsg.cs` + `RemoteInventoryIntentKind.cs` | the native call identity plus operand ids, one payload on both hops |

## Findings that amended the frozen design (recorded, not silently absorbed)

| # | Finding | Evidence | Amendment |
|---|---|---|---|
| 1 | The native release branch's guards read `PlayerCamera.body` — always the local body — while the ring and the dragged proxy belong to the displayed clone | `PlayerCamera.cs:1536` (`invButton.GetItem() == this.dragItem`), `:1614` (`this.body.HoldingItem(...)`), `InvButton.cs:12` (`body => PlayerCamera.main.body`) | §3.2.6: the window answers `HoldingItem`, `GetItem`, `GetWearable`, `DoPickupCheck` from the displayed body |
| 2 | The release's first gate would silently drop every proxy release | `PlayerCamera.cs:1467` (`DoPickupCheck(dragItem, false)`), `Body.cs:1356` (ground linecast plus a 10-unit distance) | the pickup check is answered by the body that displays the proxy |
| 3 | R8's swap guard can never pass for a proxy, and R9's occupying-slot step resolves a slot of the requester's own body | `PlayerCamera.cs:1614`, `:1625` | with finding 1's answers R8 becomes reachable, and `DropItem(int)` is absorbed so the intent's owner-side execution performs that step on the owner |
| 4 | A container move and R9's own drops are internal pairs of one native gesture | `PlayerCamera.cs:1567` (`unload` + `load`), `:1623`, `:1629` | the window absorbs them into one intent per gesture |

## Decisions taken in this stage

| Decision | Statement | Where it lands |
|---|---|---|
| Window shape | a call bracket around exactly one `HandleReleaseDragging` invocation, closed by the finalizer, with the display-body predicate answers | design §3.2, decision 218 |
| Vocabulary extent | the eight members the release branch can produce now; the rest are refused with one logged line until the stage that restores their branch | design §3.3 |
| Custody and limb intents | `TransferToBody` keeps the landed cross-player custody transaction (the owner's half is a real release, the destination's is R9's own sequence) and `ApplyToLimb` keeps the landed cross-player use path | ticket §Stage 1, design §6 |
| Host duty | validate, arbitrate first-writer-wins per item, forward; never model inventory contents | design §3.1 |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings, 0 errors.
- New tests: `RemoteDragIntentCaptureTests` (22 cases after the review round: the coalescing rules,
  the two-intent take-out-then-move order, the R9 absorption, the one-line-per-gesture batch refusal,
  the slot-0/limb-0 operands, the classified no-ops, the redirect decision, the reset between
  releases), `RemoteIntentArbitrationTests` (6 cases: first-writer-wins, the same-requester refresh,
  lease expiry, the table not growing), `RemoteIntentSlotReleaseTests` (8 cases: R9's step order and
  the slot bound that guards `Body.slots[slot]`), `RemoteInventoryIntentWireTests` (9 cases: the
  `value + 1` encoding round-trips 0 and -1 for both operands), `RemoteIntentRequestTests` (12 cases:
  host-owner delivery, guest-owner forward, unknown item, slot outside the inventory, foreign
  destination body, container move into itself, a hand-slot swap, the forwarded container take-out and
  worn drop, and both custody directions with the named slot).
- Updated: `RemoteUseOnSelfTests` (the intent kind), `LineOfSightRefusalTests`,
  `InteractionGateAuthorityTests`, both direction tables, `OnlineUiMemberProjectionTests`' fake
  control, `RemoteBackpackContractTests` (the seam's reflected surface).
- Final run after the review round: `dotnet build` 0 warnings / 0 errors, normative gates 149/149,
  and the full suite 3855/3855 **with build** (`dotnet test CasualtiesUnknownOnline.slnx`).
- Test-expectation red: **none obtainable for this rework**, and the review round confirmed why the
  green suite was not sufficient evidence — two of its three blockers (the wire's 0-omission and the
  redirect feeding CUO's report hooks) were invisible to every existing test by construction.
  Their regressions are now pinned by the suites above. The reported defects are removed by
  deleting the mechanism that caused them (the host mirror edit), not by patching it; a test written
  against the deleted type would be a compile error, which the workflow does not accept as a red. The
  replacement behaviour is covered by the new suites above, and the reported rows stay open for the
  user's acceptance run.

## Independent adversarial review of this stage (same commit)

A fresh-context reviewer audited the frozen working tree against the design record, the game's own
code and the test suites. Report: `%TEMP%\cuo-review-remote-inventory-stage1.md`. Verdict: the
architecture and the seam hold (the window state machine, the host half and the arbitration passed
claim by claim; the clone-edit path is deleted with no leftovers in `src/` or `tests/`; all 18 new
patch classes are catalogued), with **3 blockers / 7 majors / 10 minors**, all fixed in this same
commit:

| Finding | Fix |
|---|---|
| B1 `TargetSlotIndex`/`TargetLimbIndex` were 0-omitted on the wire, and 0 is a hand | both ride as `value + 1` with a backing field (the `PlayerHealRequestMsg.LimbSelection` pattern), plus wire round-trip tests for 0 and -1 |
| B2 the display-body redirect let CUO's report hooks report a display proxy (fresh id, phantom spawn/carried fact) | the reporting hooks skip a display proxy or a call the window took (`BodyItemPatches` pickup/drop/swap, `PickupSync.OnPickedUp`, `ItemSlotSync.OnSlotMoved`); the rule is recorded in the design's §6 |
| B3 W1 + W4 in one release lost the take-out | the window keeps an ordered per-container pending list, so a take-out from one container and a move into another stay two intents in native order (two new cases) |
| M1 the drain and the radial use/wear were silent, and the radial fall-through produced wrong intents | the radial release is consumed and reported while a proxy is dragged; the drain reports once per dragged proxy |
| M2/M7 R7 and R14 were reported as unknown gestures, and the R1 pre-scan missed the native `Overlaps` gate | the outcome carries a classified-no-op reason (R1/R7/R14) computed with the same `Overlaps` filter the native code uses |
| M3 an unresolvable proxy ran the whole native release against the proxy (fail-open regression) | the drag is cancelled before the native body, restoring the rule the deleted release-cancel policy held |
| M4 the rewrite orphaned the cross-player use-by-drag route | the window patch keeps calling `TryHandleDraggedItemUseOnRemote` for a local item |
| M5 design §3.6 contradicted itself and carried a non-reproducible line count | corrected (269 lines, measured), and the two pages naming the deleted class are re-pointed |
| M6 the owner-side executor and the patch layer had no coverage | the R9 step plan and the redirect decision are pure runtime types with their own suites (`RemoteIntentSlotReleaseTests`, `RemoteDragIntentCaptureTests`), and the host half gained the container/slot/wearable cases |
| minors 1-10 | R5 refusals collapse to one line per gesture; the dead lease constant, the direct event raise, the unreachable swap guard, the missing self-recursion guard, the discarded refusals and the mis-placed enum comment are all fixed |

**A coverage-driven note on the reported numbers**: the first full-suite run in this cycle happened
before the review and is superseded by the post-fix run recorded below. The reviewer's own environment
had no shell for most of its work, so its build/test observations are static — the numbers below come
from this side.

### Finding left open, with evidence (out of scope for this stage)

`SourceShapeGateTests.Architecture_OneTopLevelTypePerFileAndAggregateLimits` counts top-level types
with a regex that does not accept the `readonly` modifier
(`^(public|internal|sealed|static|abstract|partial)*(class|struct|interface|enum|record)\s+(\w+)`), so
`internal readonly record struct …` — and `internal sealed record …` used elsewhere in the repository
— are invisible to it. This stage does not depend on the hole (the two record types it introduced live
in their own files), but the gate's declaration does not equal its reach, which `tests/AGENTS.md`
forbids. It is recorded in `docs/backlog/todo/source-shape-gate-modifier-blindness.md` for the next
cycle rather than widened here, because re-baselining that census would change a pin this change does
not own.

## Limits

- No runtime evidence: no two-client session, no frame-level verification of animation, sound or
  input interception. The operator's-screen feel and the reported rows remain the user's
  release-cycle acceptance.
- Stage 1 refuses, observably, the gestures whose intents arrive in stages 2-4 (container expansion,
  drain, use/wear/combine/battery/favourite, trader). Those branches are still suppressed, or their
  calls are refused before they can touch a proxy.
- The host's arbitration lease is time-bounded (2 s) and the same requester may send its next gesture
  immediately; the owner's native guards are the serialization point for one client's successive
  calls.

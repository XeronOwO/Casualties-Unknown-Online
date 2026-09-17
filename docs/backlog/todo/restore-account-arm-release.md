# The restore account's entity and native arms are released without a contribution

- Status: Todo
- Priority: Low
- Category: Persistence / save system (the restore account)
- Source: recorded by the restore-ATTEMPT-identity cycle of
  `review/save-mid-run-consistent-cut.md` (2026-09-12) and re-verified against the tree on 2026-09-17,
  when that ticket moved to `review/`
- Related: `review/save-mid-run-consistent-cut.md` (the pass that recorded them), decisions 172/179/182
  in `docs/decisions/active.md`, `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldRestoreAudit.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntityKernelProjection.cs`

## The gap

A restore opens an account that owes a fixed number of "halves" (`WorldRestoreApplier.LiveWorldHalves` —
three on a mid-run restore: the world facts, the world-entity facts, then the item reconcile). The
session end releases the arms that never ran, but only ONE of the three releases contributes to the
account:

- **the item arm reports** — `ItemService.ResetSessionState` → `CancelRestoredWorldItems` →
  `RestoredWorldItemSet.Cancel` → `WorldRestoreAudit.LiveWriteAbandoned`;
- **the world-entity arm does not** — `WorldEntityKernelProjection.OnSessionEnded` calls
  `CancelPendingRestore`, which logs a warning naming the dropped counts and clears the arm, but never
  reaches the audit;
- **the native arm does not either** — `GameAdapterSessionBinding.OnSessionEnded` calls
  `domains.NativeWorldFacts.CancelPendingRestore()`, which likewise logs and clears only.

An account that owed the world-entity and native halves therefore stays awaiting: the loss is named in
`CUO.log` but the restore's player-visible report never completes. PRE-EXISTING — found by the
adversarial review of the attempt-identity change, not caused by it, and no worse with it (the next
restore's `BeginRestore` reopens the account). Closing it needs the audit reachable from the projection,
or the release routed through the save layer, which already has it.

## Recorded design positions (not work)

### A mismatch is silent once the account is closed

The identity warning only fires while an account is OPEN, because a closed or abandoned account ignores
every contribution by design, so a late writer after a completed restore is not named. ACCEPTED as a
design position — the rule that a straggler must not invent a report for a restore that has already
reported is the stronger one — and recorded so a later cycle does not read the silence as a defect.

### The supersession releases the arms only when a restore actually applies

The release sits after the two content refusals (an unreadable or absent archive) and before the kernel
restore, so a refused Continue leaves whatever the previous attempt armed in place; the next applying
attempt releases it, and the pre-existing `AbandonRestore` paths cover a new run. Not a regression (a
refusal never armed anything new). The invariant is exactly: "a new APPLIED restore supersedes the
previous attempt".

## Re-verified: the item release IS pinned (the recorded residual no longer holds)

The cycle recorded "the item release has no test of its own — it is verified by reading only". That is
no longer true: `RestoredWorldItemContractTests.ACancelledReconcile_ReportsTheLossInsteadOfWaitingForever`
takes the REAL `ItemService` and `WorldRestoreAudit` from `ItemSimWorld`, opens an account owing two
halves, completes only the world-fact half, calls `CancelRestoredWorldItems` with the same reason string
`ItemService.ResetSessionState` uses, and asserts the report is incomplete and names the cancelled
reconcile; `ACancelWithoutARestore_IsANoOp` pins the no-op half.

What is still unpinned is the PORT WIRING around that method: `WorldSaveService`'s and
`WorldRestoreApplier`'s `CancelRestoredWorldItems` call sites (supersession, layer-end cut,
abandon-on-new-run) and the session-end subscription, because the save fixture never assembles an
`IItemControl` (no test double for that interface exists — the suites resolve the real `ItemService`
through the composition root instead).

## Scope

1. Give the world-entity and native session-end cancels the same account contribution the item cancel
   has — or route the release through the save layer, which already owns the audit.
2. Pin the item release's PORT WIRING (the `CancelRestoredWorldItems` call sites and the session-end
   subscription) the way the release METHOD is already pinned.
3. The two recorded design positions above are not work; a cycle that reopens one states why.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | The session ends while a restore still owes the world-entity and native halves | The account is completed or abandoned WITH a report; it does not stay awaiting forever |
| 2 | A restore is superseded, or a layer-end cut replaces the armed item half | The port wiring is pinned by a test rather than by reading (the release METHOD is already covered by `RestoredWorldItemContractTests`) |

## Verification limits

The session-end path runs through the adapter's session binding, so the release's routing is
machine-checkable at the Runtime seam while the adapter wiring is read-only reviewed; the in-game effect
(a session ending in the middle of a restore) belongs to the user's unified acceptance pass.

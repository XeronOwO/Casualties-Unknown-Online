# Acceptance record — the give gesture's centre wear, re-read on the placement fix

- Ticket: `give-gesture-own-ui-and-center-use` — verdict: **stays in `todo/`**. Row 4 now passes; the ticket
  remains open for row 7 only (a view-only display needs a host rule no run can set), which is undeveloped
  tooling work rather than an acceptance question.
- Batch: `20261023-g` — the single row `20261023-f` rejected: row 4, "centre wear of a wearable: the
  displayed player wears it, and the giver's body does not".
- Commit: `b232be74` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `ProductVersion`
  `0.1.0+b232be7428d2d68dff6d798ae1e45a2de5c20524` — `tools/verify-deploy.ps1` exit 0 before the clients
  started, and both clients were launched after the deploy.
- Run: 2026-10-23 22:55 → 23:00, this session's clock · Host: physical machine (the GIVER) · Guest: first
  sandbox (the displayed player); one world, one session.
- Dependencies: `preflight.ps1`, `session-environment.ps1 -Mode status`, the in-process evaluator on both
  endpoints, the physical pointer and keyboard under the standing 2026-10-22 authorisation.
- Artifacts: `20261023-g-host-wear.txt`, `20261023-g-guest-wear.txt`, `20261023-g-guest-worn.json`,
  `20261023-g-host-worn.json`, `20261023-g-marks.txt`, `20261023-g-sample{1,2}.json` in the directory named
  by `acceptance-artifacts-dir`; players are written `<host>`/`<member>`.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 4 | Centre wear of a wearable: the displayed player wears it, and the giver's body does not | machine | **pass** | The centre release of a created `bikehelmet` (instance `1168552085443`) was applied instead of refused: `<host>` wrote `[ItemUse] <host> wears bikehelmet (id 1168552085443) on <member>; slot -2.` and `[ItemUse] local item 1168552085443 destroyed by cross-player use.` at 22:57:10.9, and `<member>`'s own client wrote `[ItemUse] local body wears bikehelmet from <host>.` (`20261023-g-host-wear.txt`, `20261023-g-guest-wear.txt`). The target's own worn set carries it on limb 0 (`20261023-g-guest-worn.json`: `wornCount:1`, `bikehelmet`, `wearable:true`) while the giver wears nothing (`20261023-g-host-worn.json`: `wornCount:0`). On the artifact batch `20261023-f` used, the same gesture was refused before the placement ran — that record's row-4 evidence is the red. |

## What changed between the two reads

`GameWearPlacement.TryResolve` now hands the index it resolved out of its `limbIndex` parameter
(`return TryResolveLimbIndex(body, info.desiredWearLimb, out limbIndex);`); the committed fix is `b232be74`,
with the syntax gate `WearChainContentGateTests.ThePlacementResolver_HandsOutTheLimbIndexItResolved` and its
matcher samples. The refusal batch `20261023-f` read was the Runtime application's first guard answering on
the `-1` sentinel.

## Residuals for the user

None.

## Limits

- **One wearable, one limb.** `bikehelmet` resolves to limb 0 by its own `desiredWearLimb`; other garments and
  other limbs are not read here, and the slot-conflict refusal (`RemoteWearApplication.HasSlotConflict`) was
  not staged.
- **The setup is the same declared substitution as batch `20261023-f`**: the item was created with
  `item-provide mode=create`, the member was positioned with `body-place`, and the gesture itself — ring,
  right-press, Tab, range entry, centre release — was the real pointer's.
- **The row was driven once.** A second wear on the same body would need a second garment in a free slot; the
  gate on the resolver covers the shape, and this read covers the machine path.

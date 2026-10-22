# Batch 20261022-h — give-item-to-another-player rows 3, 4 and 6

Batch `20261022-h`, the same run as `placement-outcome-never-returns-20261022-h.md` (one batch, one deployed
artifact `0.1.0+fb782e5c…`, the host as the giver and the first sandbox as the receiver). The scope and fixtures
are `20261022-h-scope.md`.

| # | Verdict | Evidence |
|---|---|---|
| 3 | PASS | The refused give: release of the `toolbox` on the receiver's `plasticbag` (a container whose own rules refuse it). The giver saw one line naming the container's reason (`[RemoteIntent] refused GiveToBody: toolbox does not fit container plasticbag by the container's own rules.`), the item stayed on the giver's body, and no transfer or row was written on either side |
| 4 | PASS | The substituted destination: release on the receiver's occupied slot 1. The receiver answered `slot 2`, the host recorded the ANSWERED slot (its snapshot for the receiver: `1254451431363:bandage@2`), and the receiver's own client placed it there (`placed bandage (id 1254451431363) in local slot 2`) — host row and receiver tree agree |
| 6 | UNPROVEN | The residual: a receiver body that changes between its placement answer and the transfer ends with the item back with the giver. The window is one host round trip (the run's row 3 measured answer 23:49:53.812 → apply 23:49:54.353), the batch never changed the receiver's body inside it, and the review of this batch's artifact (`fb782e5c`) found the failure branch could not run at all at that commit (the exact flag was dropped at the kernel projection and the adapter never received the resolver). The machine row is a named residual, not a pass |

## Limits

- The rows' fixtures and the bodies' placement are the declared setup substitutions of the scope; the gestures
  were the real pointer's.
- Rows 1, 2, 5, 7, 8, 9 and 10 of this ticket were read by batches `20261022-f` and `20261022-g` and were not
  re-run here.
- Row 6 is not judgeable by hand in this design; its reading would need a staging seam, which the acceptance
  rules refuse as evidence of player reachability.

## Addendum (2026-10-23)

The user ruled that row 6's reading is the behaviour test plus the wired return chain (see
`placement-outcome-never-returns-20261023-ruling.md`); the `UNPROVEN` verdict above stays as this batch's machine
reading.

# Acceptance — the drink measurement never runs a non-drink definition

- Ticket: `docs/backlog/done/drink-measurement-destructive-use-action.md`
- Artifact: `0.1.0+6ee2f683` (the fix commit; the docs commits above it do not change the build)
- Batch: `.acceptance/20261024-c/` (local, gitignored)
- Date: 2026-10-24
- Result: **pass**, with the two declared substitutions below.

## What was run

Three clients in one world (host on the physical machine, the Steam1 member and the Steam2 bystander in their
boxes), the prelude's own three-client session. The operator was the Steam1 member.

1. The operator's `bloodbag` was placed by the committed `item-provide` recipe (`type=bloodbag mode=create`).
2. The refill is a **declared setup substitution**: the native liquid transfer (drain a bag, refill it from
   another container) was replaced by an in-process probe calling the game's own
   `WaterContainerItem.DrainAll()` + `AddLiquid(liquid, amount)` — water 500 first, then alcohol 200.
3. The release is a **declared substitution for the pointer motion**: the host's displayed backpack was opened
   through the production `RemoteBackpackCoordinator.Open` (the player card's own call; `opened=true`), and the
   release branch then ran through `CrossPlayerDragUse.TryUseOnDisplayed` — the exact method
   `PlayerCameraDragUsePatch` calls when a release lands on the displayed backpack's centre (the user's
   2026-10-23 ruling). No OS-level drag was injected.

## Rows

| Run | Operator's bag | Operator's blood | Target's body (host) | Operator's render clones | Log |
|---|---|---|---|---|---|
| water 500 | `500 → 500`, water `500`, blood liquid `0` | `100 → 100` | `100`, no use line | three bodies, all `100` | `[DragUse] refused: bloodbag could not measure a drink dose for <host>` and `[ItemUse] refused drink use: bloodbag's own use action does not drink, so it was not run on this client.` |
| alcohol 200 | `200 → 200`, alcohol `200`, blood liquid `0` | `100 → 100` | `100`, no use line | three bodies, all `100` | the same two lines |

`used=true` in both runs means the release was consumed (the caller's contract); the refusal is the named
measurement refusal, not a silent drop.

## What this does and does not prove

- **Does**: the deployed build refuses a water-refilled and a health-usable-refilled blood bag at the
  production release branch, by name, with nothing written — the operator's bag, the operator's body, the
  target's body and the operator's clones are all unchanged.
- **Does not**: an OS-level mouse drag (the release branch was called directly), the native drain/refill
  sequence (the refill was written through the game's own container API), and a RED run on the pre-fix artifact —
  the defect's witness is the decompiled `Item.DrawBlood` write and the self-check's recorded limit, which the
  ticket states honestly.

## Classifier half (same artifact, same batch)

One client in a world, the deployed `GameDrinkActionFacts` asked over the real registry: `waterbottle` and
`naltrexone` → `Drinks`; `bloodbag`, `bloodbaghuman`, `liquidcentrifuge` → `NotADrink`; the summary over all
139 usable definitions: **33 `Drinks`, 106 `NotADrink`, 0 `Unreadable`** — the independent review's decompiled
count reproduced on the running client.

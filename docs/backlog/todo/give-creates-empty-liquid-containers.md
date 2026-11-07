# /give creates liquid containers with no condition and no contents

- Status: Todo
- Priority: Medium
- Category: Console / item domain
- Related: `docs/backlog/done/mod-cross-player-native-semantics.md` (found while judging its topical row),
  batch `20261107-o`
- Source: batch `20261107-o` (2026-11-07): `/give @p paincream` on the host produced an item whose own
  `condition` reads 0 and whose liquid contents are empty, while `item-provide mode=create` (the game's own
  `Utils.Create` path) produces `condition=1` with the default contents.

## The reading

```
use-eligibility mode=one type=paincream → found=true, condition=0, isUseItem=false
item-provide mode=create type=paincream → condition=1
[ItemDropped] paincream (id …) … container contents 0
```

The guest that materialized the given item read it as `condition 1.00` — the host's own copy is the broken one,
so the two clients describe the same item differently.

## Why it matters

A player who uses the console `/give` receives a container the game's own wound view refuses (`condition=0`,
empty), so the item looks broken; and the guest's copy disagrees with the host's about the same item, which is
exactly the class of divergence the item domain's fact table exists to prevent.

## Design

The item family table (`docs/design/items/README.md`) and the topical family page (`docs/design/items/topical.md`):
a container is the same item on every client, and the game's own creation path is the only template a console
grant may copy — the defect is that the grant path does not reproduce the template's condition and contents.

## What has to be found

Where `/give` builds its item: the console command's grant path versus `Utils.Create` — whether the grant path
clears the template's condition and contents, or never copies them.

## Acceptance

| Row | The check |
|---|---|
| 1 | `/give @p paincream` on the host, then `use-eligibility mode=one` reads `condition=1` and the wound view accepts the item. |
| 2 | The host's own copy and the guest's materialized copy agree on condition and contents (`[StandingItem] materialized … condition` line + `body-items`). |

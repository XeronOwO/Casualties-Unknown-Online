# A display proxy reports its own spawn as a world item

- Status: Done — the spawn report refuses display proxies on `0.1.0+1920fd4c`; the storm-shaped session
  produced no report. Evidence: `docs/evidence/acceptance/display-proxy-spawn-storm-20261107-p.md`.
- Priority: High — the loop made the game unplayable in about three minutes (user report, `m15419`).
- Opened: 2026-11-07, from batch 20261107-o's storm.

## What happened

The host materialized a trashbag every ~3.3 s (215 lines in about three minutes), each preceded by an id
watermark bump from the guest, while the guest logged `[ItemWorldSync] [ItemSpawned] local trashbag (id …)
reported at (4.8,498.3), fresh False.` RTT reached 2781.4 ms (avg 3287.0), the snapshot stream 40-44 KB
per frame, custody reported 224 disagreements, and 125 world items stood within 45 units of `(0,500)`.

## The chain

- The clone renderer stamps a display clone: `CloneInventoryRenderer.cs:258-259`
  (`AddComponent<RemoteCloneRender>()`, `SetRemoteInventoryItemId(...)`).
- The clone's `Item.Start` → `ItemWorldSync.OnItemInstantiated` (`ItemWorldSync.cs:219`); the guard at
  `:221` let a world-positioned proxy pass (`IsWorldItem` `:141-142`, `IsStandaloneWorldItem` `:182`).
- `:259` allocated an id and `:348-361` reported the spawn; the host materialized it, broadcast it, and the
  guest rendered another clone whose `Start` reported again.

The classifier family already asked `IsDisplayProxy` everywhere else (`ItemWorldSync.cs:382`,
`ItemReconcile.cs:144`, `LocalItemAccess.cs:162`, `RemoteItemSceneOps.cs:100-255`,
`GeneratedItemReconcile.cs:112`); the adopt-target gate's own comment named the spawn path as the one
uncovered site.

## The fix

`OnItemInstantiated` returns early when `IsDisplayProxy(item)` (`ItemWorldSync.cs:228-231`), before the id
allocation. The adopt-target gate now covers the spawn classifier (fifth in the family, census floor 5).
Commit `fa86b7ab`; gates 815 passed; suite 6598 passed; format clean.

## Acceptance rows

| Row | The check | Reading |
|---|---|---|
| 1 | A display proxy never reports a spawn | Guest `[ItemWorldSync] [ItemSpawned] local` lines in the session: 0, while `[CarriedSync] added trashbag (id 1297401104323) … re-rendering the clone` shows the proxy existed |
| 2 | The world's items stay put | The two trashbags materialized once each (18:16:15.826, 18:17:07.830) with no repeats |
| 3 | The custody projection agrees | Host `rows=302 objects=302 mismatches=0`; guest `rows=306 objects=306 mismatches=0` |

## Limits

The guard's own line is Debug and the client log runs at Info, so row 1 is read from the absence of the
report line beside the `CarriedSync` proof that the proxy existed. The 17:46 session is not re-created field
by field; a future storm is checked with the same two readings.

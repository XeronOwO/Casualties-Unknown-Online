# Display proxies must not report themselves as world spawns (20261107-p)

Batch on `0.1.0+1920fd4c` — `verify-deploy`: "Deployment matches this tree's build output". Three clients
(host 18590, Steam1 18591, Steam2 18592), lobby `109775243742423375`, all in world at 18:14:13.

## The defect (measured 20261107-o)

From 17:46 the host materialized a trashbag every ~3.3 s: 215 `[ItemSpawn] materializing trashbag` lines
in about three minutes, each preceded by `[ItemIdWatermarkHandler] Item id watermark N from
76561198863287957`. The guest logged `[ItemWorldSync] [ItemSpawned] local trashbag (id …) reported at
(4.8,498.3), fresh False.` for every one. The host's RTT reached `2781.4ms avg=3287.0ms`, its
`ItemSnapshotStream` 40-44 KB per frame, and its custody projection read `rows=577 objects=797
mismatches=224 — … :KindDisagrees:World:clone-render`. 125 world items stood within 45 units of `(0,500)`.
The user reported the game unplayable, "the world is all trashbags" (`m15419`).

## The chain

- `CloneInventoryRenderer.cs:258-259` stamps a display clone: `obj.AddComponent<RemoteCloneRender>()` and
  `SetRemoteInventoryItemId(...)`.
- The clone's `Item.Start` reaches `ItemWorldSync.OnItemInstantiated` (`ItemWorldSync.cs:219`). The guard at
  `:221` was `IsReplayedFact || HarmonyTraverse.IsGenerating() || !IsStandaloneWorldItem(item)`; a
  world-positioned proxy passes all three (`IsWorldItem` at `:141-142`, `IsStandaloneWorldItem` at `:182`).
- `:259` `_ids.Allocate(item)` stamps an id; `:348-361` sends the spawn. The host materializes it and
  broadcasts; the guest renders another display clone whose `Start` reports again — the loop.

## The fix

`OnItemInstantiated` now returns early when `IsDisplayProxy(item)` (`ItemWorldSync.cs:228-231`), before the
id allocation, with a debug line `[ItemSpawn] {Type} (id {ItemId}) is a remote clone display proxy — spawn
not reported.` The adopt-target gate covers the spawn classifier as the fifth in the family
(`AdoptTargetGateTests.cs:234`, census floor 5 at `:81`) and its red run failed on the missing
`IsDisplayProxy` test before the guard landed. Commit `fa86b7ab`; gates 815 passed; suite 6598 passed;
format clean.

## The reading

- 18:14:22.349 the guest rendered the host's carried trashbag as a display proxy: `[CarriedSync] added
  trashbag (id 1297401104323) to 76561198281246659's snapshot — re-rendering the clone.` The shape the storm
  needed was present.
- Guest `[ItemWorldSync] [ItemSpawned] local` lines in the whole session: **0**.
- The two trashbag world items materialized once each: 18:16:15.826 `id 1297401104323 at (1.0,498.1)`
  (the native drop) and 18:17:07.830 `id 1280221235139 at (4.8,497.0)` (the host's death drop), with no
  repeats afterwards.
- `[ItemCustody] rows=302 objects=302 mismatches=0` on the host; the guest's last projection line read
  `rows=306 objects=306 mismatches=0` (18:14:16.988).
- The storm's loop (215 spawns in about three minutes) did not reproduce across the session.

## Limits

The guard's own line is Debug and the client log runs at Info, so the positive marker is the absence of the
report line while the display proxy demonstrably existed (the `CarriedSync` line above). The 17:46 session
is not re-created field by field.

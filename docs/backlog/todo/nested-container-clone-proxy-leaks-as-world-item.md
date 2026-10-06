# A nested container's clone proxy leaks as a world item

- Status: Todo
- Priority: Medium
- Category: Item sync / remote presentation (the clone renderer's content materialization)
- Source: read from the source on 2026-10-06 while attributing
  `done/second-drop-report-loses-its-world-object.md` (that cycle's sweep over the display-proxy guards in
  the "id-less world item" family). **Not observed at runtime** — what follows is a code path with its native
  preconditions, so the first job of the cycle that takes this ticket is to confirm it with the fixture named
  below before changing anything. The user-visible symptom it would reproduce is the one the closed report
  `done/guest-container-contents-ghost-drops-on-host.md` chased ("a carried container's content periodically
  appears as a world drop on the other side"); that fix removed the periodic rebuild, not this path.
- Related: `done/guest-container-contents-ghost-drops-on-host.md` (the user report, and the ticket that
  named the display-proxy skips as the live defence), `done/second-drop-report-loses-its-world-object.md`
  (the same family's adopt-path half, fixed 2026-10-06), `review/runtime-entity-markerless-bind-absorption.md`

## The reading (source, not yet observed)

`CloneInventoryRenderer.RestoreRemoteContent` materializes one content proxy as a ROOT object and then asks
the container to take it:

```csharp
var go = Object.Instantiate(prefab, containerItem.transform.position, Quaternion.identity);
...
container.LoadItem(child);
```

The game's `Container.LoadItem` refuses in four ways (`Container.cs:116-151`). The one that matters here is
`this.mItem.TryGetParentContainer(out container2)` — "the container being loaded INTO is itself inside
another container" (`Item.TryGetParentContainer` is `transform.parent` carrying a `Container`,
`Item.cs:133-142`). On a viewer's clone that is exactly the shape the renderer's own branch above exists to
render: a nested container with contents is attached MANUALLY (it must bypass the stacking guard), and from
then on every content proxy of that inner container is loaded into a container whose parent is a container —
so `LoadItem` returns without parenting anything.

Three consequences follow from the refused call, all of them in this repository's own code:

- **The proxy stays a root object at the container's position.** `SetRemoteInventoryItemId` ran before the
  load, so it carries the display-domain marker WHENEVER the authoritative instance id is non-zero — that
  writer returns early for id 0 (`CloneInventoryRenderer.SetRemoteInventoryItemId`), so an id-0 content proxy
  is left with NO marker at all. Either way `MarkRemoteCloneTree` walks the container's tree and the orphan is
  not in it, so it carries no `RemoteCloneRender`.
- **It therefore enters the item domain at its own `Item.Start`** (`ItemPatches.ItemStartPatch` postfix →
  `ItemWorldSync.OnItemInstantiated`): not a replayed fact, not generating, `IsStandaloneWorldItem` TRUE for a
  root object, no `ItemInstanceId` — so it takes the runtime-spawn path and is reported:
  `[ItemSpawned] local <type> (id <new>) reported at …`. That is an authority row for an object the clone
  renderer owns, materialized on every peer.
- **Nothing reclaims it.** `RestoreRemoteContents`'s removal pass collects `previous` from the container's
  DIRECT children carrying `RemoteCloneRender`, so a root orphan is invisible to it: the next re-render (the
  1 Hz character snapshot) materializes another one at the same spot, and each one that reaches `Start` is
  reported in turn. `LoadItem`'s refusal also fires
  `PlayerCamera.main.DoAlert(Locale.GetOther("alertcontainerstackingalt"))` on the viewer, so the same shape
  shows a container-stacking alert.
- The same refusal is NOT reachable for the ordinary shape (a top-level carried bag's own contents): that
  bag's parent is an inventory slot, not a container, which is why the predecessor ticket's trashbag/dog-food
  report was a different path.

## Reachability

The precondition is a REMOTE player carrying a container that itself holds a container WITH contents. The
producer ticket's own reading of the native loading path (`done/drop-pending-single-slot-overwrite.md`,
point 4a) says a carried container cannot acquire such a child through `LoadItem` at all — route (a) refuses a
non-empty container child and route (b) refuses to fill a nested one — and it names the two sources of that
state: a world-generated container that holds a container with contents, and a declared scene fixture. Neither
was staged in any batch read so far. Whether a restore can also produce it is NOT claimed here: the restore
re-materializes through `Body.PickUpItem`/`Container.LoadItem`, i.e. through the same two refusals, and this
reading has not followed that path.

## Acceptance

- **Confirm first, at runtime, with a fixture that stages the shape**: three clients, the owner carrying a
  container that holds a container with contents (stage the inner container's contents before nesting it, or
  use a world-generated pair), then read (a) the owner's own tree, (b) a viewer's
  `container-read mode=clone` — the orphan is a proxy with `underClone=false`, and it carries a non-zero marker
  id only when the content's authoritative id was non-zero (an id-0 content proxy is unmarked, which is why
  the log row below is the reading that matters), and (c) the host log for `[ItemSpawned] local <inner type>`
  lines that the owner never dropped. The phantom row in the host's authoritative tables is what makes this a
  defect rather than presentation; read (c) is the one that decides it either way.
- **Then fix it at the door that is wrong**, not at the symptom: either the renderer attaches a proxy it
  cannot load the way it already attaches a nested container with contents (manual attach, no `LoadItem`), or
  the item domain refuses an object that carries the display-domain marker at `Start` — the choice belongs
  with whichever half the reading says owns the invariant, and either way a proxy must never be reported as a
  local creation.
- **Family**: the sweep that fixed the sibling ticket covered the four classifiers that take authority over an
  "id-less standalone world item" (`AdoptTargetGateTests`); this path's entry is `ItemWorldSync.OnItemInstantiated`,
  which is the fifth, and the fix's regression case belongs beside that gate.

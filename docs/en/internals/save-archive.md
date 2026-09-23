# How a world is saved

[Documentation](../README.md) > [Internals](README.md) > How a world is saved

---

**After this page** you can say what a CUO save writes, why a crash cannot leave a half-written world,
and what happens when a snapshot is refused. [Save mod data across
sessions](../how-to/save-mod-data-across-sessions.md) is the mod-facing half; this page is the archive.

## One folder per world

CUO keeps its own [world archive](../reference/glossary.md) and never reads or writes the game's own
`save.sv`: the native format cannot express a save taken in the middle of a run, so keeping both in step
would only create two sources of truth. A world is one folder under the game's persistent-data root —
`cuo/saves/` — holding the current state plus a set of compressed backups.

- `index.json` lists the worlds; a folder per world holds `world.json` and an unpacked `live/`
  directory.
- `live/` stays unpacked on purpose: a save is a fast file write, and a human can read or repair a world
  without unpacking anything.
- `backups/` holds `.cuoz` archives — the same file set, one snapshot per archive.
- `worldId` is immutable and names the folder; the display name is free to change.
- The **host is the only writer**. A guest stores local settings and nothing else, so a second
  divergent copy of the world never appears on somebody's disk.

## What one cut contains

`live/` carries a `manifest.json` and one file per domain — `run.json`, `players.json`, `items.json`,
`world-entities.json`, `enemies.json`, `fluids.json`, `world-blocks.json`, `world-transients.json` —
plus `characters/<playerKey>.json` per player. A player key is transport-scoped, `steam-<steamId64>` or
`name-<display name>`, so a Steam world is never silently claimed by a name collision in an IP-direct
session.

The DTOs are the same shapes a late-joining guest receives, so a restored host holds exactly what a
join would have given it. A new domain adds a file inside the archive; it never rewrites an existing
one. And every payload file is a JSON array of entries — the decoder is handed one entry at a time,
which is the seam the per-entry salvage below runs on.

## When a cut may be taken

A save is only useful if it is **consistent**: it must not interleave with a command batch or a frame
flush. So the capture runs at one of exactly two seams on the host's main-thread pump, and
`manifest.json` records which one, so a restore can prove what it holds:

| `cutPhase` | Taken when | Why there |
|---|---|---|
| `layer-boundary` | the kernel commits a layer advance | the cut holds the run baseline of the layer being entered — taken earlier it would store the previous layer's baseline and regenerate a different world |
| `frame-end` | the CUO pump's last step | the one point where no command batch is mid-commit and no frame flush is mid-send |

The `/save` command and a deliberate return to the main menu do not cut where they happen. They **arm**
a cut, and the frame-end seam takes it — a command runs inside the game's input handling, where a
snapshot could read a half-applied frame. The cut waits for in-flight state that has not reached the
kernel yet, and if it still cannot be taken after `WorldCutDeferral.MaxFrames` (eight pump frames) the
state that blocked it is named in the cut report instead of starving the request.

## A save is a transaction

```text
stage:   write the world's .staging/ folder (manifest last)
verify:  re-read every file and compare against the manifest's checksums
backup:  zip .staging/ to backups/<kind>-<stamp>.cuoz.tmp, then rename to .cuoz
commit:  rename live/ aside, rename .staging/ into place, then refresh index.json
```

`live/` is replaced only after the archive is safely written, so a crash between any two steps leaves
either the old snapshot or the new one — never a half-written world. A leftover `.staging/` from an
interrupted run is discarded at load and a leftover `.previous/` is restored, both with a warning.

The folder has one writer, and the folder is what the rule protects: `world.lease` names the process
writing it, refreshed on every write. A write that finds a lease another process refreshed within
`WorldLease.StaleAfter` (30 minutes) is refused with the holder named; a lease nobody refreshed for
that long is taken over with a warning.

## A refused snapshot is evidence

The manifest is the only hard gate in the format. If it cannot be read or parsed, the archive counts as
damaged and is never loaded silently: the loader falls back to the newest backup whose manifest reads,
and says so loudly. The snapshot it refused is kept aside as `damaged-<stamp>/` rather than deleted —
nothing reads it, and no later cut, prune or load removes it.

Salvage is **per entry, not per domain**. One row the restore cannot materialize — an item definition a
mod update removed, an id that no longer maps — is skipped by itself and named in the report, instead of
taking the whole world down with it.

Version mismatches do not silently load either. A different `protocolVersion` or `gameBuild` opens the
world in repair mode with a warning; a `schemaVersion` newer than the reader skips that payload instead
of guessing at it.

## Backups and retention

Every successful save archives a backup; so do a layer transition, an explicit request, the interval
autosave, and the restore that is about to replace a live snapshot. The interval defaults to ten
minutes (`SaveOptions.DefaultAutosaveIntervalMinutes`) and is armed only while a world is actually
loaded, so a host that returned to the menu does not churn the archive of a world nobody is playing. A
cut the player asked for always wins the seam.

Retention keeps the newest N archives (default 10, `SaveOptions.DefaultBackupRetentionCount`), never
deletes the newest one, and runs oldest-first after every committed cut. A file it cannot delete is
reported; the cut stays committed and the world stays loadable.

## What it is not

No cloud saves, no cross-machine save sharing, no second archive format for backups, and no
coexistence with the native `save.sv`. This is a local disk surface, not a wire protocol: nothing in
this page changes what a peer receives.

## Related reading

- [The shape of CUO](architecture-overview.md) — the checkpoint a cut is built from
- [Save mod data across sessions](../how-to/save-mod-data-across-sessions.md) — the mod's own table in the host's save
- [State streams and snapshots](state-and-snapshots.md) — what a snapshot means elsewhere
- [Build, test and deploy](../contributing/build-and-test.md) — where a save failure shows up in the logs
- [Glossary](../reference/glossary.md) — world archive, cut, backup, lease, checkpoint

---

[Documentation](../README.md) > [Internals](README.md) > How a world is saved

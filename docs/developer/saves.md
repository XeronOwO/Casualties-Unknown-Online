# Saves

English | [中文](saves.zh.md)

CUO keeps its own world archive and never reads or writes the game's own save file. A world is a
folder archive holding the current state plus a series of compressed backups, which is what makes a
restore, an inspection or a repair possible without any external tool.

The host is the only writer. A guest stores local settings and nothing else, so a second divergent
copy of the world never appears on someone's disk.

## Layout

- `index.json` lists the worlds, and one folder per world holds `world.json` plus a `live/` directory with the current state.
- `live/` stays unpacked: a save is a fast file write, and a human can read or repair it directly.
- `backups/` holds compressed `.cuoz` archives: layer-end, mid-run and automatic cuts.
- `world.lease` names the process currently writing the world, so two instances cannot interleave.
- A refused snapshot is kept as `damaged-<stamp>/` evidence instead of being deleted.

## What is written

Each cut holds the authoritative state by domain: run, players, items, world entities, enemies,
fluids, blocks, transients and per-character data. A new domain adds a file inside the archive; it
never rewrites an existing blob.

## Restore

Restoring a world loads the selected cut and rebuilds the state it describes; a promoted backup is
loaded the same way. The archive is versioned, so a cut written by an incompatible build is refused
with a reason instead of being loaded into a world it does not fit.

## Where the detail is

`docs/architecture/save-archive-format.md` is the normative format contract, including the file
contracts, the cut phases and the backup policy.

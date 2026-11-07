# Layer-end save and restore — batch 20261108-a

Date: 2026-11-08 (clients up 00:19:49, layer advance 00:21:31, continue 00:24:56).
Artifact: the installed `0.1.0+71f65c4f` tree (the ports-split build; its deploy was identity-verified then). Clients: the host desktop, Steam1 and Steam2 on the usual ports.
Batches: `.acceptance/20261108-a` (lobby 109775243782489646), `.acceptance/20261108-b`, `.acceptance/20261108-c`.

## Fixture

- Three clients joined one lobby and entered the world. The host and Steam1 stood at `(0, 446.751)` with the same four carried items (trashbag, lantern, dogfood, waterbottle).
- The host advanced a layer through the game's own sink: `layer-descend` starts `WorldGeneration.RegenerateWorld(false)` — the coroutine the console's `skiplayer` and the end-of-layer panel's ContinueRun both land in.
- The cut landed (host log):

```
[WorldService] [LayerDescent] this host is advancing a layer (twice: False, run 1 layer 0) — 2 member(s) told to generate their own new layer from this host's baseline.
[WorldService] Projected kernel run baseline (run 1, layer 1, 16 RNG bytes).
[SaveArchiveWriter] Committed layer-end snapshot for world w-20261107-1adb: 11 file(s), archive layer-end-20261107-162131.cuoz.
[WorldCutWriter] Cut LayerAdvance committed for world w-20261107-1adb: layer-end cut taken at layer-boundary, revision 657, layer 1, 11 file(s), backup …\layer-end-20261107-162131.cuoz; 12 item row(s), 3 player row(s), 0 enemy row(s), 0 fluid chunk(s), 0 world-entity row(s), 3 character(s), 0 world-block row(s), 0 transient row(s), native run fields present (133 recipe row(s)).
[WorldService] Committed kernel layer advance (run 1, layer 1).
```

- The member followed the same edge: Steam1 log `[LayerDescent] the host is advancing a layer now (twice: False, run 1 layer 0) — this member follows with its own generation`; the start gate held it (`keeping the loading screen up while waiting for the host`), then `the loading text is handed back to the game` and it entered the new layer (the game's own layer-2 alert replayed, translated).
- After the descent both clients stood at `(0, 483.615)` and kept the same four carried items (`body-items`: `1:trashbag|3:lantern|4:dogfood|5:waterbottle`).

## Quit and continue

- All three clients quit through the driver. They were relaunched with `-NoJoin` (no run started) and the host's `continue-run` restored the world (`index.json`'s `lastOpenedWorldId` = `w-20261107-1adb`).
- Host log: `[GenStream] restore …` lines, `Applying character restore (4 items).`, `CUO restore of world w-20261107-1adb reached the live world: … 133 recipe unlock row(s)`, `[SaveFacts] restored the live world: … 133 recipe unlock row(s) applied`.
- Host after the continue: `alive true, brainHealth 100`, at the layer's spawn `(0, 483.616)`, the same four carried items; `run-baseline`: `hasCommittedRunBaseline true, worldParams present, epoch 1`; `world.json`: `lastKind layer-end, layerIndex 1, playerCount 3, saveCount 1`.
- The host opened a session in the restored world (lobby `109775243783138248`) and Steam1 joined it as a member: `alive true`, at `(0, 483.616)`, the same four carried items. The host read the same four.

## Row 5 — the native save file

- The native `save.sv` under the game's persistent data root was last written 2026-10-24 11:50. Today's layer-end cut (00:21:31) and the continue (00:24:56) left its mtime untouched — the CUO path neither wrote nor re-created it.

## A finding from the member's rejoin

- After the member joined the restored world, the host's custody line read:

```
[ItemCustody] rows=314 objects=306 mismatches=4 — the first ones: 5197989525:RowWithoutObject:Carried, 9492956821:RowWithoutObject:Carried, 13787924117:RowWithoutObject:Carried, 18082891413:RowWithoutObject:Carried
```

- Those four ids are the member's pre-cut carried items; its restored items carry new instance ids (`35262760597@1:trashbag; 22377858709@3:lantern; 26672826005@4:dogfood; 30967793301@5:waterbottle` in the stored character). The player-visible items are correct on both clients, but the kernel table keeps four rows that no object answers. Filed as `docs/backlog/review/restore-leaves-stale-carried-rows.md`.

## What this settles, and what it does not

- Row 1's expected state is read: the layer-end cut committed at the boundary, the continue restored that world, and the character/run state came back on the host and on the member (same layer, same epoch, same four carried items, at the layer's spawn — the replaced layer's position is dropped, decision 170). The instance-id remap is named above as its own finding, not folded into this row.
- Row 5's in-game half is read from the file's own mtime.
- Row 3's in-game half is not applicable at a layer-end cut: the cut carries 0 enemy rows by design (the layer reset drops the leaving layer's enemies), so a killed enemy's tombstone is the mid-run cut's row (S3).
- Row 6's Continue-button state was not read (no main-menu UI recipe exists); it stays the user's look.

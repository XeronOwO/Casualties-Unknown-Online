# Acceptance record — the item-id counter is resumed by a restore

- Ticket: `item-id-counter-is-not-resumed-by-a-restore` — verdict: **moved to `done/`**
- Batch: `20261021-a` — tickets `guest-frame-rate-lower-than-host`, `give-item-to-another-player`,
  `item-id-counter-is-not-resumed-by-a-restore`
- Commit: `22fa90bf` · Deployed artifact: `CasualtiesUnknownOnline.dll`
  `0.1.0+c9769177d53b0279daa93f05f75b1c513f53a546` — `tools/verify-deploy.ps1` exit 0 in the run's own
  prelude; the commits above the build commit are documentation only
- Run: 2026-10-19 20:13:03 → 20:23 by the batch's clock · Host: physical machine · Member: sandbox
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `hotrepl` (both endpoints), `input`, `logs`,
  `artifacts` — preflight eleven present; the run's own prelude repeated `session-environment` and
  `verify-deploy` (`prelude2-session-environment.txt`, `prelude2-verify-deploy.txt`)
- Artifacts: the `i*` readings in the directory named by `acceptance-artifacts-dir`; Steam ids are written
  `<host>`/`<member>`

Run 2 is its own world: a fresh two-client run, cut twice with the CUO console `/save`, and restored through
the game's own Continue entry in a NEW host process both times. The host space's resume is the discriminating
reading (counters 241 → 242, then 242 → 243); the member space's only item id is counter 1, so its grants
show the restore's mark (1, not 0) rather than a raise above a larger last report.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| I1 | Continue a mid-run cut in a NEW process, then allocate one new host item | machine | **pass** | `i3-host-watermark.txt`: `[IdWatermark] the restored world already handed this side ids up to counter 241; resuming the local allocator above them.` and `[IdWatermark] set counter 241 (resuming at 242).` The new host item is `bandage` `1039703066563` (`i4-host-new-item.json`), whose counter is **242**; `i4-host-new-item-window.txt` carries `[ItemTrace] op=2 item=1039703066563 origin=OnItemPickedUp result=Committed(3) events=[Pickup, GenerationItem]`, and neither that window nor the continue window carries an `already exists` or `Conflict` refusal. |
| I2 | A guest is in the lobby while the host continues that world | machine | **pass** | The member was `active: true`, `inWorld: false`, in the host's new lobby before the continue (`i2b-guest-state2.json`), and still in the lobby after it (`i3-guest-state.json`); the host logged `[IdWatermark] granted 1 to <member>` at 20:20:38.017 and the member's own log `[IdWatermark] set counter 1 (resuming at 2)` at 20:20:38.023 (`i3-host-watermark.txt`, `i3-guest-watermark.txt`) — the restore edge, with no reconnect. |
| I3 | A guest that was away when the world was restored rejoins | machine | **pass** | The member quit before the second cut; the host restored the world SOLO in a new process (`i10-host-row3.txt`: resume counter 242, allocator resuming at 243), opened a lobby after the restore, and the relaunched member joined it: the host logged `[IdWatermark] granted 1 to <member>` at 20:22:08.783 and the member `[IdWatermark] set counter 1 (resuming at 2)` at 20:22:10.158 (`i10-guest-row3.txt`) — the handshake grant carries the restored mark, not 0. |
| I4 | Play on past the restore into the next layer | machine | **pass** | Before the layer change: 245 live item objects, 244 distinct ids, no duplicate, max counter 242 (`i9-census-before.json`). The game's own `skiplayer` applied (`i9-skiplayer.json`); the host logged `[LayerReset] dropped the previous layer's world-rooted items; 4 item record(s) remain` and the generation's `[GenStream] segment 2…6` lines (`i11-host-layer.txt`). After it: 263 objects, 263 distinct ids, `duplicateIds: []`, max counter **503** (`i9-census-after.json`) — the generation's publish allocated above the restored 242 — and no `already exists`/`Conflict` line in the window. |

## Residuals for the user

None.

## Limits

- **The member space's mark is 1 in both restores.** The member carried one identified item; its grant is
  the restore's mark (1) rather than a raise above a larger report, so the raise-above-last-report half is
  demonstrated on the HOST space (241 → 242, 242 → 243). A fixture with a member that had allocated many
  ids before the cut would exercise the same edge with a larger number; it is not this run.
- **The layer change is the game's own debug `skiplayer`, run last.** It is not a natural descent: the
  guest was still loading the layer at the census (`gateWaiting: true`, "Waiting for … to load"), so the
  duplicate census is the host's live scene, not both peers' views. The row is host-side publish, which
  is what it asks.
- **The host process's quit can linger.** The first quit logged `Steam API shut down` and left the process
  alive past 90 s; the run stopped its own process (the path was the physical install's executable), then
  relaunched. Both saves had already committed their archives before the stop (20:13:57 and 20:21:07), and
  the restores read them. The next batch should treat a logged shutdown with a live process as a driver
  note, not a client failure.
- **Two cuts, no gameplay.** The world is a fresh run with the two fixtures the run created; no combat,
  no long play, no layer beyond the first descent.

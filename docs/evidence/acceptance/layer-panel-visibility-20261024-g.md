# Batch 20261024-g — the end-of-layer panel's visibility rows, read on the machine

- Ticket: `docs/backlog/done/layer-end-save-and-exit-native-write.md` (the panel half)
- Artifact: `0.1.0+d4994cca`, deployed and verified against `<game-dir>` before the run
- Session: host + one guest, two clients, formed by the driver (`leave-world` → `create-lobby` → `join-lobby` →
  `start-run`); world up (`halfHeight = 512`, so the native floor line `-halfHeight + 3.1` is `-508.9`)
- Setup substitution: `body-place` (a declared recipe) moved the local body of each client down or up; the physics
  settled every body afterwards. Both bodies were read through the clients' own probes (`body-read`,
  `layer-panel-state`), never judged from a screenshot.

## Row 1 — the share of LIVING players at the floor decides, not the local body alone

| state | host body | member body | floor | panel `activeSelf` | gate's own line |
|---|---|---|---|---|---|
| share met (2/2 living at the floor) | -509.385 | -511.438 | -508.9 | **true** | shown |
| below the threshold (1/2: the member back up) | -509.385 | 380.351 | -508.9 | **false** (8 consecutive reads) | closed in the game's own write frame |

So the host's panel appears because the GROUP finished the layer, and it disappears again when a living member is still
up top — the two states the user's ruling names.

## What the two earlier reads on this ticket found (kept, because they are why the rows pass)

- `0.1.0+cea99ab9`-era read: with both living bodies at the floor the panel stayed **hidden** — the write from the
  host shell's `Update` lost to `WorldGeneration.Update`, which writes the same object every frame.
- `0.1.0+76dff211` read: the panel showed with 2/2 at the floor but STAYED shown when the member moved back up — the
  native latch (`if (!savePanel.activeSelf && … && localBody.y < floor) SetActive(true)`) re-opened whatever a later
  phase closed, so the object flipped every frame and the probe could read either state.
- `0.1.0+d4994cca` (this batch): the close runs as a postfix on the game's own `Update`, the open half stays in the
  late frame. Both rows above read clean, repeatedly.

## Not read here, and named

- **The dead-host case**: the panel appearing when the host's own body is dead. Killing a client's body needs a
  forced-death probe; the run did not stage it.
- **The member's own panel**: that a member sees no "Continue"/"Save and exit" buttons at all (`LayerPanelChoicePolicy`
  hides them). The run did not open a member's panel at the bottom of a layer.
- **The save half on this artifact**: "Save and exit" taking a CUO cut instead of writing `save.sv`. It was read on
  `0.1.0+2f674b80` (record `layer-end-save-and-exit-reading-20261024.md`); this run did not repeat it.

## Note on the input authorization

This batch drives everything through the in-process driver (recipes and control ids); the OS-level input tool
(`tools/acceptance/send-input.ps1`) was not needed for these rows. It exists for the drag rows.

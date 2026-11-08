# S2 row 6 — the Continue entry without a native save (2026-11-08, batch 20261108-f)

- Ticket: `docs/backlog/done/save-layer-end-save-and-restore.md`, row 6.
- Instrument: `tools/acceptance/recipes/main-menu-probe.cs` (reads `SaveSystem.HasSave()`, the adapter's
  `HasRestorableWorld()` through `PatchBridge.Impl`, and the live `PreRunScript.loadButton` / `loadButtonImage` /
  `saveTimeText`), plus `capture-frame`.

## The state

`PreRunScript.Start` (`reversing/Assembly-CSharp/Assembly-CSharp/PreRunScript.cs:54-65`) sets
`loadButton.interactable = SaveSystem.HasSave()` and only then reads `save.sv` for the icon and the played-time label.
`PreRunScriptStartPatch` (postfix, `src/CasualtiesUnknownOnline.GameAdapter/Patches/PreRunScriptStartPatch.cs`) overrides
the interactable state with `PatchBridge.Impl.HasRestorableWorld()`. The isolating state is therefore: no `save.sv` on
the machine, a CUO world present — the game's own code would leave the button grey, so a lit button can only be the
override.

## Fixture

- The native save was renamed for the fixture: `save.sv` → `save.sv.bak` in the game's data folder (the user's own file;
  permission asked and given). The CUO world archive (`cuo/saves`) stayed untouched.
- The three clients were started through `launch-clients.ps1` fresh, so each `PreRunScript.Start` ran with the renamed
  save. (The earlier clients were quit first.)

## The reading

`main-menu-probe` on the host client (01:44 local, the main menu behind the game's warning splash):

```json
{"ok":true,"hasSave":false,"hasRestorableWorld":"True","preRunScriptFound":true,
 "loadButtonFound":true,"interactable":true,"imageEnabled":false,"saveTimeText":"Continue "}
```

- `hasSave=false` — the native source said no.
- `hasRestorableWorld=True` — the CUO repository has a world.
- `interactable=true` — the live button the player sees is reachable; with `HasSave()` false the game's own `Start`
  would have written `false`, so this value is the patch's override.
- `imageEnabled=false` and `saveTimeText="Continue "` — the native block that paints the save's icon and the played-time
  label was skipped (`Start` only runs it when `HasSave()`), exactly as the native code reads; the entry is still lit.
- Frame: `main-menu-20261108.png` shows the warning splash; the button state is the live probe reading above, not the
  frame (the menu sits behind the splash, which needs a real click to leave).

## After

- The clients were quit, then `save.sv.bak` was renamed back: `save.sv` is 1,842 bytes with its original
  `2026-10-24 11:50:06` stamp — byte-identical to before the fixture.

## Limits

- The batch read the entry state on the host client; the other two clients share the same profile and the same menu, and
  their state was not read separately.
- The frame captured the splash, not the menu; the row's expected state is the `interactable` value, which was read live.

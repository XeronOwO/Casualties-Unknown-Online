# Quick panel: the always-available teammate panel

The compact panel that acts on one teammate without opening the Online window.

## What the player does

Presses the panel's own session hotkey; a small panel appears docked in the canvas's bottom-right corner naming the teammate it
would act on, that player's status, and one button per action the pair can perform. Presses an action, or picks another
teammate from the panel's own target row. Never has to open the window, and never has to find the body on screen.

## What it is

A CUO-owned surface that **draws nothing itself**: since its own stage it "builds an `OnlineUiPanelModel` for the game's own
control(s) … docked in the canvas's bottom-right corner, which is the rect the IMGUI panel occupied", and "the click that comes
back is dispatched to the action registered under the id the control carried". The panel's own width is `340f` canvas units,
each target button asks for `96f`, and every id it registers is namespaced with `quick.` "so a control of the panel and a
control of the window (or of the context menu) can never be the same intent".

## Rules and states

- **It is the always-available alternative to two other surfaces.** `Plugin/OnlineUiQuickPanel.cs`: "the standalone
  player-interaction quick panel … is the always-available alternative to the transient in-world right-click context menu and
  the full Players page".
- **It acts on ONE teammate and keeps them until they leave.** The target is chosen by
  `Runtime/OnlineUi/QuickPanelTargetPicker.cs`: "the panel keeps its previously chosen target while that player is still an
  in-world remote; when the target is gone or no target was chosen yet, the nearest in-world remote wins (ties by SteamId for
  determinism)" — so the panel does not jump to whoever walked closer between two presses.
- **The actions are the eligible ones, and that list is the surface's content.** It "shows the selected in-world remote
  player's status and every eligible co-op interaction (carry/piggyback/drop/heal/use/push/recruit/take)". An action whose
  condition does not hold is not offered, which is the same rule the context menu follows (`member-menu.md`).
- **It never opens the window.** "The panel never opens the full Online window; it can be toggled from a configurable session
  hotkey."
- **The one key it keeps is ESC**, "because the frame's keys are read by the IMGUI pass the console still uses — the panel owns
  no keys of its own".

## Refusals

None of its own: an action that cannot apply is absent from the panel rather than present and refused. A refusal that the
action itself produces is answered on the status line, like any other gesture.

## Who judges

The same sides the actions have elsewhere: the panel is a launcher of the carry, heal, use, push, recruit and take families, and
each of those decides exactly as its own page says.

## What travels

Nothing of its own. The target selection reads the client's own knowledge of who is in the world; the actions send what their
mechanic pages describe.

## What the player sees and hears

A small panel in the bottom-right corner with the target's name and status, a row of target buttons that wraps when the
candidates do not fit one line, and one button per applicable action.

## Records

Accepted and open work: `../audit/code.md` row 168 (the panel's own code path) and
`../../backlog/review/cross-player-drag-use-feedback.md` for the cue family the panel shares. Decision 190 records the native
UI reuse rule the panel follows.

## Gaps

- The hotkey's default and its config surface are the plugin's preference page, not this corpus; the panel's own text keys
  follow `../cross-cutting/localization.md`.
- Which of the eight actions the panel offers in one state is decided by the same family gates as the context menu, and that
  per-state matrix is not tabulated here.

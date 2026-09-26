# The CUO launcher's idle fade — self-check (2026-09-26)

Ticket: `docs/backlog/review/cuo-launcher-button-obscures-the-view.md` (Medium; the user's acceptance
finding of 2026-09-21: the top-right `CUO 联机` launcher always covered the play area). Cycle scope:
the launcher stops obstructing the view while it is idle — it fades to a translucent floor, restores
full opacity on hover and stays clickable — through a pure presentation rule with no session, wire,
save or gameplay involvement. No deployment this cycle: the rendered result is the user's acceptance
run, and the development-period standard here is static and simulation evidence.

## 1. Mechanism inventory — what the launcher drew before

| # | Mechanism | Evidence (quoted) |
|---|---|---|
| 1 | Draw path | `Plugin.OnGUI() => _onlineUi.Draw()` → `OnlineUiOverlay.Draw` (when the command console is closed) → `OnlineUiWindow.Draw` → `DrawLauncherButton(ctx)`, which runs before any visibility test |
| 2 | The visible surface | The button style carries no background (`style.normal.background = null; style.hover.background = null; style.active.background = null;`), so the frame is the whole visible surface: `OnlineUiTheme.DrawBackground(rect)` draws `Panel` (alpha 0.96) plus four `Border` (alpha 0.9) edges, each through the explicit-colour `GUI.DrawTexture` overload |
| 3 | The label | `ctx.T("launcher")` — `LocalizationCatalog` holds `"CUO ONLINE"` (en) and `"CUO 联机"` (zh) — concatenated with the `▲`/`▼` state glyph on every draw pass |
| 4 | The clock | `OnlineUiContext.Time` → `ITimeSource.NowMs` = `Environment.TickCount` (monotonic ms). `Time.timeScale` is moved by the world-time feature, so a scaled clock must not drive UI timing |
| 5 | Same-family precedent | `ConsoleFadePolicy` (Runtime, pure alpha, no Unity dependency) and `LocationPingOverlay` (already fades by folding alpha into the colour it assigns); `StartGateOverlay` is the in-repo example of the ambient-tint path (`GUI.Box` under a `GUI.color` tint) |
| 6 | Session / wire | None: the launcher reads a translated label, the runtime clock and the pointer. No NetMsg, protocol, save or session state is touched |

## 2. What landed

- **`OnlineUiLauncherFade`** (`Runtime/OnlineUi`, new, 60 lines, public like the `ConsoleFadePolicy`
  precedent): owns the last-activity stamp and returns the launcher's opacity from
  `(nowMs, hovered)` alone — `IdleDelayMs` 4000, `FadeMs` 600, `IdleAlpha` 0.35. The idle window
  starts at the first evaluation, so an untouched launcher fades without ever being pointed at; a
  hover returns 1.0 and restarts the window; a backwards clock (`Environment.TickCount` wrap) is
  activity rather than an enormous idle age; equal timestamps return equal alpha, so IMGUI's Layout
  and Repaint passes cannot double-count the idle time. One instance is held by
  `OnlineUiWindowState` as presentation-only local state.
- **`OnlineUiWindow.DrawLauncherButton`**: the pointer fact comes from
  `Event.current.mousePosition` in the same GUI space as the rect; the alpha the rule returns is
  folded into the two visible halves — `OnlineUiTheme.DrawBackground(rect, alpha)` for the frame and
  `OnlineUiTheme.Launcher(alpha)` for the label — with no `GUI.color` on the path. The rect and the
  click semantics are untouched.
- **`OnlineUiTheme`** (253 lines): the five panel draws moved into one private
  `DrawFrame(rect, panel, border, alphaBlend)`; `DrawBackground(rect)` keeps the modal window's exact
  behaviour (same colours, same order, `alphaBlend: false`), and `DrawBackground(rect, alpha)` folds
  the alpha into `Panel`/`Border` and passes `alphaBlend: true`. `Launcher(float alpha)` re-derives
  the cached launcher style's normal/hover/active text colours from the theme palette, but only when
  the alpha changes: every `GUIStyleState` access allocates a wrapper, and the launcher holds one
  alpha for seconds at a time.
- **`OnlineUiWindowState`** (95 lines): `LauncherLabel(translated)` caches both label variants and
  rebuilds them only when the translation changes, so the draw pass builds no string. It adds three
  strings and no boolean — the class already sits at the source-shape gate's five-boolean ceiling.
- **No wire, protocol, save or gameplay change**; the launcher keeps its rect and its click meaning.

## 3. Family audit — every CUO surface that draws over the world

| Surface | Verdict |
|---|---|
| `OnlineUiOverlay.DrawNetworkHud` | Returns early unless a session or IP-direct link exists (`if (!ctx.IpDirectActive && ctx.Steam.CurrentLobbyId == 0 && ctx.Session.Role == SessionRole.None)`) and draws `GUI.Label` text only — no panel by design |
| `OnlineUiOverlay.DrawNameplatesAndArrows` | Per remote player, in world, `GUI.Label` only |
| `LocationPingOverlay.Draw` | Returns early when `pings.Count == 0`; pings expire and already fade, by folding alpha into the colour it assigns (`color.a *= Mathf.Clamp01(...)`) |
| `OnlineUiQuickPanel.Draw` | `if (!_visible)` early return — hotkey-toggled |
| `OnlineUiPlayerContextMenu.Draw` | Only while a target is selected (`IsOpen => _targetSteamId.HasValue`) |
| `CommandConsoleOverlay.Draw` | Only while open; its closed-console notifications are transient and fade per line |
| `StartGateOverlay` | Only while the start gate waits; tints `GUI.Box`, a native tint path |
| `ModUiRenderer` / `ModUiDrawing` | Mod-owned windows on the mod's own visibility state |

The launcher was the only always-drawn opaque control in the family, so no sibling needed the same
fix. Two adjacent facts are recorded rather than changed:

- The theme's shared panels (`DrawBackground(rect)` and `DrawOverlayBackground`) passed
  `alphaBlend: false`. Whether that suppresses blending is not decidable without the game, and if it
  did, those panels were opaque where their comments claim translucency. The launcher's own frame took
  the blended path this cycle; the shared panels followed in their own cycle
  (`docs/backlog/review/online-ui-panels-request-alpha-blend-false.md`), which made every themed frame
  blended.
- AGENTS rule 8 (reuse the game's native UI): the launcher's origin self-check already records "The
  main-menu entry is a top-right IMGUI launcher, not yet a cloned native `AdaptiveButton` in the
  game's own main-menu list" as an accepted limitation, and this ticket's non-goals keep the window
  framework and the position, so the fade is the scoped remedy.
- The human documentation blocks (`docs/en/`, `docs/zh/`) do not describe the launcher's
  presentation (only `docs/en/reference/mod-api.md` names the built-in Online UI as a data source),
  so no paired page change is owed.

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The rule's contract holds: opaque through the idle window, linear ramp to the floor, immediate full opacity on hover, the idle window restarts after a hover, equal timestamps are idempotent, a backwards clock is activity, and the alpha never leaves `[0.35, 1]` | `OnlineUiLauncherFadeTests`: 6 rule facts (boundary values at `age == IdleDelayMs` and `fadeAge == FadeMs`, the monotone ramp, the hover restore, the restart, 10 repeated evaluations at one timestamp, the wrap, the 41-sample range sweep) |
| 2 | The blindness was real and the pin discriminates: the mechanism pin fails on the pre-fix draw path | red `%TEMP%/cuo-red-launcher-fade-pin2.txt` (`1 failed / 9 passed`, the failure naming the body that draws the frame without the alpha); the pre-change body is an in-test negative sample |
| 3 | The alpha reaches both visible halves | the pin requires `OnlineUiTheme.DrawBackground(rect, alpha);` and `OnlineUiTheme.Launcher(alpha)` in the draw body; the theme folds the alpha into `Panel`/`Border` and the style's text colours |
| 4 | The alpha does not travel through the ambient tint, which the explicit-colour `DrawTexture` overload ignores | the pin requires the absence of `GUI.color` on the path; the first cut of this cycle used exactly that tint and left the panel at alpha 0.96 (review finding B1) |
| 5 | The draw pass allocates nothing in the launcher's steady states (the 4 s opaque window, the translucent floor, a hover) | the label comes from `OnlineUiWindowState.LauncherLabel` (rebuilt only when the translation changes), `T(key)` returns a stored catalogue string, the rule is arithmetic on two fields, and the style is touched only when the alpha changes; the pin forbids the per-pass concatenation. Residual: while the 600 ms ramp runs, each `style.normal`/`hover`/`active` access allocates one `GUIStyleState` wrapper — see §7 |
| 6 | No session or wire coupling | `LauncherFade` is referenced only by `OnlineUiWindow` and `OnlineUiWindowState`; the rule's inputs are the clock and the pointer |
| 7 | Gates and structure | 10 focused facts, normative gates, full suite with build, `dotnet format` and the source-shape gates all pass; the new Runtime type is one top-level type per file, 60 lines, outside the `Abstractions` baseline like its `ConsoleFadePolicy` precedent; the state gains no boolean |

## 5. Red, ladder and the numbers

Red: the mechanism pin on the pre-fix implementation — `1 failed / 9 passed`
(`%TEMP%/cuo-red-launcher-fade-pin2.txt`). Before that, the cycle's first contract case ran against a
tree whose `src/` was still at HEAD and failed for the same reason (`1 failed / 0 passed`,
`%TEMP%/cuo-red-launcher-fade.txt`); that earlier file revision is not in the tree, so the
reproducible red is the pin's negative samples plus the captured output above.

Ladder on the frozen tree: focused `OnlineUiLauncherFadeTests` 17/17, exit 0
(`%TEMP%/cuo-focused-launcher-fade-final4.txt`) → full suite WITH build 4003/4003 plus normative gates
208/208, exit 0 (`%TEMP%/cuo-full-launcher-fade-fix4.txt`; the gate run filters
`DeliveryChecklist_NoIncompleteRequiredBoxes` because this cycle's checklist is reset while it is
filled — the unfiltered gate project run afterwards is what proves it complete) → `dotnet format`
exit 0 (`%TEMP%/cuo-format-launcher-fade-fix4.txt`). An earlier unfiltered gate run measured 209/209
before the checklist reset (`%TEMP%/cuo-gates-launcher-fade.txt`); the same project afterwards reports
208/209 with the checklist gate as the single failure, which is the expected mid-cycle state, and the
final unfiltered run after the checklist was complete reports 209/209
(`%TEMP%/cuo-gates-final-launcher-fade.txt`). The intermediate figures of this cycle's earlier fix
rounds (focused 10/10, 13/13 and 16/16; full 3996/3996, 3999/3999 and 4002/4002) are superseded by the
numbers above; the review reproduced each round it saw.

## 6. Independent adversarial review and dispositions

A fresh-context reviewer, read-only against the frozen tree; full report and its fix-verification
section in `%TEMP%/cuo-review-launcher-fade.md`. It reproduced the numbers (focused, the full suite
with build, the gate counts) and corrected one of them.

| # | Severity | Finding | Disposition |
|---|---|---|---|
| B1 | blocker | The idle alpha never reached the launcher's panel: the frame is drawn by the explicit-colour `GUI.DrawTexture` overload, which takes its colour verbatim, so the `GUI.color` tint only dimmed the label and the panel stayed at alpha 0.96. Row 1 was not met, and the first contract case certified the defect. Evidence: the game's own `UnityEngine.IMGUIModule.dll` IL (the colour lands verbatim; the ambient tint is delivered *as* the colour argument by the sibling overload) | landed: the alpha is folded into the frame colours and the label style, and the launcher's frame is drawn with `alphaBlend: true`; no `GUI.color` remains on the path |
| M1 | major | The source pin asserted wording and statement order, so a draw that consulted the rule and discarded its result would still pass; no negative sample demonstrated it could reject a broken path | landed: the pin requires the alpha at the frame draw and the label style, forbids the ambient tint and the per-pass concatenation, and carries two negative samples (the pre-change body, a discarded-fade body) asserted False; `Flatten` now drops comment lines so a comment naming an API can neither satisfy nor break a pin |
| M2 | major | The "red-first" claim as written was not reproducible: on the fully unmodified tree the test file cannot compile (the rule type did not exist), and a missing-type compile error is not a red | landed: restated in §5 in the form it took, and the reproducible red is now the mechanism pin failing on the pre-fix implementation plus the in-test pre-change negative sample |
| m3 | minor | "Normative gates 209/209 unfiltered" was not reproducible after the checklist reset (measured 208/209, the failure being the checklist gate) | accepted: both figures are stated in §5 with their measurement points; the final unfiltered run closes the cycle |
| m4 | minor | The `GUI.color` restore was not exception-safe, and the global tint is sticky across frames | moot after B1: the fix removes the global tint from the path entirely, so there is nothing to restore or leak |
| m5 | minor | Acceptance rows 3, 4 and 5 were assumed rather than pinned | landed for row 4 (the per-pass label allocation is gone and the pin forbids it; the idempotence facts cover the "no flicker" half) and for row 5's mechanism (the rule's inputs are pinned to the clock and the pointer); rows 1–3 stay as §7 states — the rendered pixels and a real click have no probe in this tree |
| m6 | minor | `DrawBackground` passes `alphaBlend: false`, which may make any alpha through that call dead on arrival | landed as scoped: the launcher's frame takes the blended path this cycle; the shared panels' question is recorded as the new Low-Medium ticket named in §3, because flipping them changes surfaces this ticket does not cover and the rendered pixels are unverifiable here. Followed up in `review/online-ui-panels-request-alpha-blend-false.md`: every themed frame is blended now |
| n1 | nit | `OnlineUiWindowState` sits exactly at the gate's five-boolean ceiling | noted: the change adds no boolean (three cached strings); the ceiling is recorded in §7 as the reason the label cache is strings rather than flags |
| n2 | nit | Two documented-everywhere-but-the-ticket behaviours: the idle window starts at the first evaluation, and the clock wrap yields one opaque blip per ~24.9 days | recorded in §7 as intended behaviours with their user-visible consequence, not defects |
| F1 | major (second pass, on the repair) | The theme half was unpinned: no test named `DrawFrame`, `alphaBlend` or `WithAlpha`, so reverting the alpha overload to the unblended path left the panel opaque with a fully green suite — the same defect class one level down, moved from the call site into the theme by the repair itself | landed: `TheThemeFoldsTheAlphaIntoABlendedFrame` reads `OnlineUiTheme.cs` and requires the folded, blended alpha overload, the unchanged parameterless overload, and the shared frame draw passing its own blending argument into all five draws; the exact revert is an asserted-False negative sample |
| F2 | minor (second pass) | The pin stayed textual: `alpha = 1f;` after the rule call passed it, and a trailing comment naming `GUI.color` broke a correct implementation | landed: the pin requires the alpha to be assigned exactly once, an overwritten-alpha negative sample is asserted False, and `Flatten` strips a trailing comment only outside a string literal. Residual: a text pin cannot see arbitrary data flow — §7 |
| F3 | minor (second pass) | "The draw pass allocates nothing" was false for the style: each `style.normal`/`hover`/`active` access allocates one `GUIStyleState` wrapper, three per pass | landed as far as this cycle can verify: the theme re-derives the colours only when the alpha changes, so the steady states touch the style not at all; the ramp residual is stated in §4 row 5 and §7 |
| R3 + label half | major (third pass, residual of F1) | The pin guarded the call sites but not what they call: mutating `WithAlpha`'s body to `=> color;`, or leaving `Launcher(alpha)`'s state colours unfolded, restored an opaque panel with a green suite | landed: the matcher now requires the helper's fold and all three label fold assignments, and four mutation tests apply the reviewer's own counter-examples to the REAL theme source, asserting both that the text changed and that the pin rejects the mutation |
| F2 nits + F3 wording | nit (third pass) | A no-space `alpha=1f;` passed the census; `StripComment` did not skip escaped characters inside a string literal; the ramp allocation was described per access rather than per alpha change | landed: the census counts both spacings, the comment scanner skips an escaped character, and the docs state "each alpha change costs three `GUIStyleState` wrappers". |
| R4 | nit (fourth pass) | The reviewer closed the round and added five boundary counter-examples, none a defect of the code as it stands: a sixth draw appended inside `DrawFrame` (opaque again), a colour re-assigned after the label folds, an `alpha *= 0f;` compound assignment, a neutralised hover fact, and `IdleAlpha = 0.99f` | landed for the three that a census can close: `DrawFrame` must hold exactly five draws, `Launcher` exactly three `textColor =` assignments, and the window body exactly three mentions of `alpha` — plus a new fact that keeps the constants inside the ticket's reading (floor at or below 0.6, idle window in the 1–10 s band, ramp under 2 s). The remaining two are declared limits in §7 |

## 7. Limits — what this cycle does not prove

- **No rendered pixels.** There is no Unity runtime in this repository's verification path: neither
  the frame's translucency nor the hover restore has been seen. The user's acceptance run is the
  judge, and it should confirm that the *panel* — not only the label — changes opacity at idle.
- **The native `Graphics::Internal_DrawTexture` body is not inspectable here.** `alphaBlend: true`
  and the folded colour are chosen from the managed IL of the module the game loads; the shared panel
  path's `alphaBlend: false` question was closed in its own cycle by making every themed frame blended
  (`docs/backlog/review/online-ui-panels-request-alpha-blend-false.md`).
- **IMGUI has no runtime probe in this tree**, so the draw-path pins are source contracts with
  negative samples, not behaviour tests; a real click and a real frame are outside them.
- **No deployment, no game session, no dual-client check** — per the development-period standard.
- The rule's constants (4 s idle, 600 ms ramp, 0.35 floor) are this cycle's reading of the ticket's
  "on the order of a few seconds" and "clearly translucent"; the ticket delegates them.
- Intended behaviours worth knowing: the idle window starts at the first evaluation (a launcher that
  has not been drawn for longer than the window appears already faded when it returns, e.g. after a
  loading gate), and the `Environment.TickCount` wrap costs one 4 s opaque blip per ~24.9 days of
  uptime.
- The allocation claim is bounded by what it says: the steady states (the idle window, the floor, a
  hover) allocate nothing, while the 600 ms ramp costs three `GUIStyleState` wrappers for every pass
  that changes the alpha, because the style is touched only when the alpha changes. Holding those
  wrappers across frames would remove even that, but it holds native style-state pointers whose
  lifetime this cycle cannot verify, so the residual is declared instead.
- The draw-path pins read source text. They require the mechanism at the call sites, forbid the
  ambient tint, the per-pass concatenation and any surplus mention of the alpha, cap the frame's draw
  count and the label's colour assignments, and carry negative samples including a discarded-fade
  body, an overwritten-alpha body, a pre-change body and four mutations of the real theme source — but
  they cannot see arbitrary data flow: a neutralised hover fact (the pointer read into a variable that
  is then ignored) would still pass, and a behaviour test would need an IMGUI runtime this tree does
  not have. What the matrix proves is that a fade exists which settles at a floor strictly below full
  opacity inside the ticket's bands; "clearly translucent" itself is the user's frame, not this
  suite's verdict.
- The reviewer saw one non-reproducible observation: an incremental build reported warnings that a
  clean rebuild does not. Warnings are errors in this repository and the clean rebuild is green, so it
  is build contention in a shared output directory rather than a compiler warning; it is recorded
  here for the next cycle that sees it.

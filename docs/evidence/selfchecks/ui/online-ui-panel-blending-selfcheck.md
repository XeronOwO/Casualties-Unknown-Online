# The Online UI's panel blending — self-check (2026-09-26)

Ticket: `docs/backlog/review/online-ui-panels-request-alpha-blend-false.md` (Low-Medium; opened by the
launcher idle-fade cycle's independent review: the theme's shared panel draws asked for
`alphaBlend: false` while their own comments claimed a translucent "operator console" look). Cycle
scope: every frame the theme draws asks for alpha blending, so the palette's alphas are live and the
flag-versus-comment contradiction is gone. No wire, session, save or gameplay involvement; no
deployment this cycle — the development-period standard here is static and simulation evidence.

## 1. Mechanism inventory — what the theme drew before

| # | Mechanism | Evidence (quoted) |
|---|---|---|
| 1 | The shared frame draw | `OnlineUiTheme.DrawFrame(rect, panel, border, alphaBlend)` drew five rectangles through the explicit-colour overload `GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend, 0f, <colour>, 0f, 0f)` — one panel plus four border edges |
| 2 | The three callers | `DrawBackground(rect)` passed `alphaBlend: false` with the palette's own colours (`Panel` 0.96, `Border` 0.9); `DrawOverlayBackground(rect)` passed `false` with `OverlayPanel` 0.58; only the launcher's `DrawBackground(rect, alpha)` overload (the idle-fade cycle) was blended |
| 3 | What the flag is | Unity documents `alphaBlend` as whether alpha blending is applied when the image is drawn. This cycle did not re-derive the module's IL: it carries the launcher cycle's finding B1 (`docs/evidence/selfchecks/ui/cuo-launcher-idle-fade-selfcheck.md`), which read the game's own `UnityEngine.IMGUIModule.dll` and found that this overload hands the colour AND the flag to the native draw verbatim while the ambient `GUI.color` tint never reaches it |
| 4 | Why the flag was unobservable | the native `Graphics::Internal_DrawTexture` body is not in this repository, so no reading of the tree can say whether the unblended draw was opaque or already blending — the ticket's own words |
| 5 | The surfaces that draw a themed frame | the modal window body (`OnlineUiWindow.DrawWindowContents`), the launcher (`OnlineUiWindow.DrawLauncherButton`), the quick panel (`OnlineUiQuickPanel.Draw`), the context menu (`OnlineUiPlayerContextMenu.Draw`), the console overlay (`CommandConsoleOverlay`: `Draw`, `DrawClosedNotifications`, `DrawSuggestions`, `DrawTooltip` — four call sites of one helper) |
| 6 | The theme's documented intent | the file's own header ("a dark, translucent 'operator console' look") and the overlay member's comment ("this remains translucent so the world stays visible behind the command history") |
| 7 | The other rectangle draws in the plugin | `CommandConsoleInputRenderer`'s caret, input background and selection use the default two-argument overload, and `StartGateOverlay` paints through `GUI.Box` under a `GUI.color` tint — both blend by default. The theme was the only surface whose blend flag was passed **explicitly** |
| 8 | Session / wire | none: the change touches the private frame draw and the two shared helpers |

## 2. Decision and what landed

**Every themed frame is drawn blended.** The rendered frame the ticket wanted cannot be produced here,
so the decision is taken from intent plus evidence: the palette's alphas and the file's two comments
state the translucent design; the flag reaches the native draw verbatim; the launcher's frame already
blends. Drawing blended is the one choice that is correct under both readings of the old draw — where
that draw was already blending, the picture does not change; where it was not, the panels become the
surfaces the theme documents. Correcting the colours to opaque instead would have made the documented
design wrong rather than the code right, so it was rejected.

Landed:

- `OnlineUiTheme.DrawFrame` no longer takes the flag: its five draws hold the literal `true`, so a
  non-blending themed frame is no longer expressible in the theme.
- `DrawOverlayBackground` blends (`OverlayPanel` 0.58 now reaches the draw as documented).
- Both `DrawBackground` overloads keep their signatures, colours and draw order, so no call site in the
  window, the quick panel, the context menu or the console overlay changed.
- The comments state the mechanism (what the flag does on this overload) instead of the question they
  used to raise, and without asserting a rendered result this tree cannot observe.

## 3. Family audit — every sibling that draws over the world

| Sibling | Verdict |
|---|---|
| `OnlineUiWindow.DrawLauncherButton` | already on the blended path since the idle-fade cycle; untouched here |
| `CommandConsoleInputRenderer` (caret, input background, selection) | the default `GUI.DrawTexture(rect, texture)` overload ⇒ blending enabled by default; nothing owed |
| `StartGateOverlay` | `GUI.Box` under a `GUI.color` tint — the native tint path, not this theme's draws |
| `OnlineUiOverlay` HUD, nameplates, `LocationPingOverlay`, `ModUiRenderer` | `GUI.Label` text, folded-alpha colours and mod-owned windows; no themed frame draw |
| `docs/en/`, `docs/zh/` human blocks | no page describes the panels' alpha (the launcher cycle checked the same surface), so no paired page change is owed |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | Every themed frame draw asks for blending | the theme pin requires `ScaleMode.StretchToFill, true, 0f, panel` once and `…, true, 0f, border` four times inside `DrawFrame`, forbids `StretchToFill, false,` in the member, and caps the member at exactly five `GUI.DrawTexture(` calls |
| 2 | The console overlay blends too | the same pin requires `ScaleMode.StretchToFill, true, 0f, OverlayPanel` in `DrawOverlayBackground`; `TheThemePinRejectsAnUnblendedOverlay` mutates the real theme source and asserts the pin rejects it |
| 3 | The five surfaces still draw through the theme, and the overlay keeps all four of its draws | `EveryThemedSurface_DrawsThroughTheBlendedFrame` names each call site (window body, launcher, quick panel, context menu, console overlay), counts `CommandConsoleOverlay`'s four `DrawOverlayBackground(rect)` calls, and asserts no `StretchToFill,\s*false` spelling anywhere in the theme |
| 4 | No unblended draw can appear unnoticed | the whole theme file is capped: `CountOf(Flatten(themeSource), "GUI.DrawTexture(") == 6` — the five frame draws plus the overlay's own |
| 5 | The launcher's fade keeps working | the pre-existing window pin and the theme pin's fold/census half are unchanged in what they require of `DrawBackground(rect, alpha)` and `Launcher(alpha)`; the four pre-existing theme mutation controls still pass |
| 6 | No call-site change, no new state | the diff touches `OnlineUiTheme.cs` (264 → 271 lines, the dead parameter gone) and the UI test file; no call site, no new type, no new state |
| 7 | Red before green | 4 failed / 15 passed / 19 with the new pins against the pre-change `src/`, 19/19 after it |

## 5. Red, ladder and the numbers

| Step | Command | Result |
|---|---|---|
| Red (new pins, pre-change `src/`) | `dotnet test tests/CasualtiesUnknownOnline.Tests --filter "FullyQualifiedName~OnlineUiLauncherFadeTests"` | **4 failed / 15 passed / 19**, exit 1 — two real pin failures (`TheThemeFoldsTheAlphaIntoABlendedFrame`, `EveryThemedSurface_DrawsThroughTheBlendedFrame`) and two mutation controls whose anti-vacuity guard fired because the anchor text they mutate does not exist in the pre-change source (`TheThemePinRejectsAHardCodedBlendFlag`, `TheThemePinRejectsAnUnblendedOverlay`). The transcript's line numbers reflect the pre-final-edit arrangement of the test file and are not re-derivable from the frozen tree; the failure texts match the methods (`%TEMP%/cuo-red-panel-blending.txt`) |
| Focused after the change | same filter | **19 passed / 0 failed**, exit 0 (`%TEMP%/cuo-focus-panel-blending.txt`) |
| Focused after the review's fixes | same filter | **19 passed / 0 failed**, exit 0 (`%TEMP%/cuo-focus2-panel-blending.txt`) |
| Gates project, mid-cycle | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` | **287 passed / 1 failed / 288** — the one failure being this cycle's own checklist gate (boxes open, the expected red) (`%TEMP%/cuo-gates-panel-blending.txt`) |
| `dotnet format` | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0, `git diff --shortstat` byte-identical before and after (`7 files changed, 135 insertions(+), 56 deletions(-)`; `%TEMP%/cuo-format-panel-blending2.txt`, `cuo-format-panel-blending-exit.txt`) |
| Full suite WITH build, checklist gate filtered | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` | **4134 passed** (main) + **287 passed** (gates), 0 failed, exit 0 (`%TEMP%/cuo-full-panel-blending.txt`) |
| Gates project, final (checklist filled) | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests` | **288 passed / 0 failed**, exit 0 (`%TEMP%/cuo-gates-final-panel-blending.txt`) |

Counting discipline: a `--filter`ed number is never quoted as a full-suite number; the excluded gate is
named, and the final unfiltered gate run is stated separately.

## 6. Independent adversarial review and dispositions

A fresh-context reviewer read the frozen working tree (read-only; it re-checked `git status` after every
command). Full report: `%TEMP%/cuo-review-panel-blending.md` — 1 blocker, 1 major, 8 minor, 3 nit. Every
finding is dispositioned here, and every fix is in this same cycle.

| Finding | Severity | Disposition |
|---|---|---|
| B1 — the moved index row measured 166 characters against the gate's 160 budget, so the commit gate could not pass | blocker | FIXED: the row's clause is short now and the title moved to the past tense (nit n2 with it); measured 147 characters, and the gate project is green |
| M1 — the production comment asserted a rendered result ("draws those alphas opaque") that the ticket itself calls undecidable in this tree | major | FIXED: the comment now states the source-level fact ("a frame that does not ask for blending leaves those alphas with nothing to apply them"); the ticket and this self-check keep the "the native body is not inspectable" statement, and §1 row 3 marks the IL finding as the launcher cycle's, not re-derived here |
| m1 — the "What landed" table showed an unchanged call site as if the call had changed, contradicting the same ticket's "no call site changed" | minor | FIXED: the table now has a "the theme helper it calls" column and says the call sites are byte-identical, with before/after on what the helper forwards |
| m2 — §5's red row said all four failures named the unblended draw; two were the mutation controls' anchor guards | minor | FIXED: the red row separates the two real pin failures from the two guard firings and says why the guards fired |
| m3 — the checklist was said to still carry the previous cycle's evidence suffix ("selfcheck §1 (13 rows): the relay chain…") | minor | NOT REPRODUCIBLE: the reset removed every evidence suffix (`grep "13 rows" docs/evidence/delivery-checklist.md` finds nothing, and `git diff` shows the suffixes only as removed lines). The underlying concern is met: all seven boxes were re-checked in this cycle with this cycle's evidence |
| m4 — the red transcript's test line numbers cannot be re-derived from the frozen tree | minor | FIXED: the red row states that the line numbers reflect the pre-final-edit arrangement of the test file and that the failure texts are what identify the methods |
| m5 — the census's `DoesNotContain("StretchToFill, false,")` had a comma-shaped blind spot, and the census was a membership pin only | minor | FIXED: the theme-wide check is now `Assert.DoesNotMatch(@"StretchToFill,\s*false", …)` (spelling-independent) and the whole theme file is capped at exactly six `GUI.DrawTexture(` calls |
| m6 — "the theme was the only unblended draw surface" read as a plugin-wide grep but was scoped to explicit-flag draws | minor | FIXED: §1 row 7 is scoped to "the only surface whose blend flag was passed explicitly" and names `StartGateOverlay`'s `GUI.Box` tint path |
| m7 — the overlay's four draws were attributed to one method, and only that one was pinned | minor | FIXED: the four call sites are enumerated in the ticket's table, §1 row 5 and §3, and the census counts them (`Assert.Equal(4, CountOf(ReadSource("CommandConsoleOverlay.cs"), …))`) |
| m8 — a modified sibling ticket still read "the shared modal path keeps its own flags untouched", now false | minor | FIXED: the sentence now points at this cycle's follow-up and says every themed frame blends |
| n1 — the mutation controls locate their target by exact source text, so drift fails them at the guard rather than at the pin | nit | FIXED: every mutation control's guard now fails with "the mutation's anchor text is no longer in the theme — re-anchor this mutation before trusting it" |
| n2 — the moved ticket kept its question as a title while the index row stated the answer | nit | FIXED: the title is now "…asked for alphaBlend: false" and the index row matches |
| n3 — nothing in this self-check names the browser-rendered frame beyond the acceptance pass | nit | ACKNOWLEDGED, no action: §7 states that no pixels exist in this tree, which is the only honest formulation |

What the review could NOT falsify (its own words): the pins' realness (every clause resolves against the
frozen source; the mutation controls fail for the right reason once their anchors exist), the absence of
any remaining explicit-flag draw in the plugin, the absence of other `DrawFrame` callers, `WithAlpha`'s
identity and the launcher's fade contract, the "no call site changed" claim, the index/manifest
bookkeeping in both directions, and the checklist reset's byte-level diff (12 insertions / 12 deletions
with the explanatory body identical).

## 7. Limits

- **No rendered pixels.** Whether the panels were opaque before this change is not observable here (the
  native draw body is not inspectable), and neither is the result afterwards: the pins prove the code
  asks for blending on every themed surface, and the frames themselves are the unified acceptance pass's
  observation.
- **The pins read source text.** They can see that every themed draw requests blending, that no themed
  draw requests the opposite in any spelling, and that the theme paints exactly six rectangles; they
  cannot prove what the native draw does with either flag. A surface outside the theme that hand-rolls
  its own rectangle is caught only by the census rows the pin names.
- The console overlay's 0.58 alpha is now live as documented (the world shows through the history,
  suggestion and tooltip panels); it is the one surface whose appearance this change may visibly alter,
  and its readability over the world has no probe in this tree. The modal window's 0.96 and the border's
  0.9 are nearly opaque by design, so their visual change is subtle even where the old draw suppressed
  blending. Nothing here changes the palette, the fonts or the layout.
- No deployment this cycle: the change is presentation-only and the development-period standard is
  static and simulation evidence.

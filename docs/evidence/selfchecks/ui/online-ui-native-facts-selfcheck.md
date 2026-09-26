# The Online UI's native facts probe — self-check (2026-09-26)

Ticket: `docs/backlog/review/online-ui-art-and-controls-overhaul.md` (High; **stage S1 of four**).
Cycle scope: the game's own UI as FACTS. A read-only runtime probe reads the four unknowns the ticket
names — the active TMP font asset, a live settings row's `Image` sprite / `Image.type` /
`pixelsPerUnitMultiplier`, the game chrome's image styles and `PlayerCamera.uiScale` — and the uGUI host
that can reach them; the pure Runtime pieces decide when to stop asking and how a reading reads. No
user-visible behaviour changes, no wire/protocol/save change, no deployment this cycle: the four logged
values come from one game run with the built plugin, exactly as the ticket's S1 line states.

## 1. Mechanism inventory — what the game's UI side is

| # | Mechanism | Evidence (quoted / cited) |
|---|---|---|
| 1 | The game's UI is uGUI + TextMeshPro with zero IMGUI | The recon (session artifact `%TEMP%/cuo-ui-recon.md`, 1054 lines): no `OnGUI`/`GUIStyle`/`GUILayout`/`GUI.*` anywhere in the decompiled assembly; `SettingsMenu.cs` builds rows from `Resources.Load` prefabs (`Utils.Create`, `Utils.cs:16-19`) |
| 2 | The game's main canvas | `PreRunScript.cs:351` `public static PreRunScript instance;` and `:450` `public Canvas mainCanvas;`; `PlayerCamera.cs:3142` `public static PlayerCamera main;` and `:3214` `public Canvas mainCanvas;` |
| 3 | `PlayerCamera.uiScale` IS the canvas scale | `PlayerCamera.cs:23-27` — `public static float uiScale { get { return PlayerCamera.main.mainCanvas.transform.localScale.y; } }`, i.e. a child of that canvas inherits the game's UI scale instead of guessing it |
| 4 | A settings row's shape | `SettingsMenu.cs:104-146` — `Special/GameSettingDropdown` is instantiated, its child 1 is the `TMP_Dropdown` (`g.transform.GetChild(1).GetComponent<TMP_Dropdown>()`) and child 0 the label (`g.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = …`); `Special/GameSettingInput` is the same shape with a `Button` on child 1 |
| 5 | The settings screen prefab is NOT a safe probe target | `SettingsMenu.cs:12-18` `OpenMenu` returns early while `SettingsMenu.instance` exists and otherwise creates `Special/SettingsMenu`; `:220-224` its `Start` runs `SelectTab(Setting.SettingCategory.Video)`, which spawns every Video row and adds tooltips/listeners; `:227-231` `Close` destroys it and calls `Settings.SaveSettings()`, and `:28-34` `ResetToDefault` resets `Settings.settings` and saves. A probe copy would therefore build a whole screen and block the player's own settings menu — the rule the host and its pin encode |
| 6 | Font asset and 9-slice are prefab-serialised | The recon: fonts are TMP assets and `Image.type` / border never appear in code; both are readable only from an instantiated object or a live component — the reason a runtime probe exists at all |
| 7 | This cycle's IMGUI side | Untouched: `OnlineUiTheme` keeps its six `GUI.DrawTexture(` draws and the five-surface census (`OnlineUiLauncherFadeTests` stays green, unchanged) |

## 2. What landed

Thirteen new source files and five new test classes; the split follows the boundary the tree already
enforces (Runtime cannot reference UnityEngine, the plugin must not bind a game assembly, and only the
adapter may touch `Assembly-CSharp`):

- **Runtime, pure (8 files, 14-142 lines each)** — `OnlineUiNativeFacts` (the reading: `CanvasAttached`,
  `IsComplete` over the four unknowns, `Unavailable(note)`), `OnlineUiNativeRgba` (four channels + the
  game's `#RRGGBBAA` idiom), `OnlineUiNativeImageStyle` (sprite, type, PPU multiplier, 9-slice border,
  colour, occurrence count, `IsNineSliced`), `OnlineUiNativeTextStyle`, `OnlineUiNativeStyleCensus`
  (duplicates fold into one row with a count, frequency orders them, the cap trims the tail, ties break
  by name), `OnlineUiNativeFactsReport` (the log lines, the `unavailable` spelling, the missing-part list
  in the ticket's order, the cap marker), `OnlineUiNativeFactsOutcome`, and
  `OnlineUiNativeFactsCapturePolicy` (see the retry design below).
- **Runtime port (1 file)** — `IOnlineUiNativeFactsQuery.Capture()`: the total contract (never throws,
  never blocks the frame callback, "no canvas" is a reading and not an exception).
- **GameAdapter (3 files, 119-179 lines)** — `OnlineUiNativeSurfaceHost` (the game's canvas first from
  `PreRunScript.instance.mainCanvas`, then `PlayerCamera.main.mainCanvas`; a CUO canvas parented under
  it, sorting 30000, INACTIVE from its first statement — before it is parented, before its `Canvas`
  exists and before any row is loaded; the game's two row prefabs instantiated; `Dispose` destroys the
  canvas), `OnlineUiNativeStyleReader` (the `Image`/`TMP_Text` reads and the bounded live-canvas sweep,
  which skips CUO's own subtree and reports whether the bound stopped it), and `OnlineUiNativeFactsCapture`
  (assembles the four facts, keeps the host between attempts, rebuilds it when a scene change destroyed
  it, disposes it the moment a complete reading lands, and converts any thrown exception into a reading).
- **Plugin (1 file)** — `OnlineUiNativeFactsProbe`: asks the Runtime policy whether to probe, calls the
  optional port, prints the Runtime report once, and logs one warning when the run ended without the full
  reading. `OnlineUiHost.Update()` drives it with `ITimeSource.NowMs`.
- **The retry design (settled after the review).** The game's canvas appears within seconds of a launch,
  while `uiScale` needs a RENDERED game that may be an hour of menu away, so the policy polls every 500 ms
  only while no canvas has been seen (bounded by a five-minute surface deadline — a game that never
  reaches a menu must not be polled forever), then slows to 5 s and keeps asking until every unknown is
  read or the 2 000-attempt budget runs out. `Partial` therefore means "the game never produced a readable
  run in this session", not "the menu took longer than five minutes" — the earlier single-deadline design
  lost the stage's own deliverable on an ordinary session.
- **Wiring** — `IGameAdapter` composes the port (13th), `GameAdapterComposition` registers it from the one
  adapter singleton, `GameAdapter` implements it explicitly and disposes the capture with its own
  `Dispose`; `AdapterCapabilityPortShapeTests` pins 13 ports / 17 members.
- **Dependency** — `Unity.TextMeshPro.dll` vendored into `references/` (gitignored, copied on demand)
  and referenced by the adapter only, `<Private>False</Private>`; `references/README.md` carries its
  copy line and origin row.

## 3. Family audit — what else touches the game's UI from CUO

| Surface | Verdict |
|---|---|
| `OnlineUiTheme` / the five IMGUI surfaces | Untouched this stage; their blended-frame pins stay green (19 cases in `OnlineUiLauncherFadeTests`, unchanged) |
| The plugin's other adapter ports | The new port follows the existing pattern exactly: declared on the Runtime aggregate, registered from the adapter singleton, resolved optionally by the consumer (`services.GetService<…>()`), asserted once each by the port census |
| `OnlineMenuInputGuard` / `INativeInputBlocker` | Untouched: the probe creates no `GraphicRaycaster` and stays inactive, so it cannot take input or join the EventSystem — the modal guard has nothing new to suppress |
| Mod UI windows (`ModUiDrawing`) | Untouched: mod windows are mod-owned IMGUI on their own visibility state |
| The plugin's adapter coupling | The new plugin code names no adapter type (it resolves the Runtime port). The one pre-existing exception is unchanged and not part of this cycle: `PluginDependencyRegistrar` calls `GameAdapterComposition.Register` |
| Wire, protocol, save, gameplay | None: the probe reads; no `NetMsg`, no save field, no protocol change (protocol stays 43) |

## 4. Self-check table — claim × evidence

| # | Claim | Evidence |
|---|---|---|
| 1 | The census folds duplicates, orders by frequency, breaks ties by name, caps the tail and treats a border difference as a different style | `OnlineUiNativeStyleCensusTests` 8 cases (empty, x3 fold, frequency order, tie order, cap trims the tail, non-positive cap, 9-slice vs plain, text-style fold) |
| 2 | A reading reads: the summary carries all four facts under one grep prefix, an unread fact says `unavailable` rather than zero (empty censuses included), the missing list follows the ticket's order, a capped census says it is capped, a row line carries `nineSlice`/`border`/`xN`, and a diagnostic prints as a note | `OnlineUiNativeFactsReportTests` 9 cases |
| 3 | The retry policy is bounded on every axis and terminal: first attempt immediate, fast interval before the canvas and slow after it, wrapped clock still attempts, complete ends it, incomplete keeps asking, an ATTACHED run outlasts the surface window, no canvas ever → `SurfaceMissing`, attempt budget → `Partial`, finished runs stay finished | `OnlineUiNativeFactsCapturePolicyTests` 11 cases + `TheDefaultBudgetIsBoundedOnEveryAxis` |
| 4 | The colour value clamps an out-of-range channel and spells eight hex digits | `OnlineUiNativeRgbaTests` 4 cases |
| 5 | The host's safety rules hold in the real source: canvas is a CHILD of the game's canvas (exactly one `.SetParent(`) with the pre-run source tried first, the spawn surface is exactly the game's two row prefabs through exactly one `Resources.Load` and one `Instantiate(`, the root is inactive before it is parented/given a Canvas/given a row and is never re-activated, the probe destroys its own canvas and rebuilds a host whose canvas a scene change destroyed, and no adapter or plugin source loads `Special/SettingsMenu` (path or quoted bare name) | `OnlineUiNativeHostPinTests` 16 cases, each `Assert.*` carrying a negative sample or a mutation of the real source |
| 6 | The pins discriminate on the REAL files, not only on in-test samples | six mutation controls fired on the frozen sources: canvas order swapped → 2 failed/14 passed; `Resources.Load<GameObject>("Special/SettingsMenu")` added to the capture → 1/15; `root.SetActive(false)` moved after the spawn loop → 2/14; `root.SetActive(true)` added → 1/15; a third spawn through `Object.Instantiate(Resources.Load(…))` → 1/15; the settings-menu path planted in a PLUGIN file → 1/15. All six reverted byte-identically (`md5sum -c` OK against `%TEMP%/cuo-s1-hashes-final.txt`) |
| 7 | The plugin does not format the reading itself and stays off the per-frame path | `ReportsThroughTheRuntime` requires the policy call and the Runtime report in `OnlineUiNativeFactsProbe.cs`; the pin's own mutation (hand-built lines instead of the report) is asserted red |
| 8 | The adapter seam stays consistent with its census | `AdapterCapabilityPortShapeTests` 19 cases pass with the port added (13 ports / 17 members, aggregate composes exactly the pinned ports, each registered exactly once) |
| 9 | Gates, build and structure | build 0 warnings / 0 errors; focused `OnlineUi` 151/151 and the five new classes 48/48; normative gates 287/288 unfiltered with this cycle's reset delivery checklist as the single failure (287/287 when that gate is filtered out); full suite WITH build 4183 (tests) + 287 (gates, same filter), exit 0; `dotnet format` exit 0; every new file is one top-level type per file and the largest touched file is `GameAdapter.cs` at 573 lines, under the 600-line gate |

## 5. Red, ladder and the numbers

This stage adds a mechanism rather than fixing a defect, so there is no pre-change red to show: the four
pure test classes call the production functions directly (they cannot pass without the code), and the
source pins — which cannot be red at HEAD because their files do not exist there — carry their
discrimination in negative samples plus the six mutation controls of §4 row 6.

Ladder on the fixed tree: `dotnet format CasualtiesUnknownOnline.slnx` exit 0
(`%TEMP%/cuo-s1-format-final.txt`) → focused `FullyQualifiedName~OnlineUi` 151/151
(`%TEMP%/cuo-s1-focus-final.txt`) and `FullyQualifiedName~OnlineUiNative` 48/48
(`%TEMP%/cuo-s1-focus2.txt`) → normative gate project 287/288, the single failure being this cycle's
reset delivery checklist, which is the expected mid-cycle state (`%TEMP%/cuo-s1-gates-final.txt`) → full
suite WITH build: tests project 4183 passed, gates project 287 passed, exit 0
(`%TEMP%/cuo-s1-full-final.txt`) — that run carries
`--filter "FullyQualifiedName!~DeliveryChecklist_NoIncompleteRequiredBoxes"`, because the checklist is
reset while it is filled; the unfiltered figure in that state is 287/288 above. The count decomposes:
4134 before this cycle + 48 new facts + one new `PortCensus` row for the 13th port = 4183. The final
unfiltered gate run after the checklist is complete is recorded in §6's closing line.

**A defect this cycle found in itself, and fixed in the same change.** The first version of the pin test
spelled its multi-line anchors with `\n`, which matched while the new files were still LF in the working
tree; `dotnet format` normalised them to CRLF (`end_of_line = crlf`), and two pins went red on unchanged
sources — a pin that breaks when the tree is checked out the way this repository checks out
(`core.autocrlf=true`) is a false alarm waiting for the next person. Every pinned file is now read with
`\r\n` normalised to `\n`, and the multi-line anchors are matched against a flattened view, so indentation
and line endings cannot decide a result.

## 6. Independent adversarial review and dispositions

A fresh-context reviewer read the frozen revision (both recorded `git diff` md5s matched, so the revision
it reviewed is provably the one the fixes were applied to), reproduced the build, the focused runs, the
gate counts and every decompiled citation, and returned 2 majors, 13 minors and 5 nits — no blockers. Its
full report is `%TEMP%/cuo-review-online-ui-native-facts.md`; the interim claim that the recorded
full-suite `exit 0` came from a pre-reset checklist was withdrawn by the reviewer itself after checking
the artifact's own totals (an unfiltered gate run always totals 288).

| # | Severity | Finding | Disposition |
|---|---|---|---|
| F1 | major | §4 row 6's byte-identity sentence cited a baseline recorded before a later comment edit, so `md5sum -c` fails against the tree even though every mutation was restored byte-identically at the time | landed: the six controls were re-run on the fixed tree against a fresh baseline (`%TEMP%/cuo-s1-hashes-final.txt`), and §4 row 6 / §5 cite those artifacts |
| F8 | major | the deadline clock started at plugin load and terminal states never retry, so a menu dwell over five minutes ended the run `Partial` with `uiScale` missing — the stage's own deliverable lost on an ordinary session | landed: the policy now bounds only the canvas-missing phase (five minutes), slows to 5 s once attached and keeps asking to the attempt budget; `AnAttachedRunOutlastsTheSurfaceDeadline` reproduces the exact scenario as a case |
| F2 | minor | the new self-check's MANIFEST row still pointed at `todo/` after the ticket moved | landed: `in-progress/` (the reference gates exempt `docs/evidence/`, so nothing else would have caught it) |
| F3 | minor | the ticket and the port contract claimed the probe tears everything down in the same call, while the capture deliberately keeps the host between attempts | landed: both now state "disposed the moment a reading is complete, at the latest when the adapter is disposed; kept between attempts" |
| F4 | minor | the chrome census had no completeness or missing representation (`IsComplete` ignored it, `MissingParts` never named it) | landed: chrome is part of both; `AnEmptyChromeCensusIsMissingRatherThanComplete` |
| F5 | minor | the mutation artifacts on disk were stale (pre-CRLF-fix, 4/3/3 failures) against §4's 2/1/2 | landed: six fresh controls with fresh artifacts (`%TEMP%/cuo-s1-mc*.txt`); the reviewer's independent predicate replay already reproduced 2/1/2 |
| F6 | minor | the port-census case count was 14, the real count is 19 (13 theory rows + 6 facts) | landed |
| F7 | minor | the ladder printed the full-suite gate figure without naming the filter, and cited the unfiltered run for the filtered number | landed: the ladder names the filter and cites the filtered figure from the full-suite artifact |
| F9 | minor | the clock-wrap case pinned the elapsed clock to zero, silently disabling the deadline | landed: a wrap restarts the interval and the surface window, the attempt budget stays the hard bound, and the class doc says so |
| F10 | minor | `rows=0` / `chrome=0` printed as facts where the class documents "unavailable rather than zero" | landed: an empty census prints `unavailable` |
| F11 | minor | the missing list was not in the ticket's order and never named chrome, and its pin asserted the implementation's own literal | landed: the order is canvas, font, rowStyle, chrome, uiScale — the four unknowns in the ticket's order behind the surface they need — the code doc quotes that order, and the case asserts the sequence |
| F12 | minor | after a scene change the cached host held destroyed objects, so one attempt threw `MissingReferenceException` before self-healing | landed: the capture rebuilds a host whose root is gone, and `TheProbeTearsItsOwnSceneObjectsDown` pins the guard |
| F13 | minor | the census caps were invisible in the log, so a capped census could be read as a complete one | landed: the summary prints `chrome=N (capped at 40)` and the sweep reports when the node bound stopped it (as a note) |
| F14 | minor | the record's `Failure` field carried non-failure notes and printed as `failure:` even on a complete reading | landed: the field is `Note` and prints `note:`; `AnAttemptWithSomethingToExplainPrintsItAsANote` |
| F15 | minor | the ladder cited `%TEMP%/cuo-s1-focus-final.txt`, which did not exist | landed: that artifact now exists (the focused run writes it) and §5 cites it |
| N1 | nit | "8 files, 29-119 lines each" was wrong | landed: 14-142 |
| N2 | nit | "the plugin names no adapter type" is over-broad (a pre-existing exception exists) | landed: §3 names it |
| N3 | nit | the root was created parented and active, and deactivated two statements later, so its own `Canvas` components did run their Awake/OnEnable | landed: the root is deactivated as its first statement — before it is parented, before the `Canvas` and before any row — and the pin requires that order |
| N4 | nit | the pins were presence-only: a later detach, a re-activation, a third spawn through another API, a new subdirectory, or a concatenated path would have passed | landed: exactly one `.SetParent(`, no `SetActive(true)`, exactly one `Resources.Load` and one `Instantiate(`, the destructive prefab matched over the whole adapter AND plugin trees including its quoted bare name, and anchors matched after comment-stripping and flattening; six mutation controls replay the evasions |
| N5 | nit | long table rows in this document | accepted: the tables are wide by nature and no rule budgets a table row's width (the 160-character budget is the backlog index's) |

The final unfiltered runs after the checklist was complete: the normative gate project 288/288, exit 0
(`%TEMP%/cuo-s1-gates-complete.txt`), and the full suite WITH build — tests project 4183, gate project
288 — exit 0 (`%TEMP%/cuo-s1-full-gate.txt`).

## 7. Limits — what this cycle does not prove

- **The four values are not in this repository.** They are produced by one game run with the built
  plugin (the ticket's S1 line says so). Nothing in this cycle was deployed and no reading was obtained
  here: the probe prints its reading on the first frame the game has a main canvas, keeps asking at 5 s
  while a fact is missing, and gives up at the attempt budget — 2 000 attempts, of which the first five
  minutes are the canvas-missing window — logging the partial reading and naming what stayed missing.
- **No Unity runtime in the verification path**: no test in this tree can instantiate a `GameObject`, so
  the host is proven by the compile-time contract, the source pins and the mutation controls, not by a
  run. Whether the game's rows read the way the game's own settings screen draws them, whether an
  inactive parent really keeps these prefabs' `Awake`/`OnEnable` dormant, whether the control child
  carries an `Image` and the label child a `TMP_Text`, and whether `PlayerCamera.main` is null in the
  menu are all the user's run, not this suite's verdict.
- **The reading is a snapshot of one moment.** The probe stops at the first complete reading; a later
  canvas-scale change is not re-read. S2 needs a live scale only if it wants to follow a settings change,
  and would ask again.
- **`Special/GameSetting*` prefabs are the only spawn target**, and only under an inactive parent: a
  prefab whose own `Awake` had side effects would run none of them here, which is intended — but it also
  means the probe sees serialised state only. A font asset or sprite the game assigns at runtime (rather
  than serialising) would be reported as missing, and the report says so instead of guessing.
- **The chrome census is a census, not a theme**: it reports the live canvas' image styles deduplicated
  and capped at 40 rows, says `capped` when it hit that cap, and deliberately makes no "this is the
  chrome" judgement — that is S2's decision, made from the reading.
- Protocol stays **43**; no wire, save or gameplay behaviour is touched, and the IMGUI Online UI keeps
  drawing exactly as it did before this stage.

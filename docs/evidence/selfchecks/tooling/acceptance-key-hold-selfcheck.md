# Acceptance key hold — self-check

- Cycle: 2026-10-06 (tools/acceptance) — row A1g of
  `docs/backlog/todo/container-move-snapshot-only-sync.md`: the container-expansion kind is gated by
  `Input.GetKey(KeyBinds.GetBind("expanddesc"))` in the game's own
  `PlayerCamera.TryPerformInventoryAction` (`reversing/…/PlayerCamera.cs`, the container arm), and three
  batches left it unjudged, two of them naming the driver's in-process contract as the reason. Measured
  the same day: a window message queued on the client's OWN window moves the client's own input state
  while the OS input queue never sees a key. Decision 237 carries the rule.
- Scope: `tools/acceptance/drive-in-process.ps1` (the `declare` action), the new
  `tools/acceptance/driver/eval-declarations/` (the presence probe and the `window-key` declaration), the
  new `tools/acceptance/recipes/key-hold.cs`, the normative gate and the black-box driver tests, and the
  pages that name the `input` capability. No `src/` change: the capability queues a message that the
  client's own frame turns into the input its own gate reads.

## Mechanism inventory

| Mechanism | Evidence (source or runtime) | What this layer does with it |
|---|---|---|
| The game's own key gate | `reversing/…/PlayerCamera.cs`: `TryPerformInventoryAction`'s container arm reads `Input.GetKey(KeyBinds.GetBind("expanddesc"))` before its per-child loop; `HandleWhileDragging` reads the same bind for its cursor hint | `key-hold` holds, releases and reads exactly that bind, so the game's own release path takes its own branch |
| The game's own bind table | `KeyBinds.GetBind` / `GetBindName` over `Settings`' keybind list (`expanddesc` 304 LeftShift, `toggleinventory` 9 Tab, `altview` 306 LeftControl, `attack` 323 Mouse0 …) | the recipe resolves the bind through the game, maps its KeyCode to a Windows virtual key and scan code, refuses what it cannot map, and refuses a mouse bind outright |
| The client's own window | `Process.GetCurrentProcess().MainWindowHandle`; Unity's legacy input state is fed by that window's own message pump | the message is queued on that handle only; measured: `Input.GetKey` reads held on the next input while the OS key state stays 0 and the client is not the foreground window |
| The evaluator's own rules | Mono.CSharp in this install: one input at a time, a declaration cannot carry a trailing expression, and a type is declared once per client (each measured on a running client) | a declaration is a committed file of its own, loaded once per client through a presence probe; every recipe stays a single declared-arg expression |

## Self-check table

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | Declaration loading | `-Action declare -Declare <name>` probes for the declared type, sends the declaration file only when it is absent and probes again for its verdict, so the step is idempotent per client | `DriverToolTests.Declare_ProbesLoadsAndVerifiesTheDeclaration`, `Declare_OnAClientThatAlreadyHasItSendsNothing`, `Declare_ThatNeverLandsFails`, `Declare_WithoutAKnownNameIsAUsageError`; live: `g1-declare-1.json` (`sent: true`) then `g2-declare-2.json` (`sent: false`) |
| 2 | Declaration language and naming | every file under `driver/eval-declarations/` carries the `// declares:` name the probe asks for and compiles at the evaluator's language version as a whole input; the probe carries exactly one placeholder; a recipe's `// requires:` names a declaration that exists | `AcceptanceDriverGateTests.TheEvalDeclarationsCarryTheirNameAndCompileAtTheEvaluatorsLanguage` (census floor and its own samples) |
| 3 | OS-input boundary | the ban keeps its whole list; `PostMessage` and `GetAsyncKeyState` are excused in that one directory and nowhere else, and no other family is excused even there | `AcceptanceDriverGateTests.TheAcceptanceToolsNeverUseOsLevelInput` (exception predicate plus teeth: the same names outside the directory fail, `SendMessage` and `SetForegroundWindow` fail inside it) |
| 4 | Hold, release, read | `down`/`up` queue one message and report `posted`; the hold's verdict is the following `read` step's `heldAtEnd`; a mouse bind and an unknown bind are refused by name | live: `m2-shift-down.json` + `m3-shift-read.json` (held true, `osKeyAtEnd` 0, `isForeground` false), `m6-shift-up.json` + `m7-shift-read-after.json` (released), `c6-mouse-bind.json` (`mouse-bind-refused`), `c7-unknown-bind.json` (`no-bind`) |
| 5 | The gesture it unlocks | the same staged release moves the container's CHILD with the key held and leaves it where it was without it | `m1-before.json` → `m5-release.json` → `m8-after.json` (the dogfood sits under slot 1's bag) against `c1-refill.json` → `c4-release-no-key.json` → `c5-after-no-key.json` (it is still under slot 0's bag) |
| 6 | Capability truth | the dependency, workflow and tools pages name the declaration step as part of `input` | `docs/acceptance/dependencies.md` input row; `docs/acceptance/workflow.md` §5 and the capability table; `tools/AGENTS.md` |

## Limits (what this cycle does not claim)

- The smoke proves the mechanism and the LOCAL gesture on the deployed `0.1.0+ad6f73ee…` (artifact
  directory `20261006-b` in `acceptance-artifacts-dir`: `g*` the declaration and first hold steps, `k*`
  the ring toggle, `m*` the key-held gesture with its before/after tree, `c*` the control without the
  key). It ran on the primary Sandboxie box's client (evaluator `18591`), so the harder host is the one
  covered: the window handle resolves and the message lands inside a box. The physical host — also a
  legitimate operator for row A1g — is the unproven half.
- The REMOTE-driven expansion — the operator's release on the owner's own items, which is what row A1g
  asks for — is judged by that batch's three-client run and not here. Two staging facts belong to it: the
  key is held (and the declaration loaded) on the OPERATOR's client, because the kind is produced by that
  client's own native loop, and the operator's proxy tree must carry the dragged container's children or
  the loop produces no intent at all. Only that run can say.
- A hold is judged one step later by construction: an input cannot read the state its own message
  produces (measured). Every hold and every release therefore needs the `read` step that follows it, and
  a run that skips it has no verdict.
- This install selects the Mono.CSharp evaluator. The same local HotRepl plugin also ships a Roslyn
  scripting evaluator, which would take a declaration as a different input shape; nothing here pins that.
- The recipe's KeyCode→virtual-key mapping covers every key the game's own bind table names plus the
  navigation keys, and the scan code is asked of the client's own layout for the key the bind resolved to
  — left/right-specific codes included, whose answers differ (`VK_LSHIFT` 0x2A against `VK_RSHIFT` 0x36).
  An unmapped bind is refused with its own name rather than queued as a guess; no bind in the game's table
  is in that state today. Whether Unity's legacy input reads the scan code at all was not established:
  the smoke exercised LeftShift with the correct value and nothing else.
- The review that closed this cycle found the scan-code line asking the layout for the GENERIC key on a
  left/right-specific bind, which the layout answers for directly. The line now passes the resolved key;
  for the key the smoke exercised the value is identical (`VK_LSHIFT` and `VK_SHIFT` both answer 0x2A, and
  the artifacts above were produced with 0x2A), so the smoke's readings still describe the committed
  recipe, and the change only affects the right-hand codes no run has driven.

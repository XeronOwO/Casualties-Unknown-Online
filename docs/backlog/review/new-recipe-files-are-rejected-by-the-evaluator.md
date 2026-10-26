# A new recipe file is rejected by the client's evaluator as `(1,1) InteractiveHost`

- Status: Review — not reproducible on 2026-10-26: a new multi-line recipe runs; the ticket's own case stays unexplained, and the two failure shapes that DO explain the symptom are recorded below. (original note: cause measured 2026-10-25: a MULTI-LINE body is refused, a one-line body runs; the working shape is now committed as `tools/acceptance/recipes/ui-raycast-at.cs`, which ran four times in one batch.
- Priority: High (tooling: it blocks every new acceptance instrument)
- Category: Acceptance tooling / eval channel
- Source: measured in batch `.acceptance/batch-menu2`, 2026-10-25 20:4x.

## Design

No corpus page applies: this is the acceptance tooling's own channel, and the record it is judged against is the driver's contract in
`docs/acceptance/workflow.md` (which states a recipe is one committed file run as a single eval) together with the
`docs/acceptance/AGENTS.md` rule that an instrument is committed rather than improvised. The instrument this ticket unblocks is
`docs/design/surfaces/member-menu.md`'s pointer rule, which cannot be judged without it.

## Problem

**A newly added recipe cannot be run, while every existing recipe still can.** The client's evaluator answers
`eval 'recipe:<name>' failed: (1,1): InteractiveHost` for the new file, and the same body appended to an existing recipe's header fails the
same way, while `item-provide`, `craft-drive` and a byte-copy of `craft-drive` under a new name all run and answer their own payloads.

Measured, in this order, on one client:

| What was sent | Answer |
|---|---|
| `craft-drive mode=list,index=0` | ran; its own `no-recipes` payload |
| a byte-copy of `craft-drive.cs` named `zz-copy-test.cs` (same `// recipe: craft-drive` header) | ran; same payload |
| a 3-line recipe (`zz-tiny.cs`, `// recipe: zz-tiny`, one `return`) | **`(1,1): InteractiveHost`** |
| the same 3-line body after the fix attempts below | **`(1,1): InteractiveHost`** |

What was ruled out by measurement, not by guessing:

- **Encoding.** The new file is pure ASCII, starts `2F 2F 20` (no BOM), and the bytes were dumped: no BOM, no NUL, no non-ASCII byte.
- **Line endings.** The new file is CRLF, byte-identical in convention to `item-provide.cs`; converting it made no difference.
- **The argument contract.** The driver's own validation works (it refused an argument the recipe does not use, and refused a value for a
  recipe with no `// args:` line), so the failure is downstream of the substitution, in the client.
- **Client state.** A fresh client (both clients restarted, new batch) answers the same, while `item-provide` runs on that same fresh client.
- **The `// args:` / `// serves:` header lines.** Adding them made no difference.

## Why it matters

The ticket it blocked needs the game's own pointer-over-UI answer at a chosen point, which is one small recipe
(`EventSystem.RaycastAll` at a point, then read each hit's layer against `LayerMask.NameToLayer("UI")`). Without new recipes, every future
instrument has to be smuggled into an existing file or into an eval declaration — which is exactly the kind of workaround the delivery rules call
a patch over a cause.

## What the fix must establish

1. What the client's evaluator actually receives for a new file (the driver builds one string: `Expand-RecipeCode` → `Invoke-DriverCode`), and
   where the difference from an existing file enters.
2. Why `(1,1)` — the submission is refused as a whole, so the answer names a position in something the recipe does not contain. The most likely
   shape is a wrapper the client adds around the submission (a REPL statement terminator, a `using` set, or a length/format rule), which the
   existing recipes satisfy by their size or shape rather than on purpose.
3. A regression test that pins it: a minimal recipe committed under `tools/acceptance/recipes/` must run, because the NEXT instrument will be
   written the same way.

## The cause, measured 2026-10-25 (second batch)

The channel is fine; the SHAPE is not. A recipe body written as a multi-line block lambda is refused as `(1,1): InteractiveHost`, and the same
body written on ONE line runs:

| Body shape | Answer |
|---|---|
| `((System.Func<string>)(() => {` / newline / `return ...;` / newline / `})())` | **`(1,1): InteractiveHost`** |
| `((System.Func<string>)(() => { var a = ...; var b = ...; return "..." + a; }))()` on one line | runs; answers its payload |
| `((System.Func<string>)(() => "literal"))()` on one line | runs |

Two experiments pinned it: the same minimal body under `item-provide.cs`'s own header (so the header is not the variable) failed, and the one-line
version of the SAME statements — a real EventSystem raycast with five locals — ran and returned `hitCount=12, first="Viewport", firstLayer=5`.

So the rule the next author needs is: **a recipe is ONE line** (statements are fine inside the block, newlines are not), and the reason is the
Mono.CSharp REPL the driver documents as evaluating "one top-level expression": a multi-line submission is read line by line, so a body that opens a
block on one line and closes it on another is never a complete expression to it. The long recipes in the tree are multi-line, which is why the
failure looked like "new files" rather than "new shape" — they predate this and evidently reach the evaluator through a path that buffers
incomplete input (a difference worth confirming when this ticket's third row lands).

## Acceptance rows

| Row | The check |
|---|---|
| 1 | A five-line recipe added in a commit runs on a fresh client and answers its own payload |
| 2 | Every existing recipe still runs unchanged (a batch that runs three of them, one with args and one without) |
| 3 | The cause is written down where the next instrument's author reads it (`docs/acceptance/workflow.md`) |

## Counter-example measured 2026-10-26: a new file DOES run

`tools/acceptance/recipes/use-request.cs` was created for the sleeping-target ticket (cross-player use requests) and runs on the first try, and a
three-line probe written the same way answers its own payload:

```
// recipe: zz-tiny-test   (created by the editor tool, first bytes 2F 2F 20 = "// ", no BOM)
{"ok": true, "note": "hello"}
```

Both are NEW files, multi-line, with `// args:` and `// returns:` headers. So "a new recipe file is rejected" is not the channel's behaviour.

The two shapes that DO produce the symptom, both hit in that same session and both now recorded:

1. **a BOM.** `docs/acceptance/AGENTS.md` already states it: a BOM fails the client's evaluator as `(1,1): InteractiveHost`. A file written by
   PowerShell's `[System.IO.File]::WriteAllText` in .NET Framework carries one; the editor tool does not.
2. **a body whose last expression is the lambda instead of its invocation.** The recipe must end with `}))()`; without it the driver receives a
   `Func<string>` and fails to parse the answer (`Invalid JSON primitive` on the first character of `System.Func…`).

What the ticket''s original case was, nobody can now say — the failing file is gone and its byte dump showed no BOM. What can be said is that the
channel accepts new files, so the rule "an instrument is committed, not improvised" is not blocked by it.
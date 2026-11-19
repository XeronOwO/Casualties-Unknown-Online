# tools/ — deployment, contracts and acceptance tooling

- `deploy.ps1` needs an explicit `-GameDir`, refuses sandbox paths and never deploys BepInEx's own DLLs. `verify-deploy.ps1`
  checks the install against this tree's build output. Commit before deploying.
- `check-language.ps1` — committed content is English: reads the staged diff (or `-WorkingTree`), fails on any non-ASCII LETTER
  outside the documented exemptions (`docs/zh/`, `zh.json`, the pinyin dictionary, fixtures that must contain non-English text).
- `check-delivery.ps1` — the delivery checklist, run before a commit: validates every required box and its evidence, archives the
  cycle's record under `docs/evidence/checklists/`, resets the boxes for the next cycle.
- `act.ps1` — **the only way to enter a step**: `-Step <n>` prints that step's two rule files in full, arms the marker and records it.
  `-Step 0` opens the round's record first; another step refuses while no round is open.
- `require-active-step.ps1` — the gate that makes work IMPOSSIBLE without its step rules. `Directory.Build.targets` runs it before
  every build (so build and test refuse), `deploy.ps1` runs it before touching the install, and the acceptance suite's build
  carries the source gate (`-Steps 7,8,13`). It demands a marker written by `act.ps1` in the last 45 minutes, for a step this
  action belongs to, whose rule file still has the SHA that was printed.
- `check-reading-evidence.ps1` — the gate against claiming without measuring: a ticket row marked pass must carry a digit or a
  quoted value outside its number and verdict cells, and a ticket marked Done must carry a number somewhere. Run by the commit hook
  beside the step gate.
- `step.ps1` — how a step is ENTERED: `tools/step.ps1 -Enter <n>` prints that step's two rule files in full and prints the STEP-TOKEN
  line the recorder demands. It exists because a rule that only lived in prose lost to the work every time (the gate below is the
  backstop).
- `check-step-read.ps1` — the step-read gate, run before a commit: records which step rule files the round read
  (`-Record '<step>' -Token '<STEP-TOKEN>'`, one SHA-256 per file) and refuses while a step the change TOUCHES has no record. The
  record is one round's: `-NewRound` (called by `act.ps1`) starts it over, so an earlier round's line is void. A `src/` change touches
  step 7, a `tests/` change 7 and 8, `docs/design/` step 2, `tools/` step 13, `docs/backlog/` step 12, and every commit step 11. It
  is a script and not a test because the record is per round, not per tree.
- `check-goal-completion.ps1` — the completion gate for an autonomous run: reads every `- Status:` line in `docs/backlog/todo/`
  and `docs/backlog/review/` and exits 1 while any ticket is still actionable. Run it as the last action before
  `update_goal complete`; its output is the reading that claim has to carry. A ticket is "parked" only when its own status says why
  nobody can do it alone — blocked on the user, on a human motion, on build capability, or on a fixture a batch cannot produce.
- `acceptance/host/` — the C# acceptance host, one command per file: `env` classifies the game's BepInEx tree by its marker DLL
  and refuses while a game runs; `preflight` is the read-only dependency check
  ([`../docs/acceptance/dependencies.md`](../docs/acceptance/dependencies.md)), its machine values from the gitignored
  `docs/acceptance/AGENTS.local.md`; `launch` reuses or starts a client and polls its endpoint; `run` builds the suite, launches,
  runs the batch and prints the report paths and wall; `capture` grabs one client window with PrintWindow; `input` is the ONE
  sanctioned OS-level input path, and only per batch with the user's explicit authorisation. The gesture `input` performs is read
  back from the clients' own logs and probes.
- `acceptance/driver/` — the in-process driver the suite's `drive` verb and every case use: real control ids on the Online UI's
  own actions, never OS-level input. `drive --action recipe --recipe <name>` runs one committed scenario from
  `acceptance/recipes/` (declared `--arg` values substituted before sending); `drive --action declare --declare <name>` loads one
  eval declaration from `acceptance/driver/eval-declarations/`. A recipe is UTF-8 **without BOM**; the nested-lambda,
  reflected-read and truncated-answer pitfalls are in [`../docs/acceptance/workflow.md`](../docs/acceptance/workflow.md).
- The former PowerShell acceptance scripts are deleted; their jobs live in the host commands and the suite's `drive` verb above.
- The contract toolchain snapshots a game build and classifies differences between two builds; update-day flow:
  [`../docs/development/game-update-runbook.md`](../docs/development/game-update-runbook.md).

Build, deploy and commit rules: [`../AGENTS.md`](../AGENTS.md).

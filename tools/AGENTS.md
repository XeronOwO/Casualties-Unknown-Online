# tools/ — deployment, contracts and acceptance tooling

- `deploy.ps1` needs an explicit `-GameDir`, refuses sandbox paths and never deploys BepInEx's own DLLs. `verify-deploy.ps1`
  checks the install against this tree's build output. Commit before deploying.
- `check-language.ps1` — committed content is English: reads the staged diff (or `-WorkingTree`), fails on any non-ASCII LETTER
  outside the documented exemptions (`docs/zh/`, `zh.json`, the pinyin dictionary, fixtures that must contain non-English text).
- `check-delivery.ps1` — the delivery checklist, run before a commit: validates every required box and its evidence, archives the
  cycle's record under `docs/evidence/checklists/`, resets the boxes for the next cycle.
- `acceptance/preflight.ps1` — the read-only dependency preflight of an acceptance run
  ([`../docs/acceptance/dependencies.md`](../docs/acceptance/dependencies.md)); it changes nothing and its machine values come from
  the gitignored `docs/acceptance/AGENTS.local.md`.
- `acceptance/drive-in-process.ps1` — drives a running client's Online UI through its in-process evaluator: real control ids on the
  UI's own actions, never OS-level input. `-Action recipe` runs one committed scenario from `acceptance/recipes/` (declared
  `-RecipeArg` values substituted before sending); `-Action declare` loads one eval declaration from
  `acceptance/driver/eval-declarations/` (idempotent, presence-probed). A recipe is UTF-8 **without BOM** (a BOM fails the client's
  evaluator as `(1,1): InteractiveHost`); a nested lambda that captures a local fails with no diagnostic — inline the value; a
  value a nested delegate must read is a literal inside it; a reflected member read that answers its default is a defect in the
  READ (try the property before the field). A large eval answer comes back truncated (driver exit 4) — ask for the small projection.
  Rules and pitfalls: [`../docs/acceptance/workflow.md`](../docs/acceptance/workflow.md).
- `acceptance/session-environment.ps1` — the BepInEx-tree gate: classifies a tree by its marker DLL (never a folder name), refuses
  while a game process runs, swaps only when the machine is free.
- `acceptance/send-input.ps1` — the ONE sanctioned OS-level input path, and only per batch with the user's explicit authorisation.
  It is for an acceptance BATCH, never a shipping feature's requirement; the gesture it performs is read back from the clients'
  own logs and probes.
- The contract toolchain snapshots a game build and classifies differences between two builds; update-day flow:
  [`../docs/development/game-update-runbook.md`](../docs/development/game-update-runbook.md).

Build, deploy and commit rules: [`../AGENTS.md`](../AGENTS.md).

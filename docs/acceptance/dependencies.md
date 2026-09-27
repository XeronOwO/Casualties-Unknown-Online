# Acceptance dependencies

Every capability an acceptance run can use, how the preflight detects it, what it unlocks, and what
happens when it is absent. The machine-specific values behind these checks live in
[AGENTS.local.md](AGENTS.local.md) in this directory (gitignored); this page never carries one.

`tools/acceptance/preflight.ps1` implements the detection column. It is read-only and prints one row
per dependency with a state of `present`, `missing`, `unknown` or `pending` (declared, helper not
built yet — staged work), plus the local facts it could not resolve. Exit code `0` means a full
two-client run is possible, `2` means at least one required capability is not present, `1` means the
preflight itself failed. `hotrepl`, `deploy` and `input` are reported but never decide the exit code.

## The table

| Id | Dependency | Capability it unlocks | Detection | Missing ⇒ |
|---|---|---|---|---|
| `steam` | Steam client, installed and startable, with the app id proved to be this install | launching a client that can initialise Steamworks — required even for the host | `steam-exe` resolves and `game-app-id` is cross-checked against the install: the install's own `steam_appid.txt` and the library's `appmanifest_<id>.acf` `installdir` (read for an install under a `steamapps\common` folder) must each confirm it, so every source that exists must agree; a running process is reported but not required, because the run starts the client. A source that contradicts is `missing`, an id no source can confirm is `unknown` — the launch line never runs on an unproven or contradicted id | the game cannot initialise Steamworks, or the launch id names a different app: every row is `blocked`; the fact and the install are made to agree before any launch — the app manifest is Steam's own record of what the launch line resolves, the install's own file what the build declares — and the user is asked when they disagree without an obvious wrong side |
| `game` | the game install with its doorstop and BepInEx | the world itself | `<game-dir>` resolves and holds the game executable, the doorstop DLL and `BepInEx/` | nothing can be observed: every row is `blocked`; ask the user |
| `deploy` | the ticket's build deployed to the physical install | evidence about the right code | the deployed plugin DLL exists; its `ProductVersion`, its write time and the repository's `HEAD` are reported together, and the workflow compares them (a documentation-only commit moves `HEAD` without changing the artifact) | build and deploy first (workflow §4); if the deploy is refused (game running, sandbox path), that refusal is the blocker |
| `sandboxie` | Sandboxie's second-client environment | the guest half of every two-client row | `sandboxie-exe` resolves — or is derived from the running `SbieSvc` image path when the fact is unset — the guest sandbox root resolves, `sandbox-alt-root` resolves when it is set, and both `SbieSvc` and `SbieDrv` are present | single-client rows only; every guest-to-host and host-to-guest row is `blocked`; ask the user |
| `hotrepl` | the in-process evaluator in a running client | probes, forced setups, live state reads and assertions | the plugin directory exists under the game's `BepInEx/plugins/`, and the endpoint (`hotrepl-host-url`, `hotrepl-guest-url`) accepts a connection once the client runs | log-based evidence only; rows that need an assertion inside the process are `blocked` |
| `dotnet` | the .NET SDK | building the commit under acceptance, and the gate suite | `dotnet --version` succeeds | no deployable artifact: every row is `blocked` |
| `capture` | screen capture from the interactive desktop | `visual` rows — frames the agent reads | the .NET drawing stack is available and the session is interactive (not a locked or disconnected desktop) | every `visual` row is `blocked`; `machine` rows still run |
| `input` | in-process probe driving — the evaluator invoked from inside the client, never OS-level keyboard or mouse (the user's boundary) | reproducing a scenario without a person at the keyboard: entering the world, creating or joining a lobby, starting a run, opening a panel — all through the game's own entry points | the in-process driver helper exists under `tools/acceptance/` (staged — see the ticket referenced from `workflow.md`); it drives the evaluator channel the `hotrepl` row reports | a driven setup is not reproducible: rows whose scenario needs one are `blocked`, and replacing them with OS input is neither available nor permitted — never do it |
| `logs` | the three log channels (BepInEx loader log, BepInEx runtime log, CUO's own log) | `machine` rows' evidence | the BepInEx log root resolves under the game install, and under the sandbox guest root when that fact is set; the per-run files appear once a client starts | `machine` rows that depend on a log line are `blocked` |
| `artifacts` | a writable local directory for frames, recordings and probe dumps | keeping heavy evidence out of git | `acceptance-artifacts-dir` resolves; the run creates it when it is missing, and writability is proven by the first artifact written — the preflight stays read-only | run without capture artifacts: `visual` and `feel` rows are `blocked` |

Two further report rows are inputs rather than capabilities: `facts` (the local facts file itself) and
`fact` (one row per required key that `FACT-MISSING` names). Both are `missing` only when the facts file
or a key is absent, and both are answered by asking the user for the value.

## The local-facts contract

The preflight resolves its machine values from [AGENTS.local.md](AGENTS.local.md), section
`验收环境 / acceptance environment`, as `- <key>: <value>` lines. A key that is absent is reported as
`FACT-MISSING` and is a question for the user, not something to guess. Everything after the colon IS
the value, taken literally: a note appended to it makes the value wrong, and the launch line that
consumes the id directly would break the same way. Put a note on a line of its own, outside the
`- <key>:` pattern.

| Key | Required | Used by |
|---|---|---|
| `game-dir` | yes | `game`, `deploy`, `logs`, the deploy and verify scripts |
| `game-app-id` | yes | launching the host client through Steam |
| `steam-exe` | yes | `steam` |
| `sandboxie-exe` | yes | `sandboxie` |
| `sandbox-guest-root` | yes | `sandboxie`, the guest's deploy-freshness check and logs |
| `sandbox-alt-root` | no | reported when set: the alternate guest sandbox, checked only for existence |
| `hotrepl-host-url` | when probes are used | `hotrepl` |
| `hotrepl-guest-url` | when probes are used | `hotrepl` |
| `acceptance-artifacts-dir` | yes | `artifacts` |

`AGENTS.local.md` is gitignored; it is the only place a machine path may appear. See the shadow rule
recorded there before a sandboxed client starts: a sandbox copy older than the physical install runs
old code, and a stale shadow is removed file by file, never recursively.
## Absent, unknown, unusable

- **Absent** — the preflight proves the dependency is not there. The agent asks the user, records the
  blocker on the ticket, and marks the dependent rows `blocked`. This is the case the workflow is
  built around: a missing Steam, game, Sandboxie or HotRepl must surface as a question, never as a
  silently weaker run.
- **Unknown** — the check cannot decide from outside the client (for example: Steam is installed and
  running, but the account is logged out; the evaluator's port is free because no client is up yet).
  `unknown` is not `present`: the run's first attempt is what settles it, and a failure there is
  reported with the same question path.
- **Pending** — the capability is declared and its helper is still being built (the in-process driver until
  the harness lands). It is reported, never counted as missing, and the workflow says which evidence
  stands in for it meanwhile.
- **Stale** — the dependency is present but the artifact under acceptance is not the one deployed.
  This is not a missing dependency; it is workflow §4 (build, deploy, verify identity) before any row
  is judged.

## Asking the user

One question per run, in plain language, and it names the concrete options:

1. what is missing and which acceptance rows it blocks;
2. why acceptance needs it (one sentence: "without Sandboxie there is no second client, so nothing
   guest-side can be observed");
3. what can be done — install it, point the agent at an existing copy (the user gives the path, the
   agent records it in `AGENTS.local.md` and never in git), or run this ticket without that capability
   and keep the dependent rows `blocked`;
4. what the agent will do next either way: it records the blocker on the ticket, keeps the ticket in
   `review/`, and continues with other work rather than stalling the whole backlog.

Installing software, changing sandbox configuration or touching the user's Steam account is the
user's decision: the agent proposes, it does not perform it unasked.

# Acceptance record — The root README as a language pair, and English first in the documentation entry

- Ticket: `readme-language-pair` — verdict: **moved to `done/`**
- Batch: `20261012-c` — offline batch; scope and plan: `docs/evidence/acceptance/20261012-c-scope.md`
- Commit: `3bacc6fc` — the tree's HEAD; this batch changes no `src/`, `tests/` or `tools/` file
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+3bacc6fcd5574b3a0b9137b11c8efb5e91163d91` — deployed and hash-verified in this run (`tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"); no runtime row is exercised
- Run: 2026-10-12 — preflight and session-environment captured before the build; build, gate suite, behavioural suite, format, deploy and deploy verification after; no client was started, so host/guest do not apply
- Dependencies: `tools/acceptance/preflight.ps1` (11 present, "RESULT: OK - a full two-client run is possible"; `preflight.txt`) and `tools/acceptance/session-environment.ps1 -Mode status` (`active=cuo`, `game-running=false`; `session-environment.txt`)
- Artifacts: none — the run's logs and TRX results are under the directory named by `acceptance-artifacts-dir`

This is a documentation ticket: its rows are the paired pages and the pairing rule, so every row below is
read from the two pages, the pairing registry, the gate that guards it and the committed architecture of
the documentation blocks.

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The root README is a pair — `README.md` English (what a repository visitor and the hosting default land on) and `README.zh.md` Chinese — each opening with a one-line language switch to the other | `machine` | **pass** | `README.md`, read in this run, opens with the switch to its twin: `[中文](README.zh.md)`; `README.zh.md`, read in the same run, opens with the mirror line: `[English](README.md)`. Both switch lines are the third line of their page, above the purpose paragraph |
| 2 | `docs/README.md` is English first: its prose and its two sections lead with English, because the page routes a stranger who does not read Chinese | `machine` | **pass** | `docs/README.md`, read in this run — "This page only switches language: two mirrored trees, the same pages at the same paths." is the page's opening prose, above the Chinese paragraph; its "## English" section stands above "## 中文" |
| 3 | The model list names the current model in both pages, with the same facts: the code was written primarily with DeepSeek V4 Flash/Pro, DeepSeek V4.1 Flash is what development runs on now, and the Claude-Code-to-Harness history is unchanged | `machine` | **pass** | `README.md` — "**DeepSeek V4.1 Flash** is" (the model development runs on now, after the DeepSeek V4 Flash/Pro line); `README.zh.md` — "**DeepSeek V4.1 Flash** 是目前主要使用的开发模型" (the same three facts in Chinese, with the Harness history line unchanged below) |
| 4 | The root pair is deliberately NOT registered in the pair registry, and the decision is recorded rather than left implicit — the registry's gate binds the `docs/en` ↔ `docs/zh` page pair | `machine` | **pass** | `docs/standard/alignment.txt`, read in this run, opens "CUO pair registry — the English and Chinese page set, confirmed consistent at these contents." and every one of its registered rows pairs a `docs/en` page with its `docs/zh` twin — no row names the root README, which is the decision the ticket records; the gate that binds a registered pair and reports drift is `CasualtiesUnknownOnline.Tests.Tooling.NormativeGates.DocumentationTreeGateTests.TheAlignmentCheck_FlagsAnEditedSide`, `Passed` in this run |
| 5 | The two versions stay fact-for-fact equal: the same sections in the same order, the same links and commands, and the same acknowledgements and disclaimer | `machine` | **pass** | The two pages read in this run carry the same eight section headings in the same order — `README.md` "## Disclaimer" against `README.zh.md` "## 免责声明" as the last of the eight — and the same link set: 26 link targets each, identical except the language switch itself (`README.md` links to its twin, `README.zh.md` back), of which 14 are distinct repository-local targets in both |

## Residuals for the user

None for this run: the rows are page and contract facts read from the committed tree, and none asks for a
person's judgement. The user's own words from the ticket — English first for the benefit of readers who do
not read Chinese — are a direction this record takes as given, not a row it judges.

## Limits

- **No client was started.** Nothing here is a rendering, frame, live log or probe observation; every row
  is decided by inspection of the committed pages, the registry and the gate suite this run ran.
- **The "all 14 local targets resolve" check was not repeated.** This run re-derived the link set and its
  count from the two pages (14 distinct repository-local targets, identical in both, differing only in the
  switch target); whether each of the 14 still resolves on disk is the writing cycle's check, and this
  batch's reading was confined to the pages and the registries its rows name.
- **The registry's own hashes were not re-computed.** Row 4 reads the registry's coverage and the gate that
  reports its drift; `git hash-object` was not re-run against the registered pairs in this batch — the gate
  suite's alignment cases are green in this run (`gates.log`).

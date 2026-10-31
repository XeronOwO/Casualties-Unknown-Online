# Code sweep: every production file, block by block

The design corpus was audited against tickets (`README.md`) and then against the code (`code.md`), page by page. This file
is the other axis of that second audit: the CODE is walked block by block, so nothing is missed by only reading the pages.

A **block** is one directory of production code (a project root, or a subdirectory; `Runtime/Session` and
`GameState/Domains` are split one level deeper because they are the largest areas), giving 76 blocks over 2,289 files. Every
`src/**/*.cs` file outside an `obj/` directory belongs to exactly one block, and `CodeSweepCoverageGateTests` fails when a
file matches no block, when a block's declared count is wrong, or when a status is not one of the three below — so this table
cannot drift away from the tree.

| Status | Meaning |
|---|---|
| `unread` | not walked yet |
| `read` | the block was walked and its findings are in `code.md` (each row names the block) |
| `swept` | walked, and every behaviour it holds is stated on a corpus page or filed as a ticket |

## The blocks

| Block | Files | Status |
|---|---|---|
| `CasualtiesUnknownOnline.Abstractions` | 161 | read |
| `CasualtiesUnknownOnline.Application` | 1 | read |
| `CasualtiesUnknownOnline.Application/Kernel` | 31 | read |
| `CasualtiesUnknownOnline.BulkTransfer.Core` | 7 | read |
| `CasualtiesUnknownOnline.BulkTransfer.Core/Audio` | 1 | read |
| `CasualtiesUnknownOnline.GameAdapter` | 76 | read |
| `CasualtiesUnknownOnline.GameAdapter/Capabilities` | 6 | read |
| `CasualtiesUnknownOnline.GameAdapter/Character` | 95 | read |
| `CasualtiesUnknownOnline.GameAdapter/Content` | 36 | read |
| `CasualtiesUnknownOnline.GameAdapter/Items` | 73 | read |
| `CasualtiesUnknownOnline.GameAdapter/ModEffects` | 7 | read |
| `CasualtiesUnknownOnline.GameAdapter/ModStatus` | 3 | read |
| `CasualtiesUnknownOnline.GameAdapter/OnlineUi` | 28 | read |
| `CasualtiesUnknownOnline.GameAdapter/Patches` | 153 | read |
| `CasualtiesUnknownOnline.GameAdapter/Run` | 29 | read |
| `CasualtiesUnknownOnline.GameAdapter/Tutorial` | 3 | read |
| `CasualtiesUnknownOnline.GameAdapter/World` | 70 | read |
| `CasualtiesUnknownOnline.GameAdapter/WorldGen` | 18 | read |
| `CasualtiesUnknownOnline.GameState` | 20 | read |
| `CasualtiesUnknownOnline.GameState/Domains/Entities` | 19 | read |
| `CasualtiesUnknownOnline.GameState/Domains/Fluids` | 8 | read |
| `CasualtiesUnknownOnline.GameState/Domains/Items` | 32 | read |
| `CasualtiesUnknownOnline.GameState/Domains/Players` | 24 | read |
| `CasualtiesUnknownOnline.GameState/Domains/World` | 10 | read |
| `CasualtiesUnknownOnline.GameState/Domains/WorldEntities` | 19 | read |
| `CasualtiesUnknownOnline.GameState/Kernel` | 6 | read |
| `CasualtiesUnknownOnline.GameState/Projections` | 2 | read |
| `CasualtiesUnknownOnline.ModExample` | 4 | read |
| `CasualtiesUnknownOnline.Music` | 7 | read |
| `CasualtiesUnknownOnline.PinyinSearch` | 1 | read |
| `CasualtiesUnknownOnline.PinyinSearch.Core` | 5 | read |
| `CasualtiesUnknownOnline.PinyinSearch.Core/Search` | 6 | read |
| `CasualtiesUnknownOnline.PinyinSearch/Patches` | 2 | read |
| `CasualtiesUnknownOnline.Plugin` | 44 | read |
| `CasualtiesUnknownOnline.Protocol/Codecs` | 1 | read |
| `CasualtiesUnknownOnline.Protocol/Versioning` | 1 | read |
| `CasualtiesUnknownOnline.Protocol/Wire` | 55 | read |
| `CasualtiesUnknownOnline.RecursiveCrafting` | 9 | read |
| `CasualtiesUnknownOnline.RecursiveCrafting.Core` | 1 | read |
| `CasualtiesUnknownOnline.RecursiveCrafting.Core/Crafting` | 8 | read |
| `CasualtiesUnknownOnline.Runtime` | 3 | read |
| `CasualtiesUnknownOnline.Runtime/Composition` | 12 | read |
| `CasualtiesUnknownOnline.Runtime/Configuration` | 16 | read |
| `CasualtiesUnknownOnline.Runtime/Data` | 18 | read |
| `CasualtiesUnknownOnline.Runtime/Diagnostics` | 3 | read |
| `CasualtiesUnknownOnline.Runtime/GameAdapter` | 54 | read |
| `CasualtiesUnknownOnline.Runtime/Localization` | 4 | read |
| `CasualtiesUnknownOnline.Runtime/Logging` | 3 | read |
| `CasualtiesUnknownOnline.Runtime/Networking` | 14 | read |
| `CasualtiesUnknownOnline.Runtime/OnlineUi` | 54 | read |
| `CasualtiesUnknownOnline.Runtime/Patching` | 3 | read |
| `CasualtiesUnknownOnline.Runtime/Persistence` | 60 | read |
| `CasualtiesUnknownOnline.Runtime/Protocol` | 10 | read |
| `CasualtiesUnknownOnline.Runtime/Protocol/Messages` | 156 | read |
| `CasualtiesUnknownOnline.Runtime/Session` | 39 | read |
| `CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync` | 12 | read |
| `CasualtiesUnknownOnline.Runtime/Session/CharacterData` | 33 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Chat` | 4 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Commands` | 77 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Content` | 7 | read |
| `CasualtiesUnknownOnline.Runtime/Session/EntitySync` | 43 | read |
| `CasualtiesUnknownOnline.Runtime/Session/HandlerContexts` | 24 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Handlers` | 111 | read |
| `CasualtiesUnknownOnline.Runtime/Session/HostRules` | 16 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Items` | 64 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Mods` | 90 | read |
| `CasualtiesUnknownOnline.Runtime/Session/NetworkTraffic` | 7 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Permissions` | 5 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Persistence` | 44 | read |
| `CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction` | 121 | read |
| `CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth` | 4 | read |
| `CasualtiesUnknownOnline.Runtime/Session/Tutorial` | 2 | read |
| `CasualtiesUnknownOnline.Runtime/Session/World` | 124 | read |
| `CasualtiesUnknownOnline.Runtime/Steam` | 5 | read |
| `CasualtiesUnknownOnline.Runtime/Time` | 2 | read |
| `CasualtiesUnknownOnline.Runtime/World` | 1 | read |

## How a block is walked

1. List the block's files and read the ones that declare behaviour (types with doc comments, seams, policies, codecs).
2. For each behaviour: does a corpus page state it? If not, add it to the page (code → page). If a page states something
   the code does not do, record the verdict in `code.md` (`page-wrong`, `code-drift` or `needs-decision`).
3. Record the block's findings as `code.md` rows naming the block, and set the block's status here.

Progress: **all 76 blocks read, 2,289 production files in scope**, with the findings in `code.md`. A second pass (every block
`swept`: each behaviour it holds traced to a page or a ticket) is the next stage, and the native audit (`native.md`) runs
beside it once the user opens that scope.

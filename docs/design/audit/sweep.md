# Code sweep: every production file, block by block

The design corpus was audited against tickets (`README.md`) and then against the code (`code.md`), page by page. This file
is the other axis of that second audit: the CODE is walked block by block, so nothing is missed by only reading the pages.

A **block** is one directory of production code (a project root, or a subdirectory; `Runtime/Session` is split one level
deeper because it is the largest area). Every `src/**/*.cs` file outside an `obj/` directory belongs to exactly one block,
and `CodeSweepCoverageGateTests` fails when a file matches no block, when a block's declared count is wrong, or when a
status is not one of the three below — so this table cannot drift away from the tree.

| Status | Meaning |
|---|---|
| `unread` | not walked yet |
| `read` | the block was walked and its findings are in `code.md` (each row names the block) |
| `swept` | walked, and every behaviour it holds is stated on a corpus page or filed as a ticket |

## The blocks

| Block | Files | Status |
|---|---|---|
| `CasualtiesUnknownOnline.Abstractions` | 150 | unread |
| `CasualtiesUnknownOnline.Application` | 1 | unread |
| `CasualtiesUnknownOnline.Application/Kernel` | 31 | unread |
| `CasualtiesUnknownOnline.BulkTransfer.Core` | 7 | unread |
| `CasualtiesUnknownOnline.BulkTransfer.Core/Audio` | 1 | unread |
| `CasualtiesUnknownOnline.GameAdapter` | 74 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Capabilities` | 6 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Character` | 94 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Content` | 36 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Items` | 69 | unread |
| `CasualtiesUnknownOnline.GameAdapter/ModEffects` | 7 | unread |
| `CasualtiesUnknownOnline.GameAdapter/ModStatus` | 3 | unread |
| `CasualtiesUnknownOnline.GameAdapter/OnlineUi` | 28 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Patches` | 153 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Run` | 28 | unread |
| `CasualtiesUnknownOnline.GameAdapter/Tutorial` | 3 | unread |
| `CasualtiesUnknownOnline.GameAdapter/World` | 70 | unread |
| `CasualtiesUnknownOnline.GameAdapter/WorldGen` | 18 | unread |
| `CasualtiesUnknownOnline.GameState` | 20 | read |
| `CasualtiesUnknownOnline.GameState/Domains` | 112 | unread |
| `CasualtiesUnknownOnline.GameState/Kernel` | 6 | read |
| `CasualtiesUnknownOnline.GameState/Projections` | 2 | read |
| `CasualtiesUnknownOnline.ModExample` | 3 | unread |
| `CasualtiesUnknownOnline.Music` | 7 | unread |
| `CasualtiesUnknownOnline.PinyinSearch` | 1 | unread |
| `CasualtiesUnknownOnline.PinyinSearch.Core` | 5 | unread |
| `CasualtiesUnknownOnline.PinyinSearch.Core/Search` | 6 | unread |
| `CasualtiesUnknownOnline.PinyinSearch/Patches` | 2 | unread |
| `CasualtiesUnknownOnline.Plugin` | 43 | unread |
| `CasualtiesUnknownOnline.Protocol/Codecs` | 1 | read |
| `CasualtiesUnknownOnline.Protocol/Versioning` | 1 | read |
| `CasualtiesUnknownOnline.Protocol/Wire` | 55 | read |
| `CasualtiesUnknownOnline.RecursiveCrafting` | 9 | unread |
| `CasualtiesUnknownOnline.RecursiveCrafting.Core` | 1 | unread |
| `CasualtiesUnknownOnline.RecursiveCrafting.Core/Crafting` | 8 | unread |
| `CasualtiesUnknownOnline.Runtime` | 3 | unread |
| `CasualtiesUnknownOnline.Runtime/Composition` | 12 | unread |
| `CasualtiesUnknownOnline.Runtime/Configuration` | 16 | read |
| `CasualtiesUnknownOnline.Runtime/Data` | 18 | unread |
| `CasualtiesUnknownOnline.Runtime/Diagnostics` | 3 | unread |
| `CasualtiesUnknownOnline.Runtime/GameAdapter` | 52 | unread |
| `CasualtiesUnknownOnline.Runtime/Localization` | 4 | unread |
| `CasualtiesUnknownOnline.Runtime/Logging` | 3 | unread |
| `CasualtiesUnknownOnline.Runtime/Networking` | 14 | unread |
| `CasualtiesUnknownOnline.Runtime/OnlineUi` | 54 | unread |
| `CasualtiesUnknownOnline.Runtime/Patching` | 3 | unread |
| `CasualtiesUnknownOnline.Runtime/Persistence` | 60 | unread |
| `CasualtiesUnknownOnline.Runtime/Protocol` | 165 | unread |
| `CasualtiesUnknownOnline.Runtime/Session` | 39 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/AdaptiveSync` | 12 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/CharacterData` | 33 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Chat` | 4 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Commands` | 66 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Content` | 7 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/EntitySync` | 43 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/HandlerContexts` | 24 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Handlers` | 111 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/HostRules` | 16 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Items` | 63 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Mods` | 89 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/NetworkTraffic` | 7 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Permissions` | 5 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Persistence` | 44 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction` | 120 | read |
| `CasualtiesUnknownOnline.Runtime/Session/ProjectionHealth` | 4 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/Tutorial` | 2 | unread |
| `CasualtiesUnknownOnline.Runtime/Session/World` | 124 | unread |
| `CasualtiesUnknownOnline.Runtime/Steam` | 5 | unread |
| `CasualtiesUnknownOnline.Runtime/Time` | 2 | unread |
| `CasualtiesUnknownOnline.Runtime/World` | 1 | unread |

## How a block is walked

1. List the block's files and read the ones that declare behaviour (types with doc comments, seams, policies, codecs).
2. For each behaviour: does a corpus page state it? If not, add it to the page (code → page). If a page states something
   the code does not do, record the verdict in `code.md` (`page-wrong`, `code-drift` or `needs-decision`).
3. Record the block's findings as `code.md` rows naming the block, and set the block's status here.

Progress is the status column: 2 of 70 blocks read, 2,289 production files in scope.

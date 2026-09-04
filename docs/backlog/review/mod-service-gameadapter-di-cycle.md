# ModService ↔ GameAdapter DI cycle

- Status: Review
- Type: Bug / startup
- Category: Dependency injection / composition root
- Source: user report 2026-09-04 after deploying the world-space ragdoll fix — the game hung at startup with no error past the character-data load.

ModService injects the Game Adapter through the `IModEntitySpawner`, `IModItemSpawner`, `IModTilePlacer`, `IModStructurePlacer`, `IModLiquidPlacer`, and `IModNativeApiProvider` seams. The production replacement maps those interfaces to `GameAdapterImpl`; `GameAdapterImpl` also depended on `ModService` for its status-store projections. That made DI recurse while building the composition root.

Fixed by registering `ModStatusStore` as its own singleton and injecting only that store into `GameAdapter`/`GameAdapterDomains`. The adapter no longer depends on the whole ModService, so ModService → IMod* → GameAdapter → ModStatusStore is acyclic. Regression guard: `ModServiceDiCycleContractTests.GameAdapter_DoesNotDependOnModService_AndUsesStatusStore`.

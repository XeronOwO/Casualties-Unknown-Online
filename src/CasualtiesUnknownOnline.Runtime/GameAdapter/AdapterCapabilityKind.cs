namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Required/Optional class of one adapter capability (user ruling
/// 2026-09-20, <c>docs/backlog/todo/adapter-capability-catalog.md</c>). The
/// yardstick is whether the vanilla game itself has the feature, never how hard
/// the capability is to fix:
/// <list type="bullet">
/// <item><see cref="Required"/> — a feature the game itself has (items,
/// medical, crafting, world generation, the session itself). A broken contract
/// refuses multiplayer as a whole, visibly.</item>
/// <item><see cref="Optional"/> — a feature CUO adds on top of the vanilla game
/// (the mod content surface, diagnostics). These may degrade to
/// off by themselves while the rest of the session keeps working.</item>
/// </list>
/// Stage 1 records the class in the catalog and prints it in the probe report;
/// the install gate stays all-or-nothing until stage 2 splits it by this class.
/// </summary>
internal enum AdapterCapabilityKind
{
	Required,
	Optional,
}

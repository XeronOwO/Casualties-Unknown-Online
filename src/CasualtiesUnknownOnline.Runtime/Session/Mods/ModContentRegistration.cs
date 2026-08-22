using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One content entry in the framework-wide read view: the owning mod id plus
/// the mod-scoped definition. Returning this to consumers keeps the per-mod
/// namespace explicit without leaking the ModService internals.
/// </summary>
public sealed record ModContentRegistration(string ModId, ModContentDefinition Definition);

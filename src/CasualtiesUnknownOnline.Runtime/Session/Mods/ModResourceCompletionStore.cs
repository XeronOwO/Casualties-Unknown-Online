using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The ephemeral stage table a mod's completion registrations land in. It owns
/// the primitive list and nothing else: the per-mod
/// <c>IModResourceCompletion</c> adapter applies the scoping, validation and
/// cap, and the console's catalog reads the table when it ranks completions.
///
/// The indirection is what keeps the dependency direction honest — the catalog
/// already reaches into the mod domain through <c>ModContentResourceLocationSource</c>
/// (which needs <c>IModContentControl</c>, i.e. the mod service), so a direct
/// mod-service → catalog edge would close a dependency cycle in the composition
/// root. The type is public only because it appears in the public constructors
/// of the services that carry it (the same reason
/// <see cref="ModBuildingRuntimeStore"/> is); its members stay internal. Stages
/// are process-local and add no wire surface.
/// </summary>
public sealed class ModResourceCompletionStore
{
	private readonly List<IResourceLocationMatchStage> _stages = [];

	/// <summary>
	/// A snapshot of the registered stages, in registration order. It is a COPY on
	/// purpose: the completion query enumerates it while a stage — third-party
	/// code — may register or unregister during that very enumeration, and a query
	/// must see the table as it was when the query started instead of failing on a
	/// modified collection. The next query sees the change.
	/// </summary>
	internal IReadOnlyList<IResourceLocationMatchStage> Stages => [.. _stages];

	internal void Add(IResourceLocationMatchStage stage) => _stages.Add(stage);

	internal bool Remove(IResourceLocationMatchStage stage) => _stages.Remove(stage);
}

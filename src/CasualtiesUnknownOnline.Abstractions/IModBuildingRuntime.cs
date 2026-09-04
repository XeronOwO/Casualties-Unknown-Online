using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-mod runtime building hook surface. It is the CUO-safe replacement
/// for CUCoreLib's <c>ConfigurePrefab</c> / <c>ConfigureInstance</c> callbacks:
/// instead of receiving a live <c>GameObject</c>, a mod registers a hook that
/// receives a plain request and returns component type names for the Game
/// Adapter to attach.
///
/// Hooks are process-local and add no wire surface. The Game Adapter only
/// consults the hook table of the mod that owns the custom building content
/// definition; a non-owner registration is stored but not applied. Hooks may
/// be registered before or after the building content is bound.
/// </summary>
public interface IModBuildingRuntime
{
	/// <summary>
	/// Register the hook that runs once when the runtime template for a custom
	/// building is created. The returned component type names are attached to
	/// the inactive template before it is cached. Returns false for a null hook,
	/// an invalid/over-long building id, a duplicate registration, or a per-mod
	/// hook cap.
	/// </summary>
	bool TryRegisterPrefabHook(string buildingId, Func<ModBuildingPrefabRequest, IReadOnlyList<string>?> hook);

	/// <summary>
	/// Register the hook that runs for every runtime instance materialized from
	/// a custom building template. The returned component type names are
	/// attached to the instance before it becomes active. Returns false for a
	/// null hook, an invalid/over-long building id, a duplicate registration, or
	/// a per-mod hook cap.
	/// </summary>
	bool TryRegisterInstanceHook(string buildingId, Func<ModBuildingInstanceRequest, IReadOnlyList<string>?> hook);

	/// <summary>Remove a previously registered prefab hook. Returns false when no hook exists for the id.</summary>
	bool TryUnregisterPrefabHook(string buildingId);

	/// <summary>Remove a previously registered instance hook. Returns false when no hook exists for the id.</summary>
	bool TryUnregisterInstanceHook(string buildingId);

	/// <summary>True when this mod has a prefab hook registered for the building id.</summary>
	bool HasPrefabHook(string buildingId);

	/// <summary>True when this mod has an instance hook registered for the building id.</summary>
	bool HasInstanceHook(string buildingId);

	/// <summary>All building ids that currently have a prefab hook (copy — safe to hold).</summary>
	IReadOnlyCollection<string> PrefabHookBuildingIds { get; }

	/// <summary>All building ids that currently have an instance hook (copy — safe to hold).</summary>
	IReadOnlyCollection<string> InstanceHookBuildingIds { get; }

	/// <summary>The number of prefab hooks registered by this mod.</summary>
	int PrefabHookCount { get; }

	/// <summary>The number of instance hooks registered by this mod.</summary>
	int InstanceHookCount { get; }
}

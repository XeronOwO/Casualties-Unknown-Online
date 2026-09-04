using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The per-mod <see cref="IModBuildingRuntime"/> adapter. It scopes the hook
/// tables to one mod id and applies the same light validation used by the
/// runtime status surface; the primitive table lives in
/// <see cref="ModBuildingRuntimeStore"/>. This is a separate top-level type so
/// the store stays under the architecture gate while keeping the adapter in the
/// mod-building domain.
/// </summary>
internal sealed class ModBuildingRuntimeAdapter(
	ModBuildingRuntimeStore store,
	ModManifest manifest,
	ILogger log) : IModBuildingRuntime
{
	public bool TryRegisterPrefabHook(string buildingId, Func<ModBuildingPrefabRequest, IReadOnlyList<string>?> hook)
	{
		if (hook is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null building prefab hook for {BuildingId} — refused.",
				manifest.Id, buildingId);
			return false;
		}

		if (!ModBuildingRuntimeStore.IsValidBuildingId(buildingId))
		{
			log.LogWarning("[Mods] {ModId} tried to register a building prefab hook with an invalid building id {BuildingId} — refused.",
				manifest.Id, buildingId);
			return false;
		}

		return store.TryRegisterPrefabHook(manifest.Id, buildingId, hook);
	}

	public bool TryRegisterInstanceHook(string buildingId, Func<ModBuildingInstanceRequest, IReadOnlyList<string>?> hook)
	{
		if (hook is null)
		{
			log.LogWarning("[Mods] {ModId} tried to register a null building instance hook for {BuildingId} — refused.",
				manifest.Id, buildingId);
			return false;
		}

		if (!ModBuildingRuntimeStore.IsValidBuildingId(buildingId))
		{
			log.LogWarning("[Mods] {ModId} tried to register a building instance hook with an invalid building id {BuildingId} — refused.",
				manifest.Id, buildingId);
			return false;
		}

		return store.TryRegisterInstanceHook(manifest.Id, buildingId, hook);
	}

	public bool TryUnregisterPrefabHook(string buildingId)
	{
		if (!ModBuildingRuntimeStore.IsValidBuildingId(buildingId))
		{
			return false;
		}

		return store.TryUnregisterPrefabHook(manifest.Id, buildingId);
	}

	public bool TryUnregisterInstanceHook(string buildingId)
	{
		if (!ModBuildingRuntimeStore.IsValidBuildingId(buildingId))
		{
			return false;
		}

		return store.TryUnregisterInstanceHook(manifest.Id, buildingId);
	}

	public bool HasPrefabHook(string buildingId) =>
		ModBuildingRuntimeStore.IsValidBuildingId(buildingId) && store.HasPrefabHook(manifest.Id, buildingId);

	public bool HasInstanceHook(string buildingId) =>
		ModBuildingRuntimeStore.IsValidBuildingId(buildingId) && store.HasInstanceHook(manifest.Id, buildingId);

	public IReadOnlyCollection<string> PrefabHookBuildingIds => store.GetPrefabHookBuildingIds(manifest.Id);

	public IReadOnlyCollection<string> InstanceHookBuildingIds => store.GetInstanceHookBuildingIds(manifest.Id);

	public int PrefabHookCount => store.GetPrefabHookCount(manifest.Id);

	public int InstanceHookCount => store.GetInstanceHookCount(manifest.Id);
}

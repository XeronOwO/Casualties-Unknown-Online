using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The ephemeral per-mod runtime building hook table. It owns the primitive
/// <c>mod id + building id → delegate</c> storage and defensive-copy mechanics;
/// the per-mod <see cref="IModBuildingRuntime"/> adapter applies validation.
/// Hooks are local-only presentation/content configuration and add no wire
/// surface. The Game Adapter is the only consumer that turns a hook's returned
/// component type names into Unity components.
/// </summary>
public sealed class ModBuildingRuntimeStore(ILogger<ModBuildingRuntimeStore> log)
{
	public const int MaxBuildingIdLength = 128;
	public const int MaxHooksPerMod = 1024;

	private readonly ILogger _log = log;
	private readonly Dictionary<string, Dictionary<string, Func<ModBuildingPrefabRequest, IReadOnlyList<string>?>>> _prefabHooks =
		[with(StringComparer.Ordinal)];
	private readonly Dictionary<string, Dictionary<string, Func<ModBuildingInstanceRequest, IReadOnlyList<string>?>>> _instanceHooks =
		[with(StringComparer.Ordinal)];

	internal bool TryRegisterPrefabHook(
		string modId,
		string buildingId,
		Func<ModBuildingPrefabRequest, IReadOnlyList<string>?> hook)
	{
		if (!IsValidBuildingId(buildingId) || hook is null)
		{
			return false;
		}

		var table = GetOrCreatePrefabTable(modId);
		if (table.ContainsKey(buildingId))
		{
			_log.LogWarning(
				"[Mods] {ModId} already registered a building prefab hook for {BuildingId} — duplicate refused.",
				modId, buildingId);
			return false;
		}

		if (!CanAddHook(table.Count))
		{
			_log.LogWarning(
				"[Mods] {ModId} reached the {Cap}-building-hook cap — prefab hook for {BuildingId} refused.",
				modId, MaxHooksPerMod, buildingId);
			return false;
		}

		table[buildingId] = hook;
		_log.LogInformation("[Mods] {ModId} registered a building prefab hook for {BuildingId}.", modId, buildingId);
		return true;
	}

	internal bool TryRegisterInstanceHook(
		string modId,
		string buildingId,
		Func<ModBuildingInstanceRequest, IReadOnlyList<string>?> hook)
	{
		if (!IsValidBuildingId(buildingId) || hook is null)
		{
			return false;
		}

		var table = GetOrCreateInstanceTable(modId);
		if (table.ContainsKey(buildingId))
		{
			_log.LogWarning(
				"[Mods] {ModId} already registered a building instance hook for {BuildingId} — duplicate refused.",
				modId, buildingId);
			return false;
		}

		if (!CanAddHook(table.Count))
		{
			_log.LogWarning(
				"[Mods] {ModId} reached the {Cap}-building-hook cap — instance hook for {BuildingId} refused.",
				modId, MaxHooksPerMod, buildingId);
			return false;
		}

		table[buildingId] = hook;
		_log.LogInformation("[Mods] {ModId} registered a building instance hook for {BuildingId}.", modId, buildingId);
		return true;
	}

	internal bool TryUnregisterPrefabHook(string modId, string buildingId)
	{
		if (!_prefabHooks.TryGetValue(modId, out var table) || !table.Remove(buildingId))
		{
			return false;
		}

		_log.LogInformation("[Mods] {ModId} unregistered the building prefab hook for {BuildingId}.", modId, buildingId);
		return true;
	}

	internal bool TryUnregisterInstanceHook(string modId, string buildingId)
	{
		if (!_instanceHooks.TryGetValue(modId, out var table) || !table.Remove(buildingId))
		{
			return false;
		}

		_log.LogInformation("[Mods] {ModId} unregistered the building instance hook for {BuildingId}.", modId, buildingId);
		return true;
	}

	internal bool HasPrefabHook(string modId, string buildingId) =>
		_prefabHooks.TryGetValue(modId, out var table) && table.ContainsKey(buildingId);

	internal bool HasInstanceHook(string modId, string buildingId) =>
		_instanceHooks.TryGetValue(modId, out var table) && table.ContainsKey(buildingId);

	internal bool TryGetPrefabHook(
		string modId,
		string buildingId,
		out Func<ModBuildingPrefabRequest, IReadOnlyList<string>?>? hook)
	{
		if (_prefabHooks.TryGetValue(modId, out var table) && table.TryGetValue(buildingId, out var found))
		{
			hook = found;
			return true;
		}

		hook = null;
		return false;
	}

	internal bool TryGetInstanceHook(
		string modId,
		string buildingId,
		out Func<ModBuildingInstanceRequest, IReadOnlyList<string>?>? hook)
	{
		if (_instanceHooks.TryGetValue(modId, out var table) && table.TryGetValue(buildingId, out var found))
		{
			hook = found;
			return true;
		}

		hook = null;
		return false;
	}

	internal IReadOnlyList<string> GetPrefabHookBuildingIds(string modId) =>
		_prefabHooks.TryGetValue(modId, out var table) ? [.. table.Keys] : [];

	internal IReadOnlyList<string> GetInstanceHookBuildingIds(string modId) =>
		_instanceHooks.TryGetValue(modId, out var table) ? [.. table.Keys] : [];

	internal int GetPrefabHookCount(string modId) =>
		_prefabHooks.TryGetValue(modId, out var table) ? table.Count : 0;

	internal int GetInstanceHookCount(string modId) =>
		_instanceHooks.TryGetValue(modId, out var table) ? table.Count : 0;

	internal static bool IsValidBuildingId(string? buildingId) =>
		!string.IsNullOrWhiteSpace(buildingId) && buildingId!.Length <= MaxBuildingIdLength;

	private static bool CanAddHook(int currentHookCount) => currentHookCount < MaxHooksPerMod;

	private Dictionary<string, Func<ModBuildingPrefabRequest, IReadOnlyList<string>?>> GetOrCreatePrefabTable(string modId)
	{
		if (!_prefabHooks.TryGetValue(modId, out var table))
		{
			table = [with(StringComparer.Ordinal)];
			_prefabHooks[modId] = table;
		}

		return table;
	}

	private Dictionary<string, Func<ModBuildingInstanceRequest, IReadOnlyList<string>?>> GetOrCreateInstanceTable(string modId)
	{
		if (!_instanceHooks.TryGetValue(modId, out var table))
		{
			table = [with(StringComparer.Ordinal)];
			_instanceHooks[modId] = table;
		}

		return table;
	}
}

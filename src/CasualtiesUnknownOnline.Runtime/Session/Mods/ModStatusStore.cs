using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The runtime status half of the mod-status domain (phase 1). It owns the
/// ephemeral per-mod status slot table and defensive-copy mechanics; the
/// per-mod <see cref="IModStatusRuntime"/> adapter applies role/scope gates.
///
/// This is NOT a vanilla game integration layer. The store only keeps opaque
/// mod payloads keyed by status id + player + optional limb slot. GameAdapter
/// remains the only layer that can translate a status into a vanilla body/limb
/// effect. No automatic replication is implemented here; shared values are
/// applied explicitly by the guest from a host-originated message.
/// </summary>
internal sealed class ModStatusStore(ILogger log)
{
	private readonly ILogger _log = log;
	private readonly Dictionary<string, Dictionary<string, ModStatusEntry>> _statuses =
		[with(StringComparer.Ordinal)];

	/// <summary>
	/// Raised after any stored status value is written or removed. It carries no
	/// payload because the GameAdapter projection consumer refreshes the whole
	/// local-player projection set from the store on each change; status changes
	/// are discrete and low-volume.
	/// </summary>
	internal event Action? StatusChanged;

	internal IModStatusRuntime CreateStatusAdapter(ModManifest manifest, SessionService session) =>
		new ModStatusAdapter(this, session, manifest, _log);

	// ---- Primitive status-table access (no role checks; the adapter gates them) ----

	internal bool TryDeclare(
		string modId,
		string statusId,
		ModStatusScope scope,
		ModDataScope runtimeScope,
		int schemaVersion,
		ModStatusProjectionKind projectionKind)
	{
		if (!ModStatusPolicy.IsValidStatusId(statusId))
		{
			_log.LogWarning("[Mods] {ModId} tried to declare runtime status with an invalid id {StatusId} — refused.",
				modId, statusId);
			return false;
		}

		if (!ModStatusPolicy.IsValidSchemaVersion(schemaVersion))
		{
			_log.LogWarning("[Mods] {ModId} tried to declare runtime status {StatusId} with invalid schema version {Version} — refused.",
				modId, statusId, schemaVersion);
			return false;
		}

		if (!ModStatusPolicy.IsValidProjectionKind(projectionKind, scope))
		{
			_log.LogWarning("[Mods] {ModId} tried to declare runtime status {StatusId} with projection kind {ProjectionKind} that does not match scope {Scope} — refused.",
				modId, statusId, projectionKind, scope);
			return false;
		}

		var table = GetOrCreate(modId);
		if (table.ContainsKey(statusId))
		{
			_log.LogWarning("[Mods] {ModId}/{StatusId} is already declared as runtime status — the duplicate is refused.",
				modId, statusId);
			return false;
		}

		if (!ModStatusPolicy.CanAddStatus(table.Count))
		{
			_log.LogWarning("[Mods] {ModId} reached the {Cap}-status runtime-status cap — {StatusId} refused.",
				modId, ModStatusPolicy.MaxStatusesPerMod, statusId);
			return false;
		}

		table[statusId] = new ModStatusEntry(scope, runtimeScope, schemaVersion, projectionKind);
		_log.LogInformation("[Mods] {ModId} declared runtime status {StatusId} ({Scope}, runtime {RuntimeScope}, schema {SchemaVersion}, projection {ProjectionKind}).",
			modId, statusId, scope, runtimeScope, schemaVersion, projectionKind);
		return true;
	}

	internal bool TryGetBodyValue(string modId, string statusId, ulong playerSteamId, out byte[]? value)
	{
		value = null;
		if (!TryGetEntry(modId, statusId, out var entry)
			|| entry.Scope != ModStatusScope.Body
			|| !entry.BodyValues.TryGetValue(playerSteamId, out var stored))
		{
			return false;
		}

		value = (byte[])stored.Clone();
		return true;
	}

	internal bool TrySetBodyValue(string modId, string statusId, ulong playerSteamId, byte[] value)
	{
		if (!ModStatusPolicy.IsValidValue(value) || !TryGetEntry(modId, statusId, out var entry)
			|| entry.Scope != ModStatusScope.Body)
		{
			_log.LogWarning("[Mods] {ModId} tried to write body runtime status {StatusId} with an invalid value/scope — refused.",
				modId, statusId);
			return false;
		}

		entry.BodyValues[playerSteamId] = (byte[])value.Clone();
		StatusChanged?.Invoke();
		return true;
	}

	internal bool TryRemoveBodyValue(string modId, string statusId, ulong playerSteamId)
	{
		if (!TryGetEntry(modId, statusId, out var entry) || entry.Scope != ModStatusScope.Body)
		{
			return false;
		}

		if (!entry.BodyValues.Remove(playerSteamId))
		{
			return false;
		}

		StatusChanged?.Invoke();
		return true;
	}

	internal bool TryGetLimbValue(string modId, string statusId, ulong playerSteamId, int limbSlot, out byte[]? value)
	{
		value = null;
		if (!TryGetEntry(modId, statusId, out var entry)
			|| entry.Scope != ModStatusScope.Limb
			|| !ModStatusPolicy.IsValidLimbSlot(limbSlot)
			|| !entry.LimbValues.TryGetValue(playerSteamId, out var limbs)
			|| !limbs.TryGetValue(limbSlot, out var stored))
		{
			return false;
		}

		value = (byte[])stored.Clone();
		return true;
	}

	internal bool TrySetLimbValue(string modId, string statusId, ulong playerSteamId, int limbSlot, byte[] value)
	{
		if (!ModStatusPolicy.IsValidValue(value)
			|| !TryGetEntry(modId, statusId, out var entry)
			|| entry.Scope != ModStatusScope.Limb
			|| !ModStatusPolicy.IsValidLimbSlot(limbSlot))
		{
			_log.LogWarning("[Mods] {ModId} tried to write limb runtime status {StatusId} with an invalid value/scope/slot — refused.",
				modId, statusId);
			return false;
		}

		if (!entry.LimbValues.TryGetValue(playerSteamId, out var limbs))
		{
			limbs = [];
			entry.LimbValues[playerSteamId] = limbs;
		}

		limbs[limbSlot] = (byte[])value.Clone();
		StatusChanged?.Invoke();
		return true;
	}

	internal bool TryRemoveLimbValue(string modId, string statusId, ulong playerSteamId, int limbSlot)
	{
		if (!TryGetEntry(modId, statusId, out var entry)
			|| entry.Scope != ModStatusScope.Limb
			|| !entry.LimbValues.TryGetValue(playerSteamId, out var limbs))
		{
			return false;
		}

		if (!limbs.Remove(limbSlot))
		{
			return false;
		}

		if (limbs.Count == 0)
		{
			entry.LimbValues.Remove(playerSteamId);
		}

		StatusChanged?.Invoke();
		return true;
	}

	internal bool TryGetScope(string modId, string statusId, out ModStatusScope scope)
	{
		scope = default;
		if (!TryGetEntry(modId, statusId, out var entry))
		{
			return false;
		}

		scope = entry.Scope;
		return true;
	}

	internal bool TryGetRuntimeScope(string modId, string statusId, out ModDataScope runtimeScope)
	{
		runtimeScope = default;
		if (!TryGetEntry(modId, statusId, out var entry))
		{
			return false;
		}

		runtimeScope = entry.RuntimeScope;
		return true;
	}

	internal bool TryGetSchemaVersion(string modId, string statusId, out int schemaVersion)
	{
		schemaVersion = 0;
		if (!TryGetEntry(modId, statusId, out var entry))
		{
			return false;
		}

		schemaVersion = entry.SchemaVersion;
		return true;
	}

	internal bool TryGetProjectionKind(string modId, string statusId, out ModStatusProjectionKind projectionKind)
	{
		projectionKind = ModStatusProjectionKind.None;
		if (!TryGetEntry(modId, statusId, out var entry))
		{
			return false;
		}

		projectionKind = entry.ProjectionKind;
		return true;
	}

	/// <summary>
	/// Snapshot every non-opaque projection value stored for one player. This is
	/// the GameAdapter-facing read seam: it returns defensive copies and all
	/// projection metadata needed to apply/remove the vanilla overlay without the
	/// GameAdapter reaching into the mod API or interpreting arbitrary statuses.
	/// </summary>
	internal IReadOnlyList<ModStatusProjectionSnapshot> GetProjectionSnapshots(ulong playerSteamId)
	{
		var snapshots = new List<ModStatusProjectionSnapshot>();
		foreach (var outer in _statuses)
		{
			var modId = outer.Key;
			foreach (var status in outer.Value)
			{
				var statusId = status.Key;
				var entry = status.Value;
				if (entry.ProjectionKind == ModStatusProjectionKind.None)
				{
					continue;
				}

				foreach (var bodyEntry in entry.BodyValues)
				{
					if (bodyEntry.Key != playerSteamId)
					{
						continue;
					}

					snapshots.Add(new ModStatusProjectionSnapshot(
						modId,
						statusId,
						entry.Scope,
						entry.RuntimeScope,
						entry.ProjectionKind,
						entry.SchemaVersion,
						bodyEntry.Key,
						-1,
						(byte[])bodyEntry.Value.Clone()));
				}

				foreach (var limbOwner in entry.LimbValues)
				{
					if (limbOwner.Key != playerSteamId)
					{
						continue;
					}

					foreach (var limbEntry in limbOwner.Value)
					{
						snapshots.Add(new ModStatusProjectionSnapshot(
							modId,
							statusId,
							entry.Scope,
							entry.RuntimeScope,
							entry.ProjectionKind,
							entry.SchemaVersion,
							limbOwner.Key,
							limbEntry.Key,
							(byte[])limbEntry.Value.Clone()));
					}
				}
			}
		}

		return snapshots;
	}

	/// <summary>
	/// List every runtime status value currently stored for one player,
	/// regardless of projection kind. This is the GameAdapter read seam for
	/// presentation-only features such as the vanilla moodle row: it returns
	/// status identity/scope/slot without exposing mod-owned payload bytes.
	/// </summary>
	internal IReadOnlyList<StatusPresence> GetStatusPresences(ulong playerSteamId)
	{
		var presences = new List<StatusPresence>();
		foreach (var outer in _statuses)
		{
			foreach (var status in outer.Value)
			{
				var entry = status.Value;
				if (entry.BodyValues.ContainsKey(playerSteamId))
				{
					presences.Add(new StatusPresence(outer.Key, status.Key, entry.Scope, -1));
				}

				if (entry.LimbValues.TryGetValue(playerSteamId, out var limbs))
				{
					foreach (var limbSlot in limbs.Keys)
					{
						presences.Add(new StatusPresence(outer.Key, status.Key, entry.Scope, limbSlot));
					}
				}
			}
		}

		return presences;
	}


	internal IReadOnlyList<string> GetStatusIds(string modId, Func<ModStatusEntry, bool> filter) =>
		_statuses.TryGetValue(modId, out var table)
			? [.. table.Where(p => filter(p.Value)).Select(p => p.Key)]
			: [];

	internal int GetStatusCount(string modId, Func<ModStatusEntry, bool> filter) =>
		_statuses.TryGetValue(modId, out var table) ? table.Values.Count(filter) : 0;

	private bool TryGetEntry(string modId, string statusId, out ModStatusEntry entry)
	{
		entry = null!;
		if (_statuses.TryGetValue(modId, out var table) && table.TryGetValue(statusId, out var found))
		{
			entry = found;
			return true;
		}

		return false;
	}

	private Dictionary<string, ModStatusEntry> GetOrCreate(string modId)
	{
		if (!_statuses.TryGetValue(modId, out var table))
		{
			table = [with(StringComparer.Ordinal)];
			_statuses[modId] = table;
		}

		return table;
	}

	/// <summary>One runtime status presence for a player and optional limb slot.</summary>
	internal sealed record StatusPresence(string ModId, string StatusId, ModStatusScope Scope, int LimbSlot);

	/// <summary>One mod's declared runtime status metadata + per-player value table.</summary>
	internal sealed class ModStatusEntry(
		ModStatusScope scope,
		ModDataScope runtimeScope,
		int schemaVersion,
		ModStatusProjectionKind projectionKind)
	{
		public ModStatusScope Scope { get; } = scope;

		public ModDataScope RuntimeScope { get; } = runtimeScope;

		public int SchemaVersion { get; } = schemaVersion;

		public ModStatusProjectionKind ProjectionKind { get; } = projectionKind;

		public Dictionary<ulong, byte[]> BodyValues { get; } = [];

		public Dictionary<ulong, Dictionary<int, byte[]>> LimbValues { get; } = [];
	}
}

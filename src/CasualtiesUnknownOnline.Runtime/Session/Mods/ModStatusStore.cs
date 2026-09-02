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

	internal IModStatusRuntime CreateStatusAdapter(ModManifest manifest, SessionService session) =>
		new ModStatusAdapter(this, session, manifest, _log);

	// ---- Primitive status-table access (no role checks; the adapter gates them) ----

	internal bool TryDeclare(
		string modId,
		string statusId,
		ModStatusScope scope,
		ModDataScope runtimeScope,
		int schemaVersion)
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

		table[statusId] = new ModStatusEntry(scope, runtimeScope, schemaVersion);
		_log.LogInformation("[Mods] {ModId} declared runtime status {StatusId} ({Scope}, runtime {RuntimeScope}, schema {SchemaVersion}).",
			modId, statusId, scope, runtimeScope, schemaVersion);
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
		return true;
	}

	internal bool TryRemoveBodyValue(string modId, string statusId, ulong playerSteamId)
	{
		if (!TryGetEntry(modId, statusId, out var entry) || entry.Scope != ModStatusScope.Body)
		{
			return false;
		}

		return entry.BodyValues.Remove(playerSteamId);
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

	/// <summary>One mod's declared runtime status metadata + per-player value table.</summary>
	internal sealed class ModStatusEntry(ModStatusScope scope, ModDataScope runtimeScope, int schemaVersion)
	{
		public ModStatusScope Scope { get; } = scope;

		public ModDataScope RuntimeScope { get; } = runtimeScope;

		public int SchemaVersion { get; } = schemaVersion;

		public Dictionary<ulong, byte[]> BodyValues { get; } = [];

		public Dictionary<ulong, Dictionary<int, byte[]>> LimbValues { get; } = [];
	}

	// ---- Per-mod API adapter ----

	private sealed class ModStatusAdapter(
		ModStatusStore store,
		SessionService session,
		ModManifest manifest,
		ILogger log) : IModStatusRuntime
	{
		public bool TryDeclare(string statusId, ModStatusScope scope, ModDataScope runtimeScope, int schemaVersion)
		{
			if (!ModStatusPolicy.IsValidRuntimeScopeFor(manifest, runtimeScope))
			{
				log.LogWarning("[Mods] {ModId} tried to declare runtime status {StatusId} with runtime scope {RuntimeScope} that is invalid for network mode {Mode} — refused.",
					manifest.Id, statusId, runtimeScope, manifest.NetworkMode);
				return false;
			}

			return store.TryDeclare(manifest.Id, statusId, scope, runtimeScope, schemaVersion);
		}

		public bool TryGetBodyStatus(string statusId, ulong playerSteamId, out byte[]? value)
		{
			value = null;
			if (!CanReadStatus(statusId))
			{
				return false;
			}

			return store.TryGetBodyValue(manifest.Id, statusId, playerSteamId, out value);
		}

		public bool TryGetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, out byte[]? value)
		{
			value = null;
			if (!CanReadStatus(statusId))
			{
				return false;
			}

			return store.TryGetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, out value);
		}

		public bool TrySetBodyStatus(string statusId, ulong playerSteamId, byte[] value)
		{
			if (!TryWriteGuard(statusId))
			{
				return false;
			}

			return store.TrySetBodyValue(manifest.Id, statusId, playerSteamId, value);
		}

		public bool TrySetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value)
		{
			if (!TryWriteGuard(statusId))
			{
				return false;
			}

			return store.TrySetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, value);
		}

		public bool TryApplyBodyStatus(string statusId, ulong playerSteamId, byte[] value, ulong senderSteamId)
		{
			if (!TryApplyGuard(statusId, senderSteamId))
			{
				return false;
			}

			return store.TrySetBodyValue(manifest.Id, statusId, playerSteamId, value);
		}

		public bool TryApplyLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value, ulong senderSteamId)
		{
			if (!TryApplyGuard(statusId, senderSteamId))
			{
				return false;
			}

			return store.TrySetLimbValue(manifest.Id, statusId, playerSteamId, limbSlot, value);
		}

		public bool TryRemoveBodyStatus(string statusId, ulong playerSteamId)
		{
			if (!TryRemoveGuard(statusId))
			{
				return false;
			}

			return store.TryRemoveBodyValue(manifest.Id, statusId, playerSteamId);
		}

		public bool TryRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot)
		{
			if (!TryRemoveGuard(statusId))
			{
				return false;
			}

			return store.TryRemoveLimbValue(manifest.Id, statusId, playerSteamId, limbSlot);
		}

		public bool TryGetScope(string statusId, out ModStatusScope scope)
		{
			scope = default;
			return CanReadStatus(statusId) && store.TryGetScope(manifest.Id, statusId, out scope);
		}

		public bool TryGetRuntimeScope(string statusId, out ModDataScope runtimeScope)
		{
			runtimeScope = default;
			return CanReadStatus(statusId) && store.TryGetRuntimeScope(manifest.Id, statusId, out runtimeScope);
		}

		public bool TryGetSchemaVersion(string statusId, out int schemaVersion)
		{
			schemaVersion = 0;
			return CanReadStatus(statusId) && store.TryGetSchemaVersion(manifest.Id, statusId, out schemaVersion);
		}

		public IReadOnlyCollection<string> StatusIds => store.GetStatusIds(manifest.Id, IsVisible);

		public int StatusCount => store.GetStatusCount(manifest.Id, IsVisible);

		private bool CanReadStatus(string statusId)
		{
			if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
			{
				return false;
			}

			if (runtimeScope == ModDataScope.HostAuthoritative && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to read host-authoritative runtime status {StatusId} on a guest copy — refused.",
					manifest.Id, statusId);
				return false;
			}

			return true;
		}

		private bool TryWriteGuard(string statusId)
		{
			if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
			{
				log.LogWarning("[Mods] {ModId} tried to write undeclared runtime status {StatusId} — refused.",
					manifest.Id, statusId);
				return false;
			}

			if (runtimeScope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to write {RuntimeScope} runtime status {StatusId} from a guest — refused.",
					manifest.Id, runtimeScope, statusId);
				return false;
			}

			return true;
		}

		private bool TryApplyGuard(string statusId, ulong senderSteamId)
		{
			if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
			{
				log.LogWarning("[Mods] {ModId} tried to apply undeclared runtime status {StatusId} — refused.",
					manifest.Id, statusId);
				return false;
			}

			if (runtimeScope != ModDataScope.Shared)
			{
				log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} as shared but its runtime scope is {RuntimeScope} — refused.",
					manifest.Id, statusId, runtimeScope);
				return false;
			}

			if (session.Role == SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} on the host — refused; the host writes with TrySet, not TryApply.",
					manifest.Id, statusId);
				return false;
			}

			if (senderSteamId != session.HostSteamId)
			{
				log.LogWarning("[Mods] {ModId} tried to apply runtime status {StatusId} from non-host sender {Sender} — refused.",
					manifest.Id, statusId, senderSteamId);
				return false;
			}

			return true;
		}

		private bool TryRemoveGuard(string statusId)
		{
			if (!store.TryGetRuntimeScope(manifest.Id, statusId, out var runtimeScope))
			{
				log.LogWarning("[Mods] {ModId} tried to remove undeclared runtime status {StatusId} — refused.",
					manifest.Id, statusId);
				return false;
			}

			if (runtimeScope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to remove {RuntimeScope} runtime status {StatusId} from a guest — refused.",
					manifest.Id, runtimeScope, statusId);
				return false;
			}

			return true;
		}

		private bool IsVisible(ModStatusEntry entry) =>
			entry.RuntimeScope != ModDataScope.HostAuthoritative || session.Role == SessionRole.Host;
	}
}

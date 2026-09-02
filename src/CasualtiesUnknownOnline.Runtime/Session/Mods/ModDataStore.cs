using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The runtime-data half of the Phase 4 Mod API / CUCoreLib migration seam.
/// This is NOT a persistence store and NOT a sync engine: it owns the
/// per-mod ephemeral slot table and the primitive defensive-copy mechanics,
/// leaving role/scope gatekeeping to the per-mod <see cref="IModData"/> adapter
/// (mirroring <see cref="ModStateStore"/>).
///
/// Shared values exist on host and guest slots only when the mod explicitly
/// applies a host-originated value through the adapter; the framework never
/// sends them. Host-authoritative values are intentionally kept only on the
/// host. This is a process-lifetime table; durable state belongs to
/// <see cref="ModStateStore"/>.
/// </summary>
internal sealed class ModDataStore(ILogger log)
{
	private readonly ILogger _log = log;
	private readonly Dictionary<string, Dictionary<string, ModDataSlot>> _slots =
		[with(StringComparer.Ordinal)];

	internal IModData CreateDataAdapter(ModManifest manifest, SessionService session) =>
		new ModDataAdapter(this, session, manifest, _log);

	// ---- Primitive slot-table access (no role checks; the adapter gates them) ----

	internal bool TryDeclare(string modId, string key, ModDataScope scope, int schemaVersion)
	{
		if (!ModDataPolicy.IsValidKey(key))
		{
			_log.LogWarning("[Mods] {ModId} tried to declare runtime data with an invalid key {Key} — refused.",
				modId, key);
			return false;
		}

		if (!ModDataPolicy.IsValidSchemaVersion(schemaVersion))
		{
			_log.LogWarning("[Mods] {ModId} tried to declare runtime data {Key} with invalid schema version {Version} — refused.",
				modId, key, schemaVersion);
			return false;
		}

		var table = GetOrCreate(modId);
		if (table.ContainsKey(key))
		{
			_log.LogWarning("[Mods] {ModId}/{Key} is already declared as runtime data — the duplicate is refused.",
				modId, key);
			return false;
		}

		if (!ModDataPolicy.CanAddSlot(table.Count))
		{
			_log.LogWarning("[Mods] {ModId} reached the {Cap}-slot runtime-data cap — {Key} refused.",
				modId, ModDataPolicy.MaxSlotsPerMod, key);
			return false;
		}

		table[key] = new ModDataSlot(scope, schemaVersion, null);
		_log.LogInformation("[Mods] {ModId} declared runtime data {Key} ({Scope}, schema {SchemaVersion}).",
			modId, key, scope, schemaVersion);
		return true;
	}

	internal bool TryGetValue(string modId, string key, out byte[]? value)
	{
		value = null;
		if (!TryGetSlot(modId, key, out var slot) || slot.Value is null)
		{
			return false;
		}

		value = (byte[])slot.Value.Clone();
		return true;
	}

	internal bool TrySetValue(string modId, string key, byte[] value)
	{
		if (!ModDataPolicy.IsValidKey(key) || !ModDataPolicy.IsValidValue(value))
		{
			_log.LogWarning("[Mods] {ModId} tried to write runtime data {Key} with an invalid key/value — refused.",
				modId, key);
			return false;
		}

		if (!TryGetSlot(modId, key, out var slot))
		{
			_log.LogWarning("[Mods] {ModId} tried to write undeclared runtime data {Key} — refused.",
				modId, key);
			return false;
		}

		slot.Value = (byte[])value.Clone();
		return true;
	}

	internal bool TryRemove(string modId, string key)
	{
		if (!_slots.TryGetValue(modId, out var table) || !table.Remove(key))
		{
			return false;
		}

		_log.LogInformation("[Mods] {ModId} removed runtime data {Key}.", modId, key);
		return true;
	}

	internal bool TryGetScope(string modId, string key, out ModDataScope scope)
	{
		scope = default;
		if (!TryGetSlot(modId, key, out var slot))
		{
			return false;
		}

		scope = slot.Scope;
		return true;
	}

	internal bool TryGetSchemaVersion(string modId, string key, out int schemaVersion)
	{
		schemaVersion = 0;
		if (!TryGetSlot(modId, key, out var slot))
		{
			return false;
		}

		schemaVersion = slot.SchemaVersion;
		return true;
	}

	internal IReadOnlyList<string> GetKeys(string modId) =>
		_slots.TryGetValue(modId, out var table) ? [.. table.Keys] : [];

	internal IReadOnlyList<string> GetKeys(string modId, Func<ModDataSlot, bool> filter) =>
		_slots.TryGetValue(modId, out var table)
			? [.. table.Where(p => filter(p.Value)).Select(p => p.Key)]
			: [];

	internal int GetCount(string modId) =>
		_slots.TryGetValue(modId, out var table) ? table.Count : 0;

	internal int GetCount(string modId, Func<ModDataSlot, bool> filter) =>
		_slots.TryGetValue(modId, out var table) ? table.Values.Count(filter) : 0;

	private bool TryGetSlot(string modId, string key, out ModDataSlot slot)
	{
		slot = null!;
		if (_slots.TryGetValue(modId, out var table) && table.TryGetValue(key, out var found))
		{
			slot = found;
			return true;
		}

		return false;
	}

	private Dictionary<string, ModDataSlot> GetOrCreate(string modId)
	{
		if (!_slots.TryGetValue(modId, out var table))
		{
			table = [with(StringComparer.Ordinal)];
			_slots[modId] = table;
		}

		return table;
	}

	/// <summary>One mod's declared runtime-data slot. Value is null until the first successful write/apply.</summary>
	internal sealed class ModDataSlot(ModDataScope scope, int schemaVersion, byte[]? value)
	{
		public ModDataScope Scope { get; } = scope;

		public int SchemaVersion { get; } = schemaVersion;

		public byte[]? Value { get; set; } = value is null ? null : (byte[])value.Clone();
	}

	// ---- Per-mod API adapter ----

	private sealed class ModDataAdapter(
		ModDataStore store,
		SessionService session,
		ModManifest manifest,
		ILogger log) : IModData
	{
		public bool TryDeclare(string key, ModDataScope scope, int schemaVersion)
		{
			if (!ModDataPolicy.IsValidScopeFor(manifest, scope))
			{
				log.LogWarning("[Mods] {ModId} tried to declare runtime data {Key} with scope {Scope} that is invalid for network mode {Mode} — refused.",
					manifest.Id, key, scope, manifest.NetworkMode);
				return false;
			}

			return store.TryDeclare(manifest.Id, key, scope, schemaVersion);
		}

		public bool TryGet(string key, out byte[]? value)
		{
			value = null;
			if (!store.TryGetScope(manifest.Id, key, out var scope))
			{
				return false;
			}

			if (scope == ModDataScope.HostAuthoritative && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to read host-authoritative runtime data {Key} on a guest copy — refused (no guest mirror).",
					manifest.Id, key);
				return false;
			}

			return store.TryGetValue(manifest.Id, key, out value);
		}

		public bool TrySet(string key, byte[] value)
		{
			if (!store.TryGetScope(manifest.Id, key, out var scope))
			{
				log.LogWarning("[Mods] {ModId} tried to write undeclared runtime data {Key} — refused.",
					manifest.Id, key);
				return false;
			}

			if (scope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to write {Scope} runtime data {Key} from a guest — refused; guests must request through commands/messages.",
					manifest.Id, scope, key);
				return false;
			}

			return store.TrySetValue(manifest.Id, key, value);
		}

		public bool TryApplyShared(string key, byte[] value, ulong senderSteamId)
		{
			if (!store.TryGetScope(manifest.Id, key, out var scope))
			{
				log.LogWarning("[Mods] {ModId} tried to apply undeclared runtime data {Key} — refused.",
					manifest.Id, key);
				return false;
			}

			if (scope != ModDataScope.Shared)
			{
				log.LogWarning("[Mods] {ModId} tried to apply runtime data {Key} as shared but its scope is {Scope} — refused.",
					manifest.Id, key, scope);
				return false;
			}

			if (session.Role == SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to apply shared runtime data {Key} on the host — refused; the host writes with TrySet, not TryApplyShared.",
					manifest.Id, key);
				return false;
			}

			if (senderSteamId != session.HostSteamId)
			{
				log.LogWarning("[Mods] {ModId} tried to apply shared runtime data {Key} from non-host sender {Sender} — refused.",
					manifest.Id, key, senderSteamId);
				return false;
			}

			return store.TrySetValue(manifest.Id, key, value);
		}

		public bool TryRemove(string key)
		{
			if (!store.TryGetScope(manifest.Id, key, out var scope))
			{
				return false;
			}

			if (scope != ModDataScope.LocalOnly && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to remove {Scope} runtime data {Key} from a guest — refused.",
					manifest.Id, scope, key);
				return false;
			}

			return store.TryRemove(manifest.Id, key);
		}

		public bool TryGetScope(string key, out ModDataScope scope)
		{
			scope = default;
			if (!store.TryGetScope(manifest.Id, key, out scope))
			{
				return false;
			}

			if (scope == ModDataScope.HostAuthoritative && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to read the scope of host-authoritative runtime data {Key} on a guest copy — refused.",
					manifest.Id, key);
				return false;
			}

			return true;
		}

		public bool TryGetSchemaVersion(string key, out int schemaVersion)
		{
			schemaVersion = 0;
			if (!store.TryGetScope(manifest.Id, key, out var scope))
			{
				return false;
			}

			if (scope == ModDataScope.HostAuthoritative && session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to read the schema of host-authoritative runtime data {Key} on a guest copy — refused.",
					manifest.Id, key);
				return false;
			}

			return store.TryGetSchemaVersion(manifest.Id, key, out schemaVersion);
		}

		public IReadOnlyCollection<string> Keys => store.GetKeys(manifest.Id, IsVisible);

		public int Count => store.GetCount(manifest.Id, IsVisible);

		private bool IsVisible(ModDataSlot slot) =>
			slot.Scope != ModDataScope.HostAuthoritative || session.Role == SessionRole.Host;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Mod-state half of <see cref="ModService"/> (Phase 4 Mod API remainder).
/// Each mod gets a per-id key/value store of opaque bytes. The host is the only
/// save authority, so writes (and host reads) are refused outside the host role
/// and require <see cref="ModPermission.WriteGameState"/> — a guest copy that
/// needs host state coordinates through the existing mod message/command
/// surfaces. The disk store is versioned, atomic and degrade-to-empty on
/// corruption; the in-memory table is loaded once at Initialize (before
/// discovery/Bind) and lives for the process.
/// </summary>
public sealed partial class ModService
{
	private readonly Dictionary<string, ModStateEntry> _modState = [];
	private bool _stateLoaded;

	private void LoadModState()
	{
		if (_stateLoaded)
		{
			return;
		}

		_stateLoaded = true;
		if (!_stateFile.TryLoad(out var entries))
		{
			_modState.Clear();
			return;
		}

		foreach (var entry in entries)
		{
			if (string.IsNullOrWhiteSpace(entry.ModId))
			{
				continue;
			}

			var values = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			foreach (var state in entry.States)
			{
				if (state is null || string.IsNullOrWhiteSpace(state.Key))
				{
					continue;
				}

				values[state.Key] = (byte[])state.Value.Clone();
			}

			_modState[entry.ModId] = new ModStateEntry
			{
				ModId = entry.ModId,
				ModVersion = entry.ModVersion,
				SchemaVersion = entry.SchemaVersion,
				Values = values,
			};
		}
	}

	// ---- Host gating + permission (called by ModStateAdapter) ----

	private bool EnsureHostStateRead(ModManifest manifest, string operation)
	{
		if (_session.Role == SessionRole.Host)
		{
			return true;
		}

		_log.LogWarning("[Mods] {ModId} tried to {Operation} mod state outside the host role — refused.",
			manifest.Id, operation);
		return false;
	}

	private bool EnsureHostStateWrite(ModManifest manifest, string operation)
	{
		if (_session.Role != SessionRole.Host)
		{
			_log.LogWarning("[Mods] {ModId} tried to {Operation} mod state outside the host role — refused.",
				manifest.Id, operation);
			return false;
		}

		if (!HasPermission(manifest, ModPermission.WriteGameState))
		{
			LogMissingPermission(manifest.Id, "WriteGameState");
			return false;
		}

		return true;
	}

	// ---- Primitive table access (no role checks — the adapter gates them) ----

	internal bool TryGetModStateValue(string modId, string key, out byte[]? value)
	{
		value = null;
		if (!_modState.TryGetValue(modId, out var entry) || !entry.Values.TryGetValue(key, out var stored))
		{
			return false;
		}

		value = (byte[])stored.Clone();
		return true;
	}

	internal IReadOnlyList<string> GetModStateKeys(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? [.. entry.Values.Keys] : [];

	internal int GetModStateCount(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? entry.Values.Count : 0;

	internal int GetModStateSchemaVersion(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? entry.SchemaVersion : 1;

	internal bool TrySetModStateSchemaVersion(ModManifest manifest, int schemaVersion)
	{
		if (schemaVersion < 1)
		{
			_log.LogWarning("[Mods] {ModId} tried to set a non-positive mod-state schema version {Version} — refused.",
				manifest.Id, schemaVersion);
			return false;
		}

		var entry = GetOrCreateModState(manifest);
		entry.SchemaVersion = schemaVersion;
		entry.ModVersion = manifest.Version;
		PersistModState();
		return true;
	}

	internal bool TrySetModState(ModManifest manifest, string key, byte[] value)
	{
		if (!ModStatePolicy.IsValidKey(key))
		{
			_log.LogWarning("[Mods] {ModId} tried to write an invalid mod-state key {Key} — refused.",
				manifest.Id, key);
			return false;
		}

		if (!ModStatePolicy.IsValidValue(value))
		{
			_log.LogWarning("[Mods] {ModId} tried to write a {Length}-byte mod-state value; the cap is {Cap} bytes — refused.",
				manifest.Id, value.Length, ModStatePolicy.MaxValueBytes);
			return false;
		}

		var entry = GetOrCreateModState(manifest);
		var isNew = !entry.Values.ContainsKey(key);
		if (isNew && !ModStatePolicy.CanAddKey(entry.Values.Count))
		{
			_log.LogWarning("[Mods] {ModId} reached the {Cap}-key mod-state cap — key {Key} refused.",
				manifest.Id, ModStatePolicy.MaxKeysPerMod, key);
			return false;
		}

		entry.Values[key] = (byte[])value.Clone();
		entry.ModVersion = manifest.Version;
		PersistModState();
		return true;
	}

	internal bool TryRemoveModState(ModManifest manifest, string key)
	{
		if (!_modState.TryGetValue(manifest.Id, out var entry) || !entry.Values.Remove(key))
		{
			return false;
		}

		entry.ModVersion = manifest.Version;
		PersistModState();
		return true;
	}

	internal bool TryClearModState(ModManifest manifest)
	{
		var entry = GetOrCreateModState(manifest);
		if (entry.Values.Count == 0)
		{
			return true;
		}

		entry.Values.Clear();
		entry.ModVersion = manifest.Version;
		PersistModState();
		return true;
	}

	private ModStateEntry GetOrCreateModState(ModManifest manifest)
	{
		if (!_modState.TryGetValue(manifest.Id, out var entry))
		{
			entry = new ModStateEntry
			{
				ModId = manifest.Id,
				ModVersion = manifest.Version,
				SchemaVersion = 1,
				Values = [],
			};
			_modState[manifest.Id] = entry;
		}

		return entry;
	}

	private void PersistModState()
	{
		if (!_stateFile.IsEnabled)
		{
			return;
		}

		var entries = _modState.Values.Select(e => new ModStateFile.Entry
		{
			ModId = e.ModId,
			ModVersion = e.ModVersion,
			SchemaVersion = e.SchemaVersion,
			States = [.. e.Values.Select(p => new ModStateFile.StateEntry
			{
				Key = p.Key,
				Value = (byte[])p.Value.Clone(),
			})],
		}).ToList();

		if (!_stateFile.Save(entries))
		{
			_log.LogWarning("Mod-state disk save failed — the in-memory table keeps working for this process.");
		}
	}

	// ---- Per-mod API adapter ----

	private sealed class ModStateAdapter(ModService owner, ModManifest manifest) : IModState
	{
		public bool CanWrite =>
			owner._session.Role == SessionRole.Host
			&& ModService.HasPermission(manifest, ModPermission.WriteGameState);

		public int SchemaVersion => owner.GetModStateSchemaVersion(manifest.Id);

		public IReadOnlyCollection<string> Keys => owner.GetModStateKeys(manifest.Id);

		public int Count => owner.GetModStateCount(manifest.Id);

		public bool TryGet(string key, out byte[]? value)
		{
			if (!owner.EnsureHostStateRead(manifest, "read"))
			{
				value = null;
				return false;
			}

			return owner.TryGetModStateValue(manifest.Id, key, out value);
		}

		public bool TrySetSchemaVersion(int schemaVersion)
		{
			if (!owner.EnsureHostStateWrite(manifest, "set its state schema version"))
			{
				return false;
			}

			return owner.TrySetModStateSchemaVersion(manifest, schemaVersion);
		}

		public bool TrySet(string key, byte[] value)
		{
			if (!owner.EnsureHostStateWrite(manifest, "write"))
			{
				return false;
			}

			return owner.TrySetModState(manifest, key, value);
		}

		public bool TryRemove(string key)
		{
			if (!owner.EnsureHostStateWrite(manifest, "remove"))
			{
				return false;
			}

			return owner.TryRemoveModState(manifest, key);
		}

		public bool TryClear()
		{
			if (!owner.EnsureHostStateWrite(manifest, "clear"))
			{
				return false;
			}

			return owner.TryClearModState(manifest);
		}
	}

	/// <summary>One mod's in-memory table + framework metadata.</summary>
	private sealed class ModStateEntry
	{
		public string ModId { get; set; } = "";

		public string ModVersion { get; set; } = "";

		public int SchemaVersion { get; set; } = 1;

		public Dictionary<string, byte[]> Values { get; set; } = [];
	}
}

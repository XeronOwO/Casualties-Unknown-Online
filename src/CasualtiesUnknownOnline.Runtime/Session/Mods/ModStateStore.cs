using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The mod-state half of the Phase 4 Mod API. Each mod gets a per-id key/value
/// store of opaque bytes. The host is the only save authority, so writes (and
/// host reads) are refused outside the host role — this store only owns the
/// table + persistence; the per-mod adapter applies the role/permission gate.
/// The disk store is versioned, atomic and degrade-to-empty on corruption; the
/// in-memory table is loaded once at Initialize (before discovery/Bind) and
/// lives for the process.
/// </summary>
internal sealed class ModStateStore(ModStateFileStore stateFile, ILogger log)
{
	private readonly ModStateFileStore _stateFile = stateFile;
	private readonly ILogger _log = log;
	private readonly Dictionary<string, ModStateEntry> _modState = [];
	private bool _stateLoaded;

	internal void Load()
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

	internal IModState CreateStateAdapter(ModManifest manifest, SessionService session) =>
		new ModStateAdapter(this, session, manifest, _log);

	// ---- Primitive table access (no role checks — the adapter gates them) ----

	internal bool TryGetValue(string modId, string key, out byte[]? value)
	{
		value = null;
		if (!_modState.TryGetValue(modId, out var entry) || !entry.Values.TryGetValue(key, out var stored))
		{
			return false;
		}

		value = (byte[])stored.Clone();
		return true;
	}

	internal IReadOnlyList<string> GetKeys(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? [.. entry.Values.Keys] : [];

	internal int GetCount(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? entry.Values.Count : 0;

	internal int GetSchemaVersion(string modId) =>
		_modState.TryGetValue(modId, out var entry) ? entry.SchemaVersion : 1;

	internal bool TrySetSchemaVersion(ModManifest manifest, int schemaVersion)
	{
		if (schemaVersion < 1)
		{
			_log.LogWarning("[Mods] {ModId} tried to set a non-positive mod-state schema version {Version} — refused.",
				manifest.Id, schemaVersion);
			return false;
		}

		var entry = GetOrCreate(manifest);
		entry.SchemaVersion = schemaVersion;
		entry.ModVersion = manifest.Version;
		Persist();
		return true;
	}

	internal bool TrySet(ModManifest manifest, string key, byte[] value)
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

		var entry = GetOrCreate(manifest);
		var isNew = !entry.Values.ContainsKey(key);
		if (isNew && !ModStatePolicy.CanAddKey(entry.Values.Count))
		{
			_log.LogWarning("[Mods] {ModId} reached the {Cap}-key mod-state cap — key {Key} refused.",
				manifest.Id, ModStatePolicy.MaxKeysPerMod, key);
			return false;
		}

		entry.Values[key] = (byte[])value.Clone();
		entry.ModVersion = manifest.Version;
		Persist();
		return true;
	}

	internal bool TryRemove(ModManifest manifest, string key)
	{
		if (!_modState.TryGetValue(manifest.Id, out var entry) || !entry.Values.Remove(key))
		{
			return false;
		}

		entry.ModVersion = manifest.Version;
		Persist();
		return true;
	}

	internal bool TryClear(ModManifest manifest)
	{
		var entry = GetOrCreate(manifest);
		if (entry.Values.Count == 0)
		{
			return true;
		}

		entry.Values.Clear();
		entry.ModVersion = manifest.Version;
		Persist();
		return true;
	}

	private ModStateEntry GetOrCreate(ModManifest manifest)
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

	private void Persist()
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

	private sealed class ModStateAdapter(ModStateStore store, SessionService session, ModManifest manifest, ILogger log) : IModState
	{
		public bool CanWrite =>
			session.Role == SessionRole.Host
			&& ModPermissionGate.HasPermission(manifest, ModPermission.WriteGameState);

		public int SchemaVersion => store.GetSchemaVersion(manifest.Id);

		public IReadOnlyCollection<string> Keys => store.GetKeys(manifest.Id);

		public int Count => store.GetCount(manifest.Id);

		public bool TryGet(string key, out byte[]? value)
		{
			if (!EnsureHostStateRead("read"))
			{
				value = null;
				return false;
			}

			return store.TryGetValue(manifest.Id, key, out value);
		}

		public bool TrySetSchemaVersion(int schemaVersion)
		{
			if (!EnsureHostStateWrite("set its state schema version"))
			{
				return false;
			}

			return store.TrySetSchemaVersion(manifest, schemaVersion);
		}

		public bool TrySet(string key, byte[] value)
		{
			if (!EnsureHostStateWrite("write"))
			{
				return false;
			}

			return store.TrySet(manifest, key, value);
		}

		public bool TryRemove(string key)
		{
			if (!EnsureHostStateWrite("remove"))
			{
				return false;
			}

			return store.TryRemove(manifest, key);
		}

		public bool TryClear()
		{
			if (!EnsureHostStateWrite("clear"))
			{
				return false;
			}

			return store.TryClear(manifest);
		}

		private bool EnsureHostStateRead(string operation)
		{
			if (session.Role == SessionRole.Host)
			{
				return true;
			}

			log.LogWarning("[Mods] {ModId} tried to {Operation} mod state outside the host role — refused.",
				manifest.Id, operation);
			return false;
		}

		private bool EnsureHostStateWrite(string operation)
		{
			if (session.Role != SessionRole.Host)
			{
				log.LogWarning("[Mods] {ModId} tried to {Operation} mod state outside the host role — refused.",
					manifest.Id, operation);
				return false;
			}

			return ModPermissionGate.Try(log, manifest, ModPermission.WriteGameState);
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

using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The host-persistent mod-state surface (Phase 4 Mod API remainder).
/// Each mod's state is scoped to its own mod id and stored by the framework as
/// opaque byte arrays — the framework never interprets or serializes the mod's
/// payload, so a mod can change its own schema behind <see cref="SchemaVersion"/>
/// and migrate/rebuild as needed. Writes are host-only: CUO's save authority is
/// the host (architecture.md §8), so a guest copy of a synchronized mod must
/// use the existing message/command surfaces to coordinate with the host copy,
/// not write a local file.
///
/// Values are copied on read and on write — the store never shares its internal
/// arrays with the mod, so a later mutation of a returned/input array cannot
/// corrupt the persisted state without an explicit Set.
/// </summary>
public interface IModState
{
	/// <summary>
	/// True on the host copy of the mod when the mod also declares
	/// <see cref="ModPermission.WriteGameState"/> — the only combination allowed to
	/// persist mod state. Guests and undeclared mods see false; every write method
	/// on this interface also refuses and logs.
	/// </summary>
	bool CanWrite { get; }

	/// <summary>
	/// The stored schema version for this mod's state. Defaults to 1 until the
	/// mod calls <see cref="TrySetSchemaVersion"/>. The framework stores the value
	/// verbatim; it does not migrate between schema versions.
	/// </summary>
	int SchemaVersion { get; }

	/// <summary>A snapshot of the stored keys (copy — safe to hold).</summary>
	IReadOnlyCollection<string> Keys { get; }

	/// <summary>The number of stored key/value entries for this mod.</summary>
	int Count { get; }

	/// <summary>
	/// Set the persisted schema version. Requires <see cref="CanWrite"/> and a
	/// positive version. Returns false (with a framework log) otherwise.
	/// </summary>
	bool TrySetSchemaVersion(int schemaVersion);

	/// <summary>
	/// Read one value. Returns false when the key is absent or the mod state is
	/// not available on this side. The returned array is a defensive copy.
	/// </summary>
	bool TryGet(string key, out byte[]? value);

	/// <summary>
	/// Write one value. Requires <see cref="CanWrite"/>, a valid non-empty key,
	/// and a value within the framework's state caps. The array is copied before
	/// storage and the whole table is persisted atomically on success.
	/// </summary>
	bool TrySet(string key, byte[] value);

	/// <summary>Remove one key. Requires <see cref="CanWrite"/>.</summary>
	bool TryRemove(string key);

	/// <summary>Remove every key for this mod. Requires <see cref="CanWrite"/>.</summary>
	bool TryClear();
}

using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The host-persistent mod-state surface (Phase 4 Mod API remainder).
/// Each mod's state is scoped to its own mod id and stored by the framework as
/// <see cref="ModValue"/> — the framework's typed data model, not an opaque
/// byte array — so the framework can validate a value, bound it structurally,
/// log it and show it, while the mod still owns what its fields mean and can
/// change its own schema behind <see cref="SchemaVersion"/> and migrate/rebuild
/// as needed. Writes are host-only: CUO's save authority is the host
/// (architecture.md §8), so a guest copy of a synchronized mod must use the
/// existing message/command surfaces to coordinate with the host copy, not
/// write a local file.
///
/// A value is immutable, so the store shares it rather than copying: no typed
/// path changes a value a mod read back, and only an explicit <c>TrySet</c>
/// replaces what is persisted.
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
	/// Read one value as a <see cref="ModValue"/>. Returns false when the key is
	/// absent or the mod state is not available on this side. No copy is made
	/// and none is needed: a value is immutable.
	/// </summary>
	bool TryGet(string key, out ModValue? value);

	/// <summary>
	/// Write one value. Requires <see cref="CanWrite"/>, a valid non-empty key,
	/// and a value the framework can encode inside the state caps; a refusal
	/// logs the path inside the model and the budget it broke. The whole table
	/// is persisted atomically on success.
	/// </summary>
	bool TrySet(string key, ModValue value);

	/// <summary>Remove one key. Requires <see cref="CanWrite"/>.</summary>
	bool TryRemove(string key);

	/// <summary>Remove every key for this mod. Requires <see cref="CanWrite"/>.</summary>
	bool TryClear();
}

using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-mod runtime data surface (Phase 4 Mod API remainder / CUCoreLib
/// migration base). It is deliberately NOT a generic snapshot service and it
/// does not add a wire protocol. It is a process-local, ephemeral, scoped
/// store that makes the mod-data boundary explicit:
///
/// - <see cref="ModDataScope.LocalOnly"/> values live only on the current
///   process and may be written by any role.
/// - <see cref="ModDataScope.Shared"/> values are host-owned; a guest can keep
///   a local mirror only by calling <see cref="TryApplyShared"/> with a value
///   that came from the host (normally through <see cref="IModNetwork"/>).
/// - <see cref="ModDataScope.HostAuthoritative"/> values exist only on the
///   host; guests have no mirror and must coordinate through
///   <see cref="IModCommands"/> or <see cref="IModNetwork"/> if they need an
///   update.
///
/// No value is persisted here. Durable host-persistent mod state belongs to
/// <see cref="IModState"/>; gameplay facts belong in CUO's typed kernel
/// domains. This surface is the migration seam for CUCoreLib-style ad-hoc
/// custom data: declare a scope, keep local-only state local, and move shared
/// state through the existing typed command/message surfaces.
/// </summary>
public interface IModData
{
	/// <summary>
	/// Declare one runtime data slot. The first declaration wins for a mod id;
	/// duplicate keys are refused. Local-only slots are allowed for every
	/// network mode. Shared slots require a state-bearing network mode and
	/// <see cref="ModPermission.SendNetworkMessage"/> (the only supported way
	/// to transport a shared mirror). Host-authoritative slots require a
	/// state-bearing or host-only mode.
	/// </summary>
	bool TryDeclare(string key, ModDataScope scope, int schemaVersion = 1);

	/// <summary>
	/// Read one declared runtime value. The returned array is a defensive copy.
	/// Returns false for an undeclared key, an absent value, or a
	/// host-authoritative slot on a guest copy (the framework keeps no guest
	/// mirror for that scope).
	/// </summary>
	bool TryGet(string key, out byte[]? value);

	/// <summary>
	/// Write one declared runtime value. Local-only slots may be written by any
	/// role. Shared and host-authoritative slots are host-only writes; guests
	/// must ask the host through <see cref="IModCommands"/> /
	/// <see cref="IModNetwork"/> and then apply the accepted value.
	/// </summary>
	bool TrySet(string key, byte[] value);

	/// <summary>
	/// Apply a shared value received from the host into this guest's local
	/// mirror. Only <see cref="ModDataScope.Shared"/> slots may be applied,
	/// only on a guest copy, and <paramref name="senderSteamId"/> must be the
	/// session host. This is the explicit, non-automatic replication step: the
	/// mod still owns sending/receiving the value over <see cref="IModNetwork"/>.
	/// </summary>
	bool TryApplyShared(string key, byte[] value, ulong senderSteamId);

	/// <summary>
	/// Remove a declared runtime value. Local-only slots may be removed by any
	/// role; shared and host-authoritative slots are host-only removals.
	/// </summary>
	bool TryRemove(string key);

	/// <summary>The declared scope for a slot. Returns false for an undeclared key or a host-authoritative slot on a guest copy.</summary>
	bool TryGetScope(string key, out ModDataScope scope);

	/// <summary>The mod-owned schema version for a declared slot. Returns false for an undeclared key or a host-authoritative slot on a guest copy.</summary>
	bool TryGetSchemaVersion(string key, out int schemaVersion);

	/// <summary>A snapshot of the declared slot keys visible to this copy (copy — safe to hold).</summary>
	IReadOnlyCollection<string> Keys { get; }

	/// <summary>The number of declared slots visible to this copy.</summary>
	int Count { get; }
}

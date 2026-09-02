using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The per-mod runtime status surface (mod-status domain phase 1). It is a
/// process-local, ephemeral status table keyed by
/// <c>(status id, player SteamId, optional limb slot)</c>. It is the typed
/// runtime counterpart to the static <see cref="ModStatusDefinition"/> content
/// seam, but it does NOT expose game/Unity types and it does NOT automatically
/// replicate values.
///
/// Scope rules mirror <see cref="IModData"/>:
/// - <see cref="ModDataScope.LocalOnly"/> statuses are available to any role.
/// - <see cref="ModDataScope.Shared"/> statuses are host-owned; a guest may
///   apply a host-originated value explicitly.
/// - <see cref="ModDataScope.HostAuthoritative"/> statuses have no guest mirror.
///
/// The mod owns the byte payload schema and version. This surface deliberately
/// stops before vanilla Body/Limb integration: that remains a GameAdapter
/// projection seam.
/// </summary>
public interface IModStatusRuntime
{
	/// <summary>
	/// Declare one status runtime slot. The status id is mod-scoped; the scope
	/// tells the framework whether the value is body-level or per-limb. The
	/// runtime scope tells the framework whether the value is local, shared, or
	/// host-authoritative. Returns false for invalid ids/scopes, duplicate
	/// declarations, or a runtime scope the mod's network mode cannot use.
	/// </summary>
	bool TryDeclare(string statusId, ModStatusScope scope, ModDataScope runtimeScope, int schemaVersion = 1);

	/// <summary>Read a body-level status value. Returns false for absent/undeclared values or hidden host-authoritative guest slots.</summary>
	bool TryGetBodyStatus(string statusId, ulong playerSteamId, out byte[]? value);

	/// <summary>Read a limb-level status value for a specific limb slot. Returns false for absent/undeclared values or hidden host-authoritative guest slots.</summary>
	bool TryGetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, out byte[]? value);

	/// <summary>Write a body-level status value. Local-only is any-role; shared/host-authoritative is host-only.</summary>
	bool TrySetBodyStatus(string statusId, ulong playerSteamId, byte[] value);

	/// <summary>Write a limb-level status value. Local-only is any-role; shared/host-authoritative is host-only.</summary>
	bool TrySetLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value);

	/// <summary>Apply a shared body status value received from the host into this guest's local mirror.</summary>
	bool TryApplyBodyStatus(string statusId, ulong playerSteamId, byte[] value, ulong senderSteamId);

	/// <summary>Apply a shared limb status value received from the host into this guest's local mirror.</summary>
	bool TryApplyLimbStatus(string statusId, ulong playerSteamId, int limbSlot, byte[] value, ulong senderSteamId);

	/// <summary>
	/// Apply a shared body status removal received from the host into this
	/// guest's local mirror. The slot declaration remains; only the value is
	/// cleared. Requires the same rules as <see cref="TryApplyBodyStatus"/>.
	/// </summary>
	bool TryApplyRemoveBodyStatus(string statusId, ulong playerSteamId, ulong senderSteamId);

	/// <summary>
	/// Apply a shared limb status removal received from the host into this
	/// guest's local mirror. The slot declaration remains; only the value is
	/// cleared. Requires the same rules as <see cref="TryApplyLimbStatus"/>.
	/// </summary>
	bool TryApplyRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot, ulong senderSteamId);

	/// <summary>Remove a body status value (slot declaration remains). Local-only any-role; shared/host-authoritative host-only.</summary>
	bool TryRemoveBodyStatus(string statusId, ulong playerSteamId);

	/// <summary>Remove a limb status value (slot declaration remains). Local-only any-role; shared/host-authoritative host-only.</summary>
	bool TryRemoveLimbStatus(string statusId, ulong playerSteamId, int limbSlot);

	/// <summary>Get the declared body/limb scope of a status slot.</summary>
	bool TryGetScope(string statusId, out ModStatusScope scope);

	/// <summary>Get the declared runtime scope of a status slot.</summary>
	bool TryGetRuntimeScope(string statusId, out ModDataScope runtimeScope);

	/// <summary>The mod-owned schema version for a status slot.</summary>
	bool TryGetSchemaVersion(string statusId, out int schemaVersion);

	/// <summary>All declared status ids visible to this copy (copy — safe to hold).</summary>
	IReadOnlyCollection<string> StatusIds { get; }

	/// <summary>The number of declared status slots visible to this copy.</summary>
	int StatusCount { get; }
}

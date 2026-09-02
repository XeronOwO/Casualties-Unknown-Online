using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// An internal, immutable snapshot of one stored runtime status value that has
/// a non-opaque projection kind. It is the read seam between
/// <see cref="ModStatusStore"/> and the GameAdapter's vanilla body/limb
/// projection; it deliberately uses only Abstractions types and defensive
/// byte copies, never mod instances or game/Unity types.
/// </summary>
internal sealed class ModStatusProjectionSnapshot(
	string modId,
	string statusId,
	ModStatusScope scope,
	ModDataScope runtimeScope,
	ModStatusProjectionKind projectionKind,
	int schemaVersion,
	ulong playerSteamId,
	int limbSlot,
	byte[] value)
{
	/// <summary>The owning mod id (status ids are mod-scoped).</summary>
	public string ModId { get; } = modId;

	/// <summary>The mod-scoped status id.</summary>
	public string StatusId { get; } = statusId;

	/// <summary>Body-level or limb-level.</summary>
	public ModStatusScope Scope { get; } = scope;

	/// <summary>Local-only, shared, or host-authoritative.</summary>
	public ModDataScope RuntimeScope { get; } = runtimeScope;

	/// <summary>The typed projection shape the GameAdapter should decode.</summary>
	public ModStatusProjectionKind ProjectionKind { get; } = projectionKind;

	/// <summary>The mod-owned schema version.</summary>
	public int SchemaVersion { get; } = schemaVersion;

	/// <summary>The player whose body/limb carries this status.</summary>
	public ulong PlayerSteamId { get; } = playerSteamId;

	/// <summary>The limb slot; -1 for body-level snapshots.</summary>
	public int LimbSlot { get; } = limbSlot;

	/// <summary>The mod-owned value payload (defensive copy at creation time).</summary>
	public byte[] Value { get; } = value;
}

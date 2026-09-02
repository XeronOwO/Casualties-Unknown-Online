using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The pure safety/scope rails for the per-mod runtime data surface
/// (<see cref="IModData"/>). The store is ephemeral and process-local, but the
/// same bounds as the durable mod-state store are still required so a broken or
/// hostile mod cannot grow an unbounded in-memory table or hide a shared slot
/// behind a giant payload. Scope validation is also centralized here: local-only
/// data is valid for every mode, shared data must be state-bearing (and must
/// have the message permission that makes a mirror reachable), and
/// host-authoritative data requires a host-capable mode.
/// </summary>
internal static class ModDataPolicy
{
	public const int MaxKeyLength = 128;
	public const int MaxValueBytes = 64 * 1024;
	public const int MaxSlotsPerMod = 1024;

	/// <summary>Keys must be non-empty, not all whitespace, and within the length cap.</summary>
	public static bool IsValidKey(string? key) =>
		!string.IsNullOrWhiteSpace(key) && key!.Length <= MaxKeyLength;

	/// <summary>Values must be non-null and within the per-value size cap (empty byte[] is valid).</summary>
	public static bool IsValidValue(byte[]? value) =>
		value is not null && value.Length <= MaxValueBytes;

	/// <summary>Schema versions are mod-owned but must be positive.</summary>
	public static bool IsValidSchemaVersion(int schemaVersion) => schemaVersion >= 1;

	/// <summary>Adding a brand-new slot must not exceed the per-mod slot-count cap.</summary>
	public static bool CanAddSlot(int currentSlotCount) => currentSlotCount < MaxSlotsPerMod;

	/// <summary>
	/// True when this mod may declare a slot with the given scope. Shared data is
	/// only meaningful when every relevant member runs the mod (state-bearing
	/// handshake) and can transport the update through <see cref="IModNetwork"/>.
	/// Host-authoritative data may also live in a host-only mod. Local-only is
	/// universal.
	/// </summary>
	public static bool IsValidScopeFor(ModManifest manifest, ModDataScope scope)
	{
		return scope switch
		{
			ModDataScope.LocalOnly => true,
			ModDataScope.Shared => IsStateBearing(manifest.NetworkMode)
				&& ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage),
			ModDataScope.HostAuthoritative => IsStateBearing(manifest.NetworkMode)
				|| manifest.NetworkMode == NetworkMode.HostOnly,
			_ => false,
		};
	}

	private static bool IsStateBearing(NetworkMode mode) =>
		mode is NetworkMode.Synchronized or NetworkMode.Authoritative or NetworkMode.RequiresAllPlayers;
}

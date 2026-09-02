using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// Pure safety/scope rails for the per-mod runtime status surface
/// (<see cref="IModStatusRuntime"/>). The store is ephemeral and process-local,
/// but it still needs bounded keys/values/slots and the same network-mode scope
/// rules as <see cref="IModData"/>: local statuses are universal, shared
/// statuses require a state-bearing mode with message permission, and
/// host-authoritative statuses require a host-capable mode.
/// </summary>
internal static class ModStatusPolicy
{
	public const int MaxStatusIdLength = 128;
	public const int MaxValueBytes = 64 * 1024;
	public const int MaxStatusesPerMod = 1024;
	public const int MaxLimbSlot = 255;

	public static bool IsValidStatusId(string? statusId) =>
		!string.IsNullOrWhiteSpace(statusId) && statusId!.Length <= MaxStatusIdLength;

	public static bool IsValidValue(byte[]? value) =>
		value is not null && value.Length <= MaxValueBytes;

	public static bool IsValidSchemaVersion(int schemaVersion) => schemaVersion >= 1;

	public static bool IsValidLimbSlot(int limbSlot) => limbSlot >= 0 && limbSlot <= MaxLimbSlot;

	public static bool CanAddStatus(int currentStatusCount) => currentStatusCount < MaxStatusesPerMod;

	public static bool IsValidRuntimeScopeFor(ModManifest manifest, ModDataScope runtimeScope) =>
		ModDataPolicy.IsValidScopeFor(manifest, runtimeScope);
}

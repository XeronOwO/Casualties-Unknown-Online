using CasualtiesUnknownOnline.GameAdapter.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Shared guard for native collision presentation on non-authoritative guest
/// world-item copies. Both known item-prefab collision methods use the same
/// rule, so the session/authority decision stays in one place instead of being
/// duplicated per script.
/// </summary>
internal static class NonAuthoritativeItemImpactGuard
{
	internal static bool ShouldSuppress(Item item) =>
		PatchBridge.Impl is { } bridge
		&& NonAuthoritativeItemImpactPolicy.ShouldSuppress(
			bridge.IsSessionActive,
			bridge.IsHostMode,
			ItemWorldSync.IsStandaloneWorldItem(item));

	/// <summary>
	/// The authority half of the same rule: this side simulated the landing, so
	/// its native presentation runs and is reported to the guests. One predicate
	/// for both collision hooks, the way the suppression already is.
	/// </summary>
	internal static bool ShouldReport(Item item) =>
		PatchBridge.Impl is { } bridge
		&& NonAuthoritativeItemImpactPolicy.ShouldReport(
			bridge.IsSessionActive,
			bridge.IsHostMode,
			ItemWorldSync.IsStandaloneWorldItem(item));

	internal static bool Suppress(Item item, string source)
	{
		if (!ShouldSuppress(item))
		{
			return false;
		}

		PatchBridge.Impl?.OnNonAuthoritativeItemImpactSuppressed(item, source);
		return true;
	}
}

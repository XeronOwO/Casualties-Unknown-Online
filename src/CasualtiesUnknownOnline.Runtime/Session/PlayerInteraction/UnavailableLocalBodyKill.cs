using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The Runtime's default <see cref="ILocalBodyKill"/> for a build with no Game Adapter: there is no
/// game scene and no local body, so the verb says that rather than killing nothing and claiming
/// success. The plugin replaces it with the adapter's implementation.
/// </summary>
internal sealed class UnavailableLocalBodyKill : ILocalBodyKill
{
	public LocalBodyKillOutcome Kill() =>
		LocalBodyKillOutcome.Refused("this build has no game scene, so it cannot kill a player's body.");
}

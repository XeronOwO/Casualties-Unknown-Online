using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The Runtime's default <see cref="ILocalBodyReset"/> for a build with no Game Adapter: there is
/// no game scene and no local body, so the verb says that rather than resetting nothing and
/// claiming success. The plugin replaces it with the adapter's implementation.
/// </summary>
internal sealed class UnavailableLocalBodyReset : ILocalBodyReset
{
	public LocalBodyResetOutcome Reset() =>
		LocalBodyResetOutcome.Refused("this build has no game scene, so it cannot reset a player's body.");
}

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The engine random stream one shrapnel grip slip runs in. The grip slip is the ONE cross-player
/// action whose effect the game rolls twice — on the operator's client against the limb of the body
/// that client displays, and on the host against the authoritative limb — so the two screens can hold
/// different values for the same event unless both draw from the same derived state.
/// <para>
/// Deriving that state is the whole mechanism: both machines derive the same seed from the slip's own
/// identity (<see cref="ShrapnelGripSlipSeed"/>), draw the same values from it, and the operator's
/// screen is right immediately instead of being corrected by the authority's state message a round
/// trip later. The authority still judges — it derives its own values and never adopts the operator's
/// — and <see cref="ShrapnelGripSlipRoller"/> compares the two so a drift between the sequences is
/// reported instead of silently shown.
/// </para>
/// <para>
/// The window is a GLOBAL resource (the engine keeps one stream), which is sound only because both
/// call sites are short synchronous calls: the operator's native <c>ShrapnelMinigame.BreakGrasp</c>
/// and the host's own draw. Nothing else runs inside them.
/// </para>
/// </summary>
public interface IShrapnelGripSlipRandom
{
	/// <summary>
	/// Draws the values a native <c>ShrapnelMinigame.BreakGrasp</c> draws for one slip, from a state
	/// derived from <paramref name="seed"/>, and leaves the engine stream where it found it. Null when
	/// this build has no engine stream to derive from: the caller then applies no damage and says so
	/// rather than inventing values.
	/// </summary>
	ShrapnelGripSlipValues? TryDraw(int seed, bool isHead);

	/// <summary>
	/// Holds the engine stream on the state derived from <paramref name="seed"/> until the returned
	/// window is disposed, so a native call made inside the window draws the very values
	/// <see cref="TryDraw"/> returns for the same seed. Null under the same condition as
	/// <see cref="TryDraw"/>.
	/// </summary>
	IShrapnelGripSlipWindow? TryEnterWindow(int seed);
}

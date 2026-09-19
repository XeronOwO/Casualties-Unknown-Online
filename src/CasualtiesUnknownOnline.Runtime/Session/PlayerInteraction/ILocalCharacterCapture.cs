using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game-specific answer to "what is MY body right now?" — captured on demand
/// instead of read from a stream. The Runtime owns the rule that the client
/// affected by a judgment is the one that makes it; the Game Adapter owns the
/// actual capture (live Body → wire snapshot). The line-of-sight oracle next to
/// this one follows the same seam shape, and for the same reason: the Runtime
/// must not know the game's types.
/// </summary>
public interface ILocalCharacterCapture
{
	/// <summary>
	/// True when this composition can read a live body at all — the adapter-backed one can,
	/// a composition with no game scene cannot. The flag decides what a null from
	/// <see cref="CaptureLocal"/> MEANS, and the two meanings must not be conflated: with a
	/// live capture a null is this client's answer "my body cannot be read right now", so the
	/// verdict has to be a refusal; without one there is no live source to consult at all and
	/// the caller falls back to this side's own stored snapshot. A live-capture composition
	/// that silently fell back would judge a present operation on a world-entry snapshot —
	/// exactly the stale-data judgment this seam exists to remove.
	/// </summary>
	bool HasLiveCapture { get; }

	/// <summary>
	/// The local body's character snapshot at this instant, or null when there is nothing to
	/// read — see <see cref="HasLiveCapture"/> for what a null means. Whatever a caller does
	/// with it, the data is always THIS client's own, never another client's picture of this
	/// body.
	/// </summary>
	CharacterDataMsg? CaptureLocal();
}

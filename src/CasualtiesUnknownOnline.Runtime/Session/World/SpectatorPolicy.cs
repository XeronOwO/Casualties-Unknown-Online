namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Who may watch, in one place. Two surfaces ask this question — the camera, which enforces it, and the member
/// card, which decides whether to offer the button — and a second copy of the terms is how the two drift: the
/// first cut of the card had its own, and it had already stopped reading the host's unconscious rule by the time
/// a review looked.
///
/// <para>
/// The rule is the ticket's own: a dead character may watch, and an unconscious one only when the host says so.
/// That second term is a group decision rather than a rendering one — an unconscious body is still its owner's
/// to act on, so letting it watch changes a LIVE player's client — which is why it is the host's switch and why
/// both readers must read it.
/// </para>
/// </summary>
internal static class SpectatorPolicy
{
	/// <summary>Whether a client in this state may watch. The caller still has to answer the target's side.</summary>
	internal static bool MayWatch(bool localAlive, bool localConscious, bool spectateWhileUnconscious, bool sessionActive) =>
		sessionActive && (!localAlive || (spectateWhileUnconscious && !localConscious));
}

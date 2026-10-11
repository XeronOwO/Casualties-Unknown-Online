namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The seed both sides of one shrapnel grip slip derive their engine random state from. It is a pure
/// function of facts BOTH machines hold about that one slip — the session's operation id, the piece
/// the operator lost its grip on, and the slip's own nonce, which the operator puts on the report —
/// so no extra round trip is needed to agree on it.
/// <para>
/// The nonce is the operator's own per-session slip counter, carried on the report because the host's
/// own counters cannot stand in for it: several operators share one session and the host counts the
/// reports it accepts, not the slips a given client drew. Two slips of the same piece therefore get
/// different seeds, and the same slip always gets the same one.
/// </para>
/// <para>
/// This is a spread, not a secret: an operator that reuses a nonce makes its own next slip redraw the
/// previous values, which changes nothing it could not already do by drawing locally. The nonce is folded
/// into 32 bits, so it wraps after about two billion slips in one session — far past any real session, and
/// a wrap would only let two far-apart slips of one piece share a state, never make the two sides disagree,
/// because both fold it the same way.
/// </para>
/// </summary>
internal static class ShrapnelGripSlipSeed
{
	private const ulong Gamma = 0x9E3779B97F4A7C15UL;

	/// <summary>
	/// The 32-bit seed the engine's own state is initialized from. The engine takes an
	/// <see cref="int"/>, so the mixed 64-bit value is folded to its high half — the low half of a
	/// SplitMix64 output is the weaker one.
	/// </summary>
	internal static int Of(ulong operationId, int pieceIndex, int nonce)
	{
		var slip = ((ulong)(uint)pieceIndex << 32) | (uint)nonce;
		return unchecked((int)(Mix((operationId * Gamma) + Mix(slip)) >> 32));
	}

	private static ulong Mix(ulong value)
	{
		value += Gamma;
		value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
		value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
		return value ^ (value >> 31);
	}
}

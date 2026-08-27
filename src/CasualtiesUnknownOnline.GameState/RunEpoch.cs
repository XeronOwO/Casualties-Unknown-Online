namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Identity of one multiplayer run/world epoch. Every command, batch, and
/// checkpoint carries a RunEpoch so a stale run can never pollute a new one.
/// </summary>
public readonly record struct RunEpoch(ulong Value)
{
	public static RunEpoch Fresh(ulong value) => new(value);
}

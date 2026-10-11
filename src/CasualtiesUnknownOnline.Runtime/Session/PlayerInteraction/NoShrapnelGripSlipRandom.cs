namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The base composition root's answer to <see cref="IShrapnelGripSlipRandom"/>: this root runs with no
/// game engine, so there is no random stream to derive a state from and no window to hold one open.
/// Both methods answer null, and the authority's
/// <see cref="ShrapnelGripSlipRoller.Roll"/> turns that into a named error and no damage rather than a
/// value nobody could reproduce. The plugin replaces this with the Game Adapter's engine-backed
/// implementation, so in a packaged build the null path is unreachable — the same shape the four
/// content seams beside it already use.
/// </summary>
internal sealed class NoShrapnelGripSlipRandom : IShrapnelGripSlipRandom
{
	public ShrapnelGripSlipValues? TryDraw(int seed, bool isHead) => null;

	public IShrapnelGripSlipWindow? TryEnterWindow(int seed) => null;
}

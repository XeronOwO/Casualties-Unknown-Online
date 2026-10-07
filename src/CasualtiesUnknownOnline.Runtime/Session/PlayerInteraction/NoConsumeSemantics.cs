namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The base composition's answer: this side has no game scene, so no content
/// fact is known and the consume chain refuses. The plugin replaces it with the
/// Game Adapter's game-data implementation, the same way
/// <see cref="NoLimbUseSemantics"/> and
/// <see cref="UnavailableLocalCharacterCapture"/> are replaced.
/// </summary>
public sealed class NoConsumeSemantics : IConsumeSemantics
{
	public bool IsUsableLiquidContainer(string itemId) => false;
}

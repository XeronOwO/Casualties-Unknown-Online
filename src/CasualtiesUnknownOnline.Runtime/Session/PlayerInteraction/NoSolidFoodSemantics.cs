namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The base composition's answer: this side has no game scene, so no content
/// fact is known and the solid-food chain refuses. The plugin replaces it with
/// the Game Adapter's game-data implementation, the same way
/// <see cref="NoConsumeSemantics"/> and <see cref="NoLimbUseSemantics"/> are
/// replaced.
/// </summary>
public sealed class NoSolidFoodSemantics : ISolidFoodSemantics
{
	public SolidFoodVerdict Classify(string itemId) => SolidFoodVerdict.NotSolidFood;
}

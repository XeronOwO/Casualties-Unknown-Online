namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The unmodified run's settings: what the container serves when no game is present (the
/// console-only container and the tests). The Game Adapter re-registers its own implementation
/// in a real session so a widened run is honoured.
/// </summary>
public sealed class DefaultPlayerVitalsTuning : IPlayerVitalsTuning
{
	/// <summary>The unmodified run's <c>healingrate</c>.</summary>
	public float HealingRate => 1f;

	/// <summary>The unmodified run's <c>bleedrate</c>.</summary>
	public float BleedRate => 1f;
}

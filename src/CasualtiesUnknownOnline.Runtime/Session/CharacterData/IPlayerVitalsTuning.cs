namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The two run settings the host's bleeding model integrates with (<c>healingrate</c>,
/// <c>bleedrate</c>). They are world globals the Game Adapter reads from the game's own run
/// settings; the Runtime never touches game types, so it asks through this port.
/// </summary>
public interface IPlayerVitalsTuning
{
	/// <summary>The run's <c>healingrate</c> setting (1 is the unmodified run).</summary>
	float HealingRate { get; }

	/// <summary>The run's <c>bleedrate</c> setting (1 is the unmodified run).</summary>
	float BleedRate { get; }
}

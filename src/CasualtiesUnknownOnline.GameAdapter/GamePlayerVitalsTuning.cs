using CasualtiesUnknownOnline.Runtime.Session.CharacterData;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The run settings the host's bleeding model integrates with, read from the game's own run
/// settings (the <c>RunSettings</c> table behind <c>WorldGeneration.GetRunSettingFloat</c>).
/// The values are the LIVE run's: a widened or modified run changes how fast a wound clots and
/// how fast it bleeds, and the host's model has to follow the same numbers the bodies do.
/// </summary>
internal sealed class GamePlayerVitalsTuning : IPlayerVitalsTuning
{
	/// <summary>The run's <c>healingrate</c>.</summary>
	public float HealingRate => Setting("healingrate");

	/// <summary>The run's <c>bleedrate</c>.</summary>
	public float BleedRate => Setting("bleedrate");

	/// <summary>
	/// One float run setting. Before a world exists the game has no settings table at all
	/// (<c>WorldGeneration.runSettings</c> is null until its <c>Awake</c>), so the unmodified
	/// run's 1 answers; once the table exists its own value is used verbatim, including a run
	/// that deliberately sets the rate to zero.
	/// </summary>
	private static float Setting(string name)
	{
		if (WorldGeneration.runSettings is null)
		{
			return 1f;
		}

		return WorldGeneration.GetRunSettingFloat(name);
	}
}

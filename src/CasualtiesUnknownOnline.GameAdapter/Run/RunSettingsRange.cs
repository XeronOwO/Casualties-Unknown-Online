using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Pure co-op range policy for the game's custom run-settings sliders. The
/// base game tunes these ranges for a single player; in a multiplayer lobby the
/// host may need more loot, more traps, more trader stock, a longer time limit,
/// etc. to compensate for several players sharing one world. This policy widens
/// the upper bound of the tuning sliders proportionally to the lobby size.
/// Values are still selected by the host and continue to ride the existing
/// world-start params unchanged — this is a UI range decision only.
/// </summary>
internal static class RunSettingsRange
{
	/// <summary>
	/// Sliders whose maximum is a resource/difficulty tuning value safe to widen
	/// for a larger lobby. Percentage sliders (traderchance/layermodifierchance),
	/// fixed offsets (traderrepoffset/temperatureoffset) and debug/bool settings
	/// are intentionally excluded: their single-player limits are already
	/// semantic bounds, not scale factors.
	/// </summary>
	private static readonly HashSet<string> ScalableSettings = [
		"baselootdensity",
		"lootmultiplier",
		"basetrapdensity",
		"trapincrease",
		"timelimit",
		"xpgain",
		"metabolismrate",
		"healingrate",
		"fracturepain",
		"bleedrate",
		"infectionspeed",
		"infectionchance",
		"fibrillationrate",
		"moodnormalizationrate",
		"bonuslimbarmor",
		"staminaregen",
		"attackdamage",
		"minigamehandshake",
		"sleepcyclespeed",
		"encumbrancecap",
		"traderitemamount",
		"itemdecayrate",
		"lockpickprecision",
		"timebetweenearthquakes",
		"oreamount",
	];

	internal static bool IsScalable(string name) => ScalableSettings.Contains(name);

	/// <summary>
	/// Compute the effective slider limits for a run setting in co-op.
	/// <paramref name="memberCount"/> is the total player count (host + guests).
	/// Non-scalable settings keep their original range; scalable ones widen the
	/// upper bound by the lobby size (solo = 1 keeps the original range).
	/// </summary>
	internal static (float Min, float Max) ForCoOp(string name, float min, float max, int memberCount)
	{
		if (!IsScalable(name))
		{
			return (min, max);
		}

		var scale = Math.Max(1, memberCount);
		return (min, max * scale);
	}
}

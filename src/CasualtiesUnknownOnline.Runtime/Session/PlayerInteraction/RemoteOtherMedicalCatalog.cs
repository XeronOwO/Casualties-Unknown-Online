using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The non-bandage Stage 3 medical action item surfaces that do not fit the
/// direct heal/tool catalogs: amputation cutting tools, defibrillators and
/// dislocation wrenches. These are read-only presence checks; the host service
/// is the authority.
/// </summary>
public static class RemoteOtherMedicalCatalog
{
	private static readonly HashSet<string> AmputationTools =
	[
		"machete",
		"titaniummachete",
		"crudecleaver",
		"sickle",
		"claws",
		"titaniummultitool",
		"flimsyknife",
	];

	private static readonly HashSet<string> Defibrillators =
	[
		"aed",
		"manualdefibrillator",
	];

	private static readonly HashSet<string> DislocationWrenches =
	[
		"wrench",
		"makeshiftwrench",
	];

	public static bool IsAmputationTool(string itemId) => AmputationTools.Contains(itemId);

	public static bool IsDefibrillator(string itemId) => Defibrillators.Contains(itemId);

	public static bool IsAed(string itemId) => itemId == "aed";

	public static bool IsManualDefibrillator(string itemId) => itemId == "manualdefibrillator";

	public static bool IsDislocationWrench(string itemId) => DislocationWrenches.Contains(itemId);
}

using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The clip a REMOTE limb treatment has to play, per accepted item.
///
/// <para>
/// The remote medical view blocks the native <c>PlayerCamera.ApplyWoundItem</c>
/// (<c>RemoteMedicalBlockApplyWoundItemPatch</c>), so the item's own
/// <c>useLimbAction</c> never runs and the clip it would have played was heard
/// by nobody — operator, patient and third peers alike. The clip identity is
/// native knowledge the blocked call took with it (each delegate plays its own
/// literal), so CUO holds it here: the medical domain plays the exact clip at
/// the treated limb on the operator's client, and the existing character-sound
/// relay presents it to everyone else.
/// </para>
///
/// <para>
/// The rows are the 2026-09-26 census of the decompiled tree
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/Item.cs</c> and
/// <c>Liquids.cs</c>; those line numbers are stable). Three groups, each with a
/// reason:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="TreatmentClips"/> — the delegates that play a
/// clip SYNCHRONOUSLY when the limb action runs. These are exactly the ones the
/// blocked call silenced.</description></item>
/// <item><description><see cref="TreatmentLiquidClips"/> — the topical
/// containers, whose clip comes from the LIQUID's <c>onHealthUse</c> reached
/// through <c>WaterContainerItem.ApplyToLimb</c> rather than from the item
/// (<c>Liquids.cs:1074</c> / <c>:1100</c>), so the row is keyed by liquid id. A
/// spray bottle holding one of them plays its own <c>spray</c> AND this clip,
/// the way the native path does.</description></item>
/// <item><description><see cref="UncarriedItems"/> — accepted items this table plays nothing for,
/// recorded rather than left undecided, with the reason per group. The bandage family is here
/// because its clip comes from the native minigame's own step (captured by
/// <c>BandageMinigameSoundPatches</c>, announced to the peers by that scope); the
/// syringe/defibrillator families because their minigames play only 2D screen feedback, which the
/// user's ruling keeps local; and the amputating tools because their clip comes from the
/// amputation minigame's completion rather than from the item's limb action — the same
/// per-step shape, captured where it plays by <c>AmputationMinigameSoundPatch</c> and
/// <c>ShrapnelMinigameSoundPatch</c> (<c>review/treatment-gore-presentation-carried.md</c> holds that
/// census).</description></item>
/// </list>
/// </summary>
public static class RemoteMedicalTreatmentSoundCatalog
{
	/// <summary>The accepted items whose native limb action plays a clip itself, mapped to that exact clip.</summary>
	private static readonly IReadOnlyDictionary<string, string> TreatmentClips =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			// Injectable containers whose delegate plays "syringe" directly
			// instead of starting the syringe minigame (Item.cs:1760/1926/1375/1734/1539/1565).
			["antiserum"] = "syringe",
			["bloodbag"] = "syringe",
			["bloodbaghuman"] = "syringe",
			["bloodcoagulant"] = "syringe",
			["combatpen"] = "syringe",
			["streptokinase"] = "syringe",
			// Limb tools (Item.cs:696/1613/1589/616/1483/1509/4864).
			["boneweldingtool"] = "boneweld",
			["carcasssplint"] = "splint",
			["chestdrain"] = "syringe",
			["clottingmush"] = "goo",
			// The suture's own delegate plays the limb's gore roll (Item.cs:378 →
			// Body.DoGoreSound). The blocked call is suppressed in the remote view, so
			// the clip has to come from here; the body's roll picks one of five
			// variants, and this table names the base clip the limb's own Dismember
			// plays first (Limb.cs:98) — the variation is native flavour, not a
			// second fact to carry.
			["medicalsuture"] = "gore",
			["musharm"] = "goo",
			["splint"] = "splint",
			["tweezers"] = "tweezeruse",
			["wrench"] = "wrenchhit",
			// Topical containers that spray before they apply (Item.cs:2100/2124).
			["disinfectant"] = "spray",
			["spraybottle"] = "spray",
		};

	/// <summary>The health-usable liquids whose <c>onHealthUse</c> plays a cream clip (Liquids.cs:1074 reliefcream, :1100 woundglue).</summary>
	private static readonly IReadOnlyDictionary<string, string> TreatmentLiquidClips =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["reliefcream"] = "cream",
			["woundglue"] = "cream",
		};

	/// <summary>Accepted items whose clip is the LIQUID's, not the item's — the topical cream containers (Item.cs:650/674 route through <c>ApplyToLimb</c>).</summary>
	private static readonly string[] LiquidDrivenItems =
	[
		"paincream",
		"woundglue",
	];

	/// <summary>Accepted items this table plays no clip for, in recorded groups: the bandage family (its clip is the native minigame's own step), the 2D-minigame surfaces, the tools whose delegate is silent, and the two groups whose clip belongs to the limb/gore presentation instead (see the class doc).</summary>
	private static readonly string[] UncarriedItems =
	[
		// Bandage family — the clip is "bandage", played by the native minigame's own step.
		"alginate",
		"analgesicgauze",
		"bandage",
		"bruisekit",
		"plasticbandage",
		"rag",
		"rippeddressing",
		"sterilizedbandage",
		// Heal items whose delegate plays nothing at all.
		"adhesivebandage",
		// Tools with no clip in their delegate.
		"icepack",
		"tourniquet",
		// Injectable liquids whose delegate starts SyringeMinigame (2D cues only).
		"ceftriaxone",
		"fentanyl",
		"heroin",
		"morphine",
		"naloxone",
		"opium",
		"ringersolution",
		"saline",
		"syringe",
		// Defibrillators — AEDMinigame / ManualDefibMinigame cues are 2D.
		"aed",
		"manualdefibrillator",
		// The dislocation wrench whose delegate plays nothing (the "wrench" row above does).
		"makeshiftwrench",
		// Amputating tools — the delegate starts the native AmputationMinigame, whose
		// COMPLETION plays the limb's gore presentation (Limb.Dismember → "gore" +
		// Body.DoGoreSound → "gore{N}"). This table carries nothing for them: the clip
		// belongs to the minigame's own step, captured where it plays
		// (AmputationMinigameSoundPatch), which is why the operator of a remote amputation
		// and every peer now hear it while the table's rows stay item-action facts.
		"claws",
		"crudecleaver",
		"flimsyknife",
		"machete",
		"sickle",
		"titaniummachete",
		"titaniummultitool",
	];

	/// <summary>The clip this item's native limb action would have played, when it plays one itself.</summary>
	public static bool TryGetClip(string itemId, out string clip) =>
		TreatmentClips.TryGetValue(itemId, out clip!);

	/// <summary>The clip the applied LIQUID's own health use plays, for the topical container path.</summary>
	public static bool TryGetLiquidClip(string liquidId, out string clip) =>
		TreatmentLiquidClips.TryGetValue(liquidId, out clip!);

	/// <summary>The items whose clip is decided by the liquid they hold (recorded so the census has no undecided item).</summary>
	public static IReadOnlyCollection<string> LiquidDriven => LiquidDrivenItems;

	/// <summary>The accepted items this table plays no clip for — each group's reason is on the array's own rows (a group may be natively silent OR carry a clip this table does not own, e.g. the limb's gore presentation).</summary>
	public static IReadOnlyCollection<string> Uncarried => UncarriedItems;
}

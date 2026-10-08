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
/// <item><description><see cref="UncarriedItems"/> — accepted items this table plays nothing for,
/// recorded rather than left undecided, with the reason per group. The bandage family is here
/// because its clip comes from the native minigame's own step (captured by
/// <c>BandageMinigameSoundPatches</c>, announced to the peers by that scope); the
/// defibrillator family because its minigame plays only 2D screen feedback, which the
/// user's ruling keeps local; and the amputating tools because their clip comes from the
/// amputation minigame's completion rather than from the item's limb action — the same
/// per-step shape, captured where it plays by <c>AmputationMinigameSoundPatch</c> and
/// <c>ShrapnelMinigameSoundPatch</c> (<c>review/treatment-gore-presentation-carried.md</c> holds that
/// census).</description></item>
/// </list>
///
/// <para>
/// The injection family left this table (Part A of
/// <c>mod-cross-player-native-semantics</c>): the operator's client now RUNS the item's
/// own <c>useLimbAction</c> inside the medical capture window
/// (<c>NativeLimbActionScope</c>), so the clip is the native one — played by the
/// delegate itself and classified by that scope — instead of a copy CUO replays. A
/// carrier must therefore appear in this table NOWHERE: a row here would be a second
/// decider for one clip. What that means per carrier is worth stating: six of the
/// fifteen play <c>"syringe"</c> in their own delegate (relayed by that window), while
/// the nine minigame carriers have no delegate-level clip at all — their only cue is the
/// syringe minigame's 2D screen feedback, which the existing ruling keeps local — so for
/// them this table's old <c>UncarriedItems</c> rows simply recorded a silence the native
/// path still keeps.
/// </para>
///
/// <para>
/// The topical family followed it (Part B). Both halves of its sound are native and the
/// table has no row for either: the two spray containers play <c>"spray"</c> in their own
/// delegate (Item.cs:2100/2124), relayed by the operator's measurement window, and the
/// cream containers' clip comes from the LIQUID's <c>onHealthUse</c>
/// (Liquids.cs:1074/1100), which now runs on the patient's own client through
/// <c>NativeTopicalApply</c> and is relayed from there. Keeping the liquid rows would
/// double the clip on the patient, who runs that delegate locally.
/// </para>
///
/// <para>
/// The LIMB-TOOL family is the third to leave (Part B's last chain). Its clips are the
/// delegates' own — <c>"boneweld"</c>, <c>"splint"</c>, <c>"syringe"</c> for the chest
/// drain, <c>"goo"</c>, and the limb's gore roll for the suture (Item.cs:696/1613/1589/
/// 616/378/1483/1509) — and the delegate now runs on the TREATED player's client through
/// <c>NativeLimbToolApply</c>, inside the same medical capture scope, so every clip is
/// native and relayed from the body it lands on. The two tools whose delegate plays
/// nothing (<c>icepack</c>, <c>tourniquet</c>) simply stay silent, as they always were.
/// The rows those items used to hold are gone for the injection and topical families'
/// reason: a row here plus the delegate's own clip is one clip played twice.
/// </para>
/// </summary>
public static class RemoteMedicalTreatmentSoundCatalog
{
	/// <summary>The accepted items whose native limb action plays a clip itself, mapped to that exact clip. The migrated chains' items are NOT listed here any more: their clip is played by the delegate this side now runs, and a row would be a second decider for one clip.</summary>
	private static readonly IReadOnlyDictionary<string, string> TreatmentClips =
		new Dictionary<string, string>(StringComparer.Ordinal)
		{
			// musharm is the wound-view bandage session's item (its clip comes from that
			// session's own step) but its delegate also plays "goo"; the routing hands it
			// to the session, which is the half this table still serves.
			["musharm"] = "goo",
			// The shared shrapnel and dislocation sessions' held tools.
			["tweezers"] = "tweezeruse",
			["wrench"] = "wrenchhit",
		};

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

	/// <summary>The accepted items this table plays no clip for — each group's reason is on the array's own rows (a group may be natively silent OR carry a clip this table does not own, e.g. the limb's gore presentation).</summary>
	public static IReadOnlyCollection<string> Uncarried => UncarriedItems;
}

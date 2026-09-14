using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Whether a body that just appeared in the world is a NEW player — the world has no
/// character for it — and if so what the run's <c>startingsupplies</c> setting hands
/// it (S4.3). Pure: the world baseline and the body's entry state come in as data, the
/// decision and the item/slot plan come out, so every branch the live game can reach is
/// verifiable without a Unity scene.
///
/// The game's own grant is the reference implementation and the reason this exists:
/// <c>WorldGeneration.WorldPlacePlayer</c> hands the supplies out inside generation and
/// ONLY on the run's first layer (<c>WorldGeneration.cs:1891</c> —
/// <c>totalTraveled &lt;= 0 &amp;&amp; biomeOverride == None &amp;&amp; debugStartDepth == 0</c>),
/// so every later entry into the run — and every restored world, whose generation never
/// goes through that branch with a fresh body — otherwise gets nothing at all. The same
/// setting, the same content ids and the same slots are reproduced here so a player
/// supplied by either path ends up with one inventory, not two flavours of one.
/// </summary>
internal static class StartingSupplyPolicy
{
	/// <summary>The run setting the game reads at <c>WorldGeneration.cs:1899</c>.</summary>
	internal const string SettingName = "startingsupplies";

	/// <summary>The game's own words for the setting's values (<c>RunSettings.cs:53</c>), in value order.</summary>
	private static readonly string[] SettingNames = ["none", "light", "full"];

	/// <summary>
	/// Why a body was or was not supplied — the account the report and the log carry, so
	/// "nothing happened" is always one of a small set of named answers.
	/// </summary>
	internal enum Reason
	{
		/// <summary>The setting's items were planned for this body.</summary>
		Granted,

		/// <summary>The run is configured with <c>startingsupplies = none</c> (or carries no run settings at all, e.g. the tutorial).</summary>
		Disabled,

		/// <summary>The game's own first-layer grant already supplied this body — CUO must not supply it twice.</summary>
		AlreadyOwned,

		/// <summary>A character restore is queued for this body: the world HAS a character for this player.</summary>
		CharacterRestored,

		/// <summary>No generation baseline is published (no world params) — nothing can be decided about this generation.</summary>
		NoBaseline,
	}

	/// <summary>
	/// What the decision resolved to. <see cref="Reason"/> is never
	/// <see cref="Reason.Granted"/> with an empty <see cref="Plan"/> (the setting's plan
	/// is non-empty by construction) and never carries a plan for any other reason, so a
	/// caller cannot half-grant.
	/// </summary>
	internal readonly record struct Decision(Reason Reason, string Setting, IReadOnlyList<StartingSupplyEntry> Plan)
	{
		/// <summary>True = this body is a new player and the run's setting has items for it.</summary>
		internal bool ShouldGrant => Reason == Reason.Granted;
	}

	/// <summary>
	/// One item of the plan: the content id the game's own grant creates
	/// (<c>Utils.Create</c> ids, <c>WorldGeneration.cs:1904-1912</c>) and the slot it is
	/// placed into. The slot numbers are the game's own, so a player supplied here and a
	/// player supplied by the native grant carry the same items in the same places.
	/// </summary>
	/// <param name="ItemId">The content id (<c>Utils.Create</c> resource name).</param>
	/// <param name="Slot">The preferred inventory slot, by the native grant's numbering.</param>
	internal readonly record struct StartingSupplyEntry(string ItemId, int Slot);

	/// <summary>
	/// The run's configured supplies, in the game's own value space (0 = none,
	/// 1 = light, 2 = full). Anything else — including a run that carries no such
	/// setting at all — is NONE, which is exactly what the game itself does (its
	/// <c>if (runSettingInt != 1) { if (runSettingInt == 2) ... } else ...</c> grants
	/// nothing for any other value).
	/// </summary>
	internal static IReadOnlyList<StartingSupplyEntry> PlanFor(int setting) => setting switch
	{
		1 => [new StartingSupplyEntry("emergencylight", 3)],
		2 =>
		[
			new StartingSupplyEntry("lantern", 3),
			new StartingSupplyEntry("dogfood", 4),
			new StartingSupplyEntry("waterbottle", 5),
			new StartingSupplyEntry("trashbag", 1),
		],
		_ => [],
	};

	/// <summary>
	/// The run's setting as the game names it, for the report's words; an unknown value
	/// keeps the raw number so the account stays truthful about a value the game has no
	/// name for.
	/// </summary>
	internal static string Describe(int setting) =>
		setting >= 0 && setting < SettingNames.Length ? SettingNames[setting] : $"unknown ({setting})";

	/// <summary>
	/// The live world's setting, or null when the run carries no run settings at all
	/// (the tutorial nulls them — <c>PreRunScript.cs:312</c>). Read from the generation
	/// baseline rather than from the game's field: the baseline IS the value this
	/// generation was built with, on both sides (<c>WorldParamsService.Apply</c> writes
	/// it into the game before generation starts), so host and guest can never disagree.
	/// </summary>
	internal static int? SettingOf(WorldStartParams? baseline)
	{
		if (baseline?.RunSettings is not { } settings || !settings.TryGetValue(SettingName, out var value))
		{
			return null;
		}

		return value switch
		{
			int i => i,
			long l => (int)l,
			_ => null,
		};
	}

	/// <summary>
	/// True = the game's own grant covers this generation, so CUO must not supply on top of
	/// it. Every clause is the game's own condition (<c>WorldGeneration.cs:1891</c>:
	/// <c>totalTraveled &lt;= 0 &amp;&amp; biomeOverride == None &amp;&amp; debugStartDepth == 0</c>),
	/// read off the generation baseline the two sides share.
	///
	/// It is deliberately the WHOLE condition and nothing more. The tempting extra clause —
	/// "unless this generation came from an archive" — is wrong, and the S4.3 adversarial
	/// review proved it: a CUO continue lets the native <c>PreRunScript.LoadRun</c> run
	/// (<c>PreRunScriptLoadRunPatch</c>), which loads the scene, and the CUO patch blocks
	/// <c>SaveSystem.TryLoadGame</c> — the only thing that ever wrote a non-zero
	/// <c>totalTraveled</c> on a load. So on a restored run frozen on its starting layer the
	/// live field is the archive's own <c>totalTraveled == 0</c> (written by
	/// <c>WorldParamsService.Apply</c> before generation) and the game's grant DOES fire —
	/// for the host and for a following guest alike, because both walk the same
	/// <c>GenerateWorld</c> → <c>WorldPlacePlayer</c> path. Flipping this to "not covered"
	/// therefore handed a SECOND set of items to a player whose slots the native grant had
	/// just filled: <c>PickUpItem</c> refuses every occupied slot silently and the new items
	/// were left on the ground as world items. The two comments that describe the native
	/// handout happening on a restore (<c>CharacterDataSync</c>'s "the game hands out the
	/// starting supplies inside generation" and <c>CharacterRestoreApplier</c>'s "the game's
	/// starting supplies … are already on the body when a restore runs") are the same fact
	/// from the live side.
	///
	/// <c>LoadedRun</c> is part of the condition because the game's own load path sets it
	/// (<c>PreRunScript.cs:302</c>); under CUO it is always false, since the Runtime has no
	/// source for it (<c>WorldParamsService</c>). It stays in the mirror so that a future
	/// source — a native load CUO lets through — cannot silently re-open the double grant.
	/// </summary>
	internal static bool NativeGrantCovers(WorldStartParams? baseline) =>
		baseline is not null
		&& baseline.TotalTraveled <= 0
		&& baseline.BiomeOverride == 0
		&& baseline.DebugStartDepth == 0
		&& !baseline.LoadedRun;

	/// <summary>
	/// The whole decision. <paramref name="restoredAtEntry"/> is the world's answer to "is
	/// there a character for me": a restore is queued for this very body (see
	/// <see cref="CharacterDataSync.RestorePending"/>), so it is not a new player. The grant
	/// otherwise follows the run's setting, unless the game's own first-layer grant already
	/// covered the body.
	/// </summary>
	internal static Decision Decide(bool restoredAtEntry, WorldStartParams? baseline, int? setting)
	{
		if (restoredAtEntry)
		{
			return new Decision(Reason.CharacterRestored, string.Empty, []);
		}

		if (baseline is null)
		{
			// No generation baseline is published at all: nothing describes this
			// generation, so nothing may be handed out on the strength of it.
			return new Decision(Reason.NoBaseline, string.Empty, []);
		}

		if (setting is null)
		{
			// No run settings: the tutorial, whose generation the game itself skips the
			// grant for (`biomeOverride == Tutorial`), and a run whose baseline was
			// published without them. Nothing is known to hand out, so nothing is.
			return new Decision(Reason.Disabled, "unset", []);
		}

		var plan = PlanFor(setting.Value);
		if (plan.Count == 0)
		{
			return new Decision(Reason.Disabled, Describe(setting.Value), []);
		}

		if (NativeGrantCovers(baseline))
		{
			return new Decision(Reason.AlreadyOwned, Describe(setting.Value), []);
		}

		return new Decision(Reason.Granted, Describe(setting.Value), plan);
	}
}

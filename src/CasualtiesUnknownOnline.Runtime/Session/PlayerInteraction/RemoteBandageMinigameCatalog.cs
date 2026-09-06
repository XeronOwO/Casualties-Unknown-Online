using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The carried items that run the native <c>BandageMinigame</c> when used on a
/// limb. Each entry carries the same full-item effect profile used by the
/// direct heal slice plus the native <c>num</c> denominator from
/// <c>Item.cs SetupItems</c>, which determines how many full rotations consume
/// one whole item. One wrap = 1/18 of a rotation, so one wrap consumes
/// <c>1/(18 * TurnsPerFullUse)</c> of the full profile/item.
/// </summary>
public static class RemoteBandageMinigameCatalog
{
	private sealed record Entry(RemoteHealProfile Profile, float TurnsPerFullUse);

	private static readonly IReadOnlyDictionary<string, Entry> Registry =
		new Dictionary<string, Entry>(StringComparer.Ordinal)
		{
			["bandage"] = EntryFor(conditionCost: 1f, turns: 12f, skinHeal: 30f, bandageSlow: 45f, pain: -60f, boneHeal: -20f, dislocation: -20f),
			["rippeddressing"] = EntryFor(conditionCost: 1f, turns: 8f, skinHeal: 8f, bandageSlow: 18f, pain: -40f, boneHeal: -5f, dislocation: -5f),
			["sterilizedbandage"] = EntryFor(conditionCost: 1f, turns: 12f, skinHeal: 30f, bandageSlow: 45f, pain: -60f, boneHeal: -20f, dislocation: -20f, disinfection: 900f),
			["plasticbandage"] = EntryFor(conditionCost: 1f, turns: 12f, skinHeal: 60f, bandageSlow: 72f, pain: -100f, boneHeal: -30f, dislocation: -30f),
			["analgesicgauze"] = EntryFor(conditionCost: 1f, turns: 15f, skinHeal: 20f, bandageSlow: 50f, pain: -300f, opiate: 28f),
			["alginate"] = EntryFor(conditionCost: 1f, turns: 15f, skinHeal: 125f, bandageSlow: 72.5f, pain: -80f, disinfection: 800f),
			["rag"] = EntryFor(conditionCost: 1f, turns: 8f, skinHeal: 8f, bandageSlow: 10f, pain: -25f, boneHeal: -5f, dislocation: -5f),
			["bruisekit"] = EntryFor(conditionCost: 1f, turns: 10f, skinHeal: 100f, pain: -80f, dislocation: -80f),
			["musharm"] = EntryFor(conditionCost: 1f, turns: 2.5f, skinHeal: 8f, bandageSlow: 10f),
		};

	public static bool IsBandageItem(string itemId) => Registry.ContainsKey(itemId);

	public static bool TryGet(string itemId, out RemoteHealProfile profile, out float turnsPerFullUse)
	{
		if (Registry.TryGetValue(itemId, out var entry))
		{
			profile = entry.Profile;
			turnsPerFullUse = entry.TurnsPerFullUse;
			return true;
		}

		profile = null!;
		turnsPerFullUse = 0f;
		return false;
	}

	private static Entry EntryFor(
		float conditionCost,
		float turns,
		float skinHeal = 0f,
		float bandageSlow = 0f,
		float pain = 0f,
		float boneHeal = 0f,
		float dislocation = 0f,
		float disinfection = 0f,
		float opiate = 0f)
	{
		var profile = new RemoteHealProfile(
			"",
			conditionCost,
			SkinHealAmount: skinHeal,
			BandageSlowAmount: bandageSlow,
			Pain: pain,
			BoneHealTimer: boneHeal,
			DislocationTimer: dislocation,
			DisinfectionTime: disinfection,
			OpiateAmount: opiate);
		return new Entry(profile, turns);
	}
}

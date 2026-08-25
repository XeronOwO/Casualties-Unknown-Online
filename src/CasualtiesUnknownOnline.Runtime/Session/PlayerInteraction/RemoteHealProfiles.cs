using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The known carried medical-item profiles for the cross-player heal slice.
/// Only limb-usable dressing/medicine items with a one-shot first-aid effect are
/// listed; container/drug/liquid-only items are deliberately outside this
/// slice. The registry is host-authoritative — the Online UI and GameAdapter
/// use it only as a read-only presence check, never as a source of truth.
/// </summary>
public static class RemoteHealProfiles
{
	private static readonly IReadOnlyDictionary<string, RemoteHealProfile> Registry =
		new Dictionary<string, RemoteHealProfile>(System.StringComparer.Ordinal)
		{
			["bandage"] = new("bandage", 1f, SkinHealAmount: 30f, BandageSlowAmount: 45f, Pain: -60f, BoneHealTimer: -20f, DislocationTimer: -20f),
			["rippeddressing"] = new("rippeddressing", 1f, SkinHealAmount: 8f, BandageSlowAmount: 18f, Pain: -40f, BoneHealTimer: -5f, DislocationTimer: -5f),
			["sterilizedbandage"] = new("sterilizedbandage", 1f, SkinHealAmount: 30f, BandageSlowAmount: 45f, Pain: -60f, BoneHealTimer: -20f, DislocationTimer: -20f, DisinfectionTime: 900f),
			["plasticbandage"] = new("plasticbandage", 1f, SkinHealAmount: 60f, BandageSlowAmount: 72f, Pain: -100f, BoneHealTimer: -30f, DislocationTimer: -30f),
			["adhesivebandage"] = new("adhesivebandage", 0.16f, SkinHealAmount: 3f, BandageSlowAmount: 6f, Pain: -5f),
			["analgesicgauze"] = new("analgesicgauze", 1f, SkinHealAmount: 20f, BandageSlowAmount: 50f, Pain: -300f, OpiateAmount: 28f),
			["alginate"] = new("alginate", 1f, SkinHealAmount: 125f, BandageSlowAmount: 72.5f, Pain: -80f, DisinfectionTime: 800f),
			["rag"] = new("rag", 1f, SkinHealAmount: 8f, BandageSlowAmount: 10f, Pain: -25f, BoneHealTimer: -5f, DislocationTimer: -5f),
			["bruisekit"] = new("bruisekit", 1f, SkinHealAmount: 100f, Pain: -80f, DislocationTimer: -80f),
		};

	public static bool IsHealItem(string itemId) => Registry.ContainsKey(itemId);

	public static bool TryGet(string itemId, out RemoteHealProfile profile) =>
		Registry.TryGetValue(itemId, out profile!);

	public static IEnumerable<RemoteHealProfile> All => Registry.Values;
}

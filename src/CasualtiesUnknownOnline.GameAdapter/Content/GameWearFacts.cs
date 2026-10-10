using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The game's own content facts behind the cross-player wearable (wear) chain,
/// the third of the three content readers beside <see cref="GameLimbUseFacts"/>
/// and <see cref="GameConsumeFacts"/>:
/// <list type="bullet">
/// <item><c>Item.GlobalItems</c> (Item.cs:7168) maps every item id — vanilla AND
/// mod-registered, because the item content provider writes into the same
/// dictionary — to its <c>ItemInfo</c>; <c>wearable</c> is the flag the game's
/// own wear FLOW dispatches on (<c>Body.AutoPickUpItem</c>, Body.cs:500, and
/// <c>PlayerCamera.TryPerformRadialAction</c>, PlayerCamera.cs:1640).</item>
/// <item><c>wearSlotId</c> is what the same flow's occupancy check compares
/// (<c>Body.GetWearableBySlotID</c>, Body.cs:1590) — and the game's own save file
/// records it as the worn item's slot (SaveSystem.cs:101), which is the evidence
/// that it, not the limb, identifies the slot.</item>
/// <item><c>desiredWearLimb</c> is the limb NAME <c>Body.LimbByName</c>
/// (Body.cs:1467) resolves. The name becomes the limb INDEX the character
/// snapshot encodes as <c>-(index + 2)</c>, and the array that index refers to is
/// the character prefab's own limb order — the same order in every body of one
/// session, and the order the snapshot itself is captured in.</item>
/// </list>
/// The operator's own client and the host's <see cref="IWearSemantics"/> seam both
/// read THIS type, so one predicate exists rather than two. The limb-name half has
/// a third reader with a different job: the wear guard
/// (<c>BodyPatches.WearWearablePatch</c>) refuses the game's own call on the same
/// answer, because <c>Body.WearWearable</c> dereferences what
/// <c>Body.LimbByName</c> returns without testing it.
/// </summary>
internal static class GameWearPlacement
{
	/// <summary>
	/// Resolve where the game wears <paramref name="itemId"/>: false when the
	/// item's own data does not make it a wearable, when the wear limb name is
	/// empty, or when <paramref name="body"/> cannot supply the limb layout (no
	/// body in this scene yet).
	/// </summary>
	internal static bool TryResolve(Body? body, string? itemId, out int limbIndex, out string wearSlotId)
	{
		limbIndex = -1;
		wearSlotId = "";
		if (string.IsNullOrEmpty(itemId)
			|| !Item.GlobalItems.TryGetValue(itemId, out var info)
			|| !IsPlaceable(body, info))
		{
			limbIndex = -1;
			return false;
		}

		wearSlotId = info.wearSlotId;
		return true;
	}

	/// <summary>
	/// True when this body can place <paramref name="info"/>: the item's own data
	/// makes it a wearable AND the limb name it declares resolves here. False is
	/// exactly the state the game's wear family dereferences null in — the flag is
	/// what selects those calls, and this is the one answer that says whether they
	/// have a limb to run on.
	/// </summary>
	internal static bool IsPlaceable(Body? body, ItemInfo? info) =>
		info is { wearable: true } && TryResolveLimbIndex(body, info.desiredWearLimb, out _);

	/// <summary>
	/// True when a <c>Body</c> query about <paramref name="itemId"/> must be refused
	/// instead of run: the id names a wearable whose declared limb this body does not
	/// carry. False for an empty or unknown id — the game's own behaviour for those
	/// stays exactly as it is (an unknown id throws in the game's own lookup, and that
	/// is not this rule's business).
	/// </summary>
	internal static bool Refuses(Body? body, string? itemId, out string limbName)
	{
		limbName = "";
		if (string.IsNullOrEmpty(itemId) || !Item.GlobalItems.TryGetValue(itemId, out var info))
		{
			return false;
		}

		return Refuses(body, info, out limbName);
	}

	/// <summary>The same answer for an item already in hand — the <c>HasWearable(Item)</c> and wear-call shape.</summary>
	internal static bool Refuses(Body? body, ItemInfo? info, out string limbName)
	{
		limbName = "";
		if (info is not { wearable: true })
		{
			return false;
		}

		limbName = info.desiredWearLimb ?? string.Empty;
		return !TryResolveLimbIndex(body, info.desiredWearLimb, out _);
	}

	/// <summary>
	/// The limb INDEX <paramref name="body"/> carries under
	/// <paramref name="limbName"/> — the loop <c>Body.LimbByName</c> runs
	/// (<c>Body.cs:1467-1477</c>, <c>limb.name == nm</c>), compared the same exact
	/// way. False is precisely the state the wear family dereferences null in
	/// (<c>Body.cs:1493-1494</c>, <c>:1541</c>, <c>:1558</c>, <c>:1575</c>): a call
	/// the guard must refuse rather than let the game run.
	/// </summary>
	internal static bool TryResolveLimbIndex(Body? body, string? limbName, out int limbIndex)
	{
		limbIndex = -1;
		if (body == null) // Unity object — ==
		{
			return false;
		}

		var limbs = body.limbs;
		if (limbs is null)
		{
			return false;
		}

		var names = new string?[limbs.Length];
		for (var i = 0; i < limbs.Length; i++)
		{
			names[i] = limbs[i] != null ? limbs[i].name : null; // Unity object — ==
		}

		return TryIndexOfLimbName(names, limbName, out limbIndex);
	}

	/// <summary>
	/// The name comparison itself, split from the body so the rule can be pinned
	/// without a live <c>Body</c>: the FIRST limb whose name equals
	/// <paramref name="limbName"/>, compared exactly as the game compares it
	/// (<c>Body.cs:1471</c> — no case folding), and false for an empty or unknown
	/// name.
	/// </summary>
	internal static bool TryIndexOfLimbName(IReadOnlyList<string?> limbNames, string? limbName, out int limbIndex)
	{
		limbIndex = -1;
		if (limbNames is null || string.IsNullOrEmpty(limbName))
		{
			return false;
		}

		for (var i = 0; i < limbNames.Count; i++)
		{
			if (limbNames[i] == limbName)
			{
				limbIndex = i;
				return true;
			}
		}

		return false;
	}
}

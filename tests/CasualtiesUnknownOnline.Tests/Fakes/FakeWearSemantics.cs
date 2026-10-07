using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The suite's stand-in for the game's own content facts behind
/// <see cref="IWearSemantics"/>. A test host has no game scene, so the vanilla
/// wearables the suite exercises are listed here with the placement
/// <c>Item.SetupItems()</c> gives them (<c>desiredWearLimb</c> resolved to the
/// character prefab's limb index, <c>wearSlotId</c> as the game's own occupancy
/// check compares it). The production verdict is never this list: it is the
/// game's registry plus the live limb layout (GameWearPlacement), so a stale entry
/// here can only make a suite test wrong, never a live session.
/// <para>
/// The index is the character PREFAB's limb order — the one
/// <c>CharacterDataCapture</c> numbers and <c>-(index + 2)</c> encodes — not a
/// name order this comment could state correctly from the tree alone. Every row
/// below reproduces the deleted catalog's own `(slot, index)` pair, and the three
/// families the indices fall into are the game's own (<c>Body.LimbNum</c>, the
/// unused enum at <c>Body.cs:4445</c>: arm 3-5, arm 6-8, leg 9-11, leg 12-14,
/// after the three central limbs), which is also why a wear slot
/// (<c>hat</c>, <c>thigh</c>, <c>hands</c>, …) belongs to exactly one limb in
/// vanilla data.
/// </para>
/// </summary>
internal sealed class FakeWearSemantics : IWearSemantics
{
	internal static readonly FakeWearSemantics Instance = new();

	/// <summary>The vanilla wearables these tests use, by id → (limb index, wear slot id).</summary>
	private static readonly Dictionary<string, (int LimbIndex, string WearSlotId)> Wearables =
		new(StringComparer.Ordinal)
		{
			["bikehelmet"] = (0, "hat"),
			["holidayhat"] = (0, "hat"),
			["dustmask"] = (0, "mouth"),
			["smallpack"] = (1, "back"),
			["hoodie"] = (1, "outertorso"),
			["armwarmers"] = (4, "arms"),
			["legpouch"] = (9, "thigh"),
			["sneakers"] = (11, "feet"),
			["latexgloves"] = (5, "hands"),
			["belt"] = (2, "belt"),

			// A wearable the deleted RemoteWearCatalog's 40 vanilla ids never
			// carried. The production answer comes from Item.GlobalItems, which mod
			// content registers into as well, so an id like this is exactly the
			// content the table could not reach.
			["testwearable"] = (1, "outertorso"),

			// The same trick for the wear SLOT rule: an id whose slot is one a
			// vanilla row above already occupies, but on a different limb — the
			// discrimination no vanilla pair can make (every vanilla slot maps to
			// one limb), and the reason the rule compares the slot id rather than
			// the limb.
			["testarmwrap"] = (1, "arms"),
		};

	public bool TryGetWearPlacement(string itemId, out int limbIndex, out string wearSlotId)
	{
		if (Wearables.TryGetValue(itemId, out var placement))
		{
			limbIndex = placement.LimbIndex;
			wearSlotId = placement.WearSlotId;
			return true;
		}

		limbIndex = -1;
		wearSlotId = "";
		return false;
	}
}

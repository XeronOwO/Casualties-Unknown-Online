using CasualtiesUnknownOnline.GameAdapter.Content;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The wear placement's guard family, beside the <c>WearWearable</c> prefix in
/// <see cref="BodyPatches"/>: every other <c>Body</c> member that dereferences the
/// limb an item's own data names, with no null check —
/// <c>GetWearable(string)</c> (<c>Body.cs:1541</c>), <c>HasWearable(string)</c>
/// (<c>:1558</c>) and <c>HasWearable(Item)</c> (<c>:1575</c>). A wearable flag CUO
/// installs selects all four calls, so before this family existed the same
/// unresolvable limb name was reachable from a carry, a wear slot check and a drag
/// release as well as from the wear itself.
///
/// <para>
/// Each guard asks the SAME rule (<see cref="GameWearPlacement"/>, the function the
/// cross-player chain places with) and answers with the meaning the query has for an
/// item that cannot be worn — "no such worn item", "not worn" — which is the game's
/// own answer for that state minus the exception. The refusal is logged through
/// <see cref="IWearablePatchPort"/>, the one thing a static patch cannot reach by
/// construction.
/// </para>
///
/// <para>
/// The display-proxy redirect (<c>RemoteDragPredicatePatches</c>) keeps its own
/// answer for <c>GetWearable</c>: it re-enters this same patched method on the body
/// the ring shows, so an unplaceable garment is refused there too, whichever prefix
/// Harmony runs first — every body of one session carries the same limb names.
/// </para>
/// </summary>
internal static class WearableQueryPatches
{
	/// <summary><c>Body.GetWearable(string)</c> — the worn item with this id, walked through the limb its data names.</summary>
	[HarmonyPatch(typeof(Body), "GetWearable")]
	internal static class GetWearablePlacementPatch
	{
		private static bool Prefix(Body __instance, string itemid, ref Item __result)
		{
			if (!GameWearPlacement.Refuses(__instance, itemid, out var limbName))
			{
				return true;
			}

			__result = null!; // "no worn item with this id" is the game's own answer for the state, minus the exception
			PatchBridge.Wearable?.ReportWearPlacementRefused(itemid, limbName);
			return false;
		}
	}

	/// <summary><c>Body.HasWearable(string)</c> — the same walk asked as a question (a pickup check and two tutorial courses read it).</summary>
	[HarmonyPatch(typeof(Body), "HasWearable", [typeof(string)])]
	internal static class HasWearableByIdPlacementPatch
	{
		private static bool Prefix(Body __instance, string itemid, ref bool __result)
		{
			if (!GameWearPlacement.Refuses(__instance, itemid, out var limbName))
			{
				return true;
			}

			__result = false;
			PatchBridge.Wearable?.ReportWearPlacementRefused(itemid, limbName);
			return false;
		}
	}

	/// <summary><c>Body.HasWearable(Item)</c> — the shape the pickup check asks about a carried container.</summary>
	[HarmonyPatch(typeof(Body), "HasWearable", [typeof(Item)])]
	internal static class HasWearableItemPlacementPatch
	{
		private static bool Prefix(Body __instance, Item item, ref bool __result)
		{
			if (item == null // Unity object — ==
				|| !GameWearPlacement.Refuses(__instance, item.Stats, out var limbName))
			{
				return true;
			}

			__result = false;
			PatchBridge.Wearable?.ReportWearPlacementRefused(item.id, limbName);
			return false;
		}
	}
}

using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Remote-clone head-sprite authority patch. The game's own
/// <c>FacialExpression.Update</c> picks the owner's head/mouth sprite from the
/// clone's local <c>Body</c> fields, but a frozen render proxy can present
/// different mouth triggers from the owner (stale/inherited slot contents,
/// head-limb latches, or the zeroed eat-time). This postfix restores the
/// owner's captured <see cref="HeadMouthState"/> on remote clones after the
/// game's formula has run; local players are untouched and disfigured bodies
/// keep the synced disfigurement sprites.
/// </summary>
[HarmonyPatch(typeof(FacialExpression), "Update")]
internal static class FacialExpressionHeadPatch
{
	private static void Postfix(FacialExpression __instance)
	{
		var body = __instance.body;
		if (body == null) // Unity object — ==
		{
			return;
		}

		var driver = body.GetComponent<RemoteBodyDriver>();
		if (driver == null) // Unity object — ==
		{
			return; // local player's own face is already the source of truth
		}

		if (body.disfigured)
		{
			return; // the game's disfigured-head path remains authoritative
		}

		if (__instance.head == null) // Unity object — ==
		{
			return;
		}

		__instance.head.sprite = driver.HeadMouth switch
		{
			HeadMouthState.Open => __instance.defaultHeadMouth,
			HeadMouthState.HalfOpen => __instance.defaultHeadMouthHalf,
			_ => __instance.defaultHead,
		};
	}
}

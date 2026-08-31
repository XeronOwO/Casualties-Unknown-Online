using CasualtiesUnknownOnline.GameAdapter.Tutorial;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Marks the tutorial claw's creations (Utils.Create inside the
/// TutorialClawSpawn scope — TutorialHandlerUpdatePatch) with
/// <see cref="TutorialClawProp"/>. The marker lands in the same postfix, so it
/// exists before the created Item's / BuildingEntity's Start runs and the
/// item/entity hooks can skip the shared-domain entry. The string/Vector2/float
/// overload is the only one TutorialHandler.Update calls
/// (TutorialHandler.cs:260).
/// </summary>
[HarmonyPatch(typeof(Utils), "Create",
	[typeof(string), typeof(Vector2), typeof(float)])]
internal static class UtilsCreateTutorialPatch
{
	private static void Postfix(GameObject? __result)
	{
		if (__result == null || CallContext.Current != CallContext.Origin.TutorialClawSpawn)
		{
			return;
		}

		__result.AddComponent<TutorialClawProp>();
	}
}

using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Fragile crystal → CrystalFragileBroken (one-shot): an item or body touched
/// it and its health dropped to 0 (CrystalFragile.cs:14-21 — the crystal
/// shatters; the drops roll on the triggering side). CrystalFragile is
/// INTERNAL to the game assembly (unreferencable at compile time) and its
/// Touched OVERRIDES the public base (a base-class patch cannot intercept the
/// CLR dispatch), so this patch is installed DYNAMICALLY by the adapter
/// (GameAdapter.InstallDynamicPatches) on the reflected type. The postfix
/// reads the health transition — the only Touched path that writes health = 0
/// for the fragile variant is its break.
/// </summary>
internal static class TrapCrystalPatch
{
	internal static void Postfix(object __instance)
	{
		if (__instance is not CrystalEffect crystal)
		{
			return;
		}

		if (crystal.crystal.build.health >= 0.5f)
		{
			return; // not broken by this touch (the touch conditions failed)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CrystalFragileBroken, crystal.crystal.transform.position, 0);
	}
}

using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Block damage sync (local compute, remote verify/sync): after ANY local
/// DamageBlock (mining/attacking blocks — Body.cs:1929, Limb.cs:384,
/// TurretScript.cs:144 all enter through this Vector2 overload, which calls the
/// Vector2Int overload with ignoreLoot=false, WorldGeneration.cs:851-854) the
/// adapter reports it so the peer applies the same damage at the same world
/// position. A BREAK (the block is gone — GetBlock == 0) is not reported
/// immediately: its drops are still being created inside the same call, and
/// the report waits one frame for them (the item domain folds them in, one
/// message, one verdict — PendingBlockBreak). The internal Vector2Int
/// overload's direct callers (footstep-crushing fragile blocks, Body.cs:2709;
/// the spider burrow, SpiderHandler.cs:218) are not hooked.
/// The Prefix opens the DamageBlockOrigin scope — the roll's Utils.Create
/// calls inside it get marked as block drops (UtilsCreateDropPatch).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	new Type[] { typeof(Vector2), typeof(float), typeof(bool), typeof(bool) })]
internal static class WorldGenerationDamageBlockPatch
{
	private static void Prefix(out IDisposable? __state) =>
		__state = CallContext.Enter(CallContext.Origin.DamageBlockOrigin);

	private static void Postfix(IDisposable? __state, Vector2 pos, float dmg)
	{
		try
		{
			PatchBridge.Impl?.OnBlockDamaged(pos, dmg);
		}
		finally
		{
			__state?.Dispose(); // a leaked scope would mask every later Create — release on exception paths too
		}
	}
}


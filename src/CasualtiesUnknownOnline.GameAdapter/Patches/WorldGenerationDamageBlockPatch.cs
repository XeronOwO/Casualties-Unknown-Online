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
/// calls inside it get marked as block drops (UtilsCreateDropPatch). Custom
/// tile drop entries are produced here too, before the block is gone from the
/// report, so they ride the same pending break.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	[typeof(Vector2), typeof(float), typeof(bool), typeof(bool)])]
internal static class WorldGenerationDamageBlockPatch
{
	private static bool Prefix(WorldGeneration __instance, Vector2 pos, out DamageBlockState? __state)
	{
		var cell = __instance.WorldToBlockPos(pos);
		var isLocalAction = CallContext.Current != CallContext.Origin.RemoteApply;
		__state = new DamageBlockState(
			__instance,
			cell,
			__instance.GetBlock(cell),
			isLocalAction,
			CallContext.Enter(CallContext.Origin.DamageBlockOrigin));
		return true;
	}

	private static void Postfix(DamageBlockState? __state, Vector2 pos, float dmg, bool bonusMetal)
	{
		try
		{
			if (__state is null)
			{
				return;
			}

			if (__state.IsLocalAction
				&& __state.World.GetBlock(__state.Cell) == 0)
			{
				PatchBridge.Impl?.OnCustomTileBroken(__state.World, __state.Cell, __state.OriginalBlock);
			}

			PatchBridge.Impl?.OnBlockDamaged(pos, dmg, bonusMetal);
		}
		finally
		{
			__state?.Dispose(); // a leaked scope would mask every later Create — release on exception paths too
		}
	}

	private sealed class DamageBlockState(
		WorldGeneration world,
		Vector2Int cell,
		ushort originalBlock,
		bool isLocalAction,
		IDisposable scope) : IDisposable
	{
		internal readonly WorldGeneration World = world;
		internal readonly Vector2Int Cell = cell;
		internal readonly ushort OriginalBlock = originalBlock;
		internal readonly bool IsLocalAction = isLocalAction;
		private readonly IDisposable _scope = scope;

		public void Dispose() => _scope.Dispose();
	}
}

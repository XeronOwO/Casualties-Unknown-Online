using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Block damage sync (local compute, remote verify/sync): after ANY local
/// DamageBlock — mining/attacking blocks (Body.cs:1929, Limb.cs:384,
/// TurretScript.cs:144), the footstep crush (Body.cs:2709) and the spider
/// burrow (SpiderHandler.cs:218) — the adapter reports it so the peer applies
/// the same damage at the same cell. The patch binds the BODY overload
/// (WorldGeneration.cs:711), the one every native caller enters: the Vector2
/// overload only converts a world position and forwards to it with
/// ignoreLoot=false (WorldGeneration.cs:851-854), so anchoring the forwarder
/// would leave the two callers that enter the body directly unreported
/// (guarded by DamageBlockHookCoverageGateTests).
/// A BREAK (the block is gone — GetBlock == 0) is not reported
/// immediately: its drops are still being created inside the same call, and
/// the report waits one frame for them (the item domain folds them in, one
/// message, one verdict — PendingBlockBreak).
/// The game gates its WHOLE loot step on ignoreLoot (WorldGeneration.cs:751),
/// so the custom-tile drops CUO produces for that step are gated the same way:
/// a caller that asked for no loot (the spider burrow) gets none either.
/// The Prefix opens the DamageBlockOrigin scope — the roll's Utils.Create
/// calls inside it get marked as block drops (UtilsCreateDropPatch). Custom
/// tile drop entries are produced here too, before the block is gone from the
/// report, so they ride the same pending break.
/// A REMOTE application — this side applying a peer's damage — runs through
/// this roll too and is wrapped in the caller's RemoteApply scope: the Prefix
/// still opens the damage scope, and the report is skipped because the peers
/// already have it (and every remote apply passes ignoreLoot=true, so the loot
/// step cannot run there either).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "DamageBlock",
	[typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool)])]
internal static class WorldGenerationDamageBlockPatch
{
	private static bool Prefix(WorldGeneration __instance, Vector2Int pos, out DamageBlockState? __state)
	{
		// The chain query, not Current: the caller may already be inside a classification sub-scope
		// (Craft, InternalReorder, CharacterMedicalUse) when a roll is applied, and a nested scope must
		// not turn a remote-applied roll into a local one. The row-4 write echo itself was raised by
		// WorldEventSync.OnBlockSet's guard, which the same chain query now protects.
		var isLocalAction = !CallContext.IsWithin(CallContext.Origin.RemoteApply);
		__state = new DamageBlockState(
			__instance,
			pos,
			__instance.GetBlock(pos),
			__instance.GetBlockDamage(pos)?.damage ?? 0f,
			isLocalAction,
			CallContext.Enter(CallContext.Origin.DamageBlockOrigin));
		return true;
	}

	private static void Postfix(DamageBlockState? __state, float dmg, bool bonusMetal, bool ignoreLoot)
	{
		try
		{
			if (__state is null || !__state.IsLocalAction)
			{
				return; // a remote apply: the peers already have this damage, and the local-report hook stays silent
			}

			if (!ignoreLoot && __state.World.GetBlock(__state.Cell) == 0)
			{
				PatchBridge.Impl?.OnCustomTileBroken(__state.World, __state.Cell, __state.OriginalBlock);
			}

			// How much this call actually added to the cell's row, in the game's own
			// accumulated units (the metallic ×10 is already folded in,
			// WorldGeneration.cs:715) — the sender's own contribution, which the
			// per-sender accounting reports instead of the cell's total. A hit that
			// BROKE the block removed the row inside the call, so the reading says
			// nothing; the break path owns that cell instead.
			var applied = __state.World.GetBlock(__state.Cell) == 0
				? 0f
				: (__state.World.GetBlockDamage(__state.Cell)?.damage ?? 0f) - __state.PreviousDamage;
			PatchBridge.Impl?.OnBlockDamaged(__state.Cell, dmg, bonusMetal, applied);
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
		float previousDamage,
		bool isLocalAction,
		IDisposable scope) : IDisposable
	{
		internal readonly WorldGeneration World = world;
		internal readonly Vector2Int Cell = cell;
		internal readonly ushort OriginalBlock = originalBlock;

		/// <summary>The cell's accumulated damage BEFORE this call — the other half of the applied increment.</summary>
		internal readonly float PreviousDamage = previousDamage;
		internal readonly bool IsLocalAction = isLocalAction;
		private readonly IDisposable _scope = scope;

		public void Dispose() => _scope.Dispose();
	}
}

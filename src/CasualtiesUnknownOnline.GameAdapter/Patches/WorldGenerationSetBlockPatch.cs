using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-mutation capture on the SetBlock write path (the one post-generation
/// write entry — mining, remote damage application, earthquakes, placement).
/// Host: diff against the generated baseline (damage table, late-joiner full
/// snapshot) + broadcast the mutation live (air writes included — the quake
/// breaks SetBlock(0) with per-side random, so without the relay the terrain
/// diverges). Guest: report local mutations for arbitration (mining
/// double-reports via BlockDamaged — idempotent). Generation itself is the
/// baseline and is excluded via generatingWorld; SetBlockNoUpdate is
/// generation-only and intentionally not hooked.
/// QUAKE REGIONS are the UNION of every side's breaks: each side quakes on
/// the synced timer and breaks its own nearby region; the air-write relay
/// applies every side's breaks everywhere, and SetBlock(0) is idempotent —
/// overlapping regions are computed exactly once (user mandate). The PREFIX
/// gates quake breaks by the numbering-distance rule so overlapping players
/// do not double the total break rate (see ShouldApplyQuakeBreak).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
internal static class WorldGenerationSetBlockPatch
{
	private static bool Prefix(Vector2Int pos, ushort block)
	{
		// Quake/environment breaks only (inside WorldGeneration.Update, air
		// writes) — mining and placement are never gated. A REMOTE application
		// (a peer's break being applied here) must pass unconditionally: it
		// already passed the source side's gate, and the local gate would
		// otherwise swallow host-broadcast breaks whenever this side's Update
		// frame happens to be running (which, during a quake, is every frame —
		// "the guest's broken blocks are a subset of the host's").
		if (block == 0 && WorldGenerationUpdatePatch.InUpdate && PatchBridge.Impl is { } bridge
			&& CallContext.Current != CallContext.Origin.RemoteApply)
		{
			return bridge.ShouldApplyQuakeBreak(pos);
		}

		return true;
	}

	/// <summary>
	/// Report only writes that actually landed. The PREFIX gate (ShouldApplyQuakeBreak)
	/// can return false — a quake break inside an earlier player's region is
	/// swallowed and SetBlock never runs — but Harmony still runs this postfix.
	/// Reporting a swallowed write made the HOST apply a break that never happened
	/// on this side (host: empty, this side: still solid — "the guest's breaks
	/// are a proper subset of the host's"). The landed check reads the table the
	/// write just touched: swallowed writes leave the original block, landed
	/// writes equal the requested block (same-value SetBlock stays idempotent).
	/// </summary>
	private static void Postfix(Vector2Int pos, ushort block)
	{
		if (WorldGeneration.world.GetBlock(pos) == block) // Unity object — ==; the write landed
		{
			PatchBridge.Impl?.OnBlockSet(pos, block);
		}
	}
}

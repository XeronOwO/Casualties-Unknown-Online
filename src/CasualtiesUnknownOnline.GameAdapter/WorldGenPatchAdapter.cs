using System.Collections;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The world-generation patch port's implementation: the generation-pass hooks and the cell-keyed block writes, each forwarded to the domain that owns the
/// behaviour.
/// </summary>
/// <remarks>
/// A composed object rather than members on <see cref="GameAdapterBridge"/>, which is the direction that class's own doc names: the bridge is frozen, and a domain
/// that grows hooks grows them here. A patch that needs one generation hook states the port and cannot reach the other hundred members of the bridge through it.
/// </remarks>
internal sealed class WorldGenPatchAdapter(GameAdapterDomains domains) : IWorldGenPatchBridge
{
	public void OnWorldGenerate()
	{
		domains.Run.OnWorldGenerate();
		// Every enemy id and match hint names the layer being left: the roster state starts empty on BOTH roles, and the host holds snapshots until its new capture is complete.
		domains.EnemySync.OnWorldGenerationStarted();
		domains.Gate.AttachKeepLoading(); // both roles: while the gate waits for the others, the loading animation keeps playing
										  // The tutorial/debug layers skip the game's ResetLayerModifiers
										  // (WorldGeneration.cs:3280-3283 — it only runs for biomeOverride == None),
										  // so the previous layer's modifier keeps its static active state: a run
										  // with a modifier followed by the tutorial showed no banner (the tutorial
										  // never rolls one, WorldGeneration.cs:3626-3628) but the modifier's
										  // effects still ran. Clear it on EVERY side — the modifier instances are
										  // process-static (LayerModifier.availableModifiers), one set per game;
										  // the guest's active was set by the snapshot path.
		if (WorldGeneration.world != null && HarmonyTraverse.ReadBiomeOverride() != 0) // Unity object — ==; 0 = None
		{
			WorldGeneration.world.ResetLayerModifiers();
			domains.Log.LogInformation("[LayerMod] non-none layer — cleared the previous layer's residual modifiers.");
		}

		if (domains.Session.Role != SessionRole.Guest)
		{
			// A new world/layer is generating — the old layer's world items are
			// gone with the scene; the authoritative table starts empty again
			// (regression guard: this call lived inside the old GameAdapter's
			// OnWorldGenerate and was lost in the domain split).
			domains.Items.ResetItems();
			domains.ItemWorldSync.ResetPending(); // a pending drop's item is gone with the old layer
			domains.BlockBreakSync.ResetPending(); // a pending break's drops are gone with the old layer
		}
	}

	public IEnumerator WrapStructureWorldGen(IEnumerator original, WorldGeneration world) =>
		domains.StructureWorldGen.Wrap(original, world);

	public void OnCustomTileOreGeneration(WorldGeneration world) =>
		domains.TileWorldGen.Distribute(world);

	public void OnCustomLiquidWorldGeneration(WorldGeneration world) =>
		domains.LiquidTileWorldGen.Distribute(world);

	public void OnCustomBuildingWorldGeneration(WorldGeneration world) =>
		domains.BuildingWorldGen.Distribute(world);

	public void OnCustomItemWorldGeneration(WorldGeneration world) =>
		domains.ItemWorldGen.Scatter(world);

	public void OnBlockSet(Vector2Int pos, ushort block) => domains.WorldEventSync.OnBlockSet(pos, block);

	public void OnBlockDamaged(Vector2Int cell, float dmg, bool bonusMetal, float applied) =>
		domains.BlockBreakSync.OnBlockDamaged(cell, dmg, bonusMetal, applied);

	public void OnCustomTileBroken(WorldGeneration world, Vector2Int cell, ushort block) =>
		domains.TileContent.TrySpawnDrops(world, cell, block);
}

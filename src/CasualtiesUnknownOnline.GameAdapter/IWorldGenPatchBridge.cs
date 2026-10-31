using System.Collections;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The world-generation patch port: the hooks the generation pass and the block-write pass call while the game is building or editing the world. It is a port
/// rather than part of <see cref="IPatchBridge"/> because these hooks all run inside the sealed generation stream or the cell-keyed write path, and a patch that
/// needs one of them should say so instead of holding the whole bridge.
/// </summary>
/// <remarks>
/// The aggregate does not declare these members; <c>PatchBridge.WorldGen</c> casts the one bound implementation, the same shape the fluid, layer-advance and
/// item-category ports already use. The mod-content distribution hooks (structure, ore, liquid, building, item) belong together because they all run between the
/// vanilla passes and the collider pass, in that order, on the same sealed stream.
/// </remarks>
internal interface IWorldGenPatchBridge
{
	/// <summary>The host: capture the world-start parameters and publish them as the generation baseline; the guest: apply the host's.</summary>
	void OnWorldGenerate();

	/// <summary>
	/// Wrap the vanilla <c>WorldGenerateWorldBorders</c> iterator so the Game Adapter can distribute mod-bound structures after the vanilla worldgen has finished
	/// its terrain/structures but before the collider pass. The returned coroutine is driven by <see cref="WorldGenRandomIsolation"/>, so its Random consumption
	/// stays on the sealed generation stream.
	/// </summary>
	IEnumerator WrapStructureWorldGen(IEnumerator original, WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.GenerateOres</c> finished — distribute mod-bound custom tile ore after the vanilla ore pass but before the structure/liquid
	/// passes, all inside the sealed generation stream.
	/// </summary>
	void OnCustomTileOreGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.GenerateOres</c> finished — distribute mod-bound liquid-tile pools after the vanilla ore pass, inside the same sealed generation
	/// stream.
	/// </summary>
	void OnCustomLiquidWorldGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.PlaceCrystals</c> finished — distribute mod-bound custom building entities after colliders exist, inside the same sealed
	/// generation stream. The deterministic buildings never need a wire message; runtime Start reports are suppressed during generation.
	/// </summary>
	void OnCustomBuildingWorldGeneration(WorldGeneration world);

	/// <summary>
	/// Vanilla <c>WorldGeneration.PlaceCrystals</c> finished — scatter mod-bound custom items' loose world spawns, inside the same sealed generation stream. The
	/// resulting ground items are later bound by the existing generation-item snapshot.
	/// </summary>
	void OnCustomItemWorldGeneration(WorldGeneration world);

	/// <summary>A block write landed (the local player's or a generation pass's): report it so the peers apply the same cell.</summary>
	void OnBlockSet(Vector2Int pos, ushort block);

	/// <summary>
	/// A LOCAL <c>WorldGeneration.DamageBlock</c> roll finished: the raw damage plus how much THIS call added to the cell's row. The CELL travels, not a world
	/// position — the patch binds the overload that already holds it, and the peers apply the report by cell.
	/// </summary>
	void OnBlockDamaged(Vector2Int cell, float dmg, bool bonusMetal, float applied);

	/// <summary>
	/// A local block break hit a custom tile index. The Game Adapter spawns the tile's authored drops while still inside the damage-block scope so they ride the
	/// pending break report like vanilla block drops.
	/// </summary>
	void OnCustomTileBroken(WorldGeneration world, Vector2Int cell, ushort block);
}

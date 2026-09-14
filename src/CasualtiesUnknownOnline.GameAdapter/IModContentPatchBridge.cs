using CasualtiesUnknownOnline.Abstractions;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The mod-content half of the Harmony patch bridge: the resolution seams a
/// patch needs so a mod-registered template, drop source or tile behaviour can
/// be served from the content providers instead of vanilla <c>Resources.Load</c>.
/// Kept as its own interface so <see cref="IPatchBridge"/> stays under the
/// architecture line gate while the mod-content surface has one focused seam
/// (same reason <see cref="IRemoteMedicalPatchBridge"/> exists).
/// </summary>
internal interface IModContentPatchBridge
{
	/// <summary>
	/// Resolve a mod-registered runtime item template by item id. Returns false
	/// when the id has no custom template; the caller falls back to
	/// <c>Resources.Load</c> for vanilla items.
	/// </summary>
	bool TryResolveItemTemplate(string id, out GameObject? template);

	/// <summary>
	/// Resolve a mod-registered runtime building template by building id.
	/// Returns false when the id has no custom template; the caller falls back
	/// to <c>Resources.Load</c> for vanilla buildings.
	/// </summary>
	bool TryResolveBuildingTemplate(string id, out GameObject? template);

	/// <summary>
	/// Apply a registered runtime building instance hook to a freshly
	/// instantiated custom building. The instance is still inactive when this is
	/// called, so hook-returned components attach before <c>Awake</c> runs.
	/// </summary>
	void ApplyCustomBuildingInstanceHooks(string id, GameObject instance);

	/// <summary>
	/// Resolve the synthetic <c>ItemLootPool</c> category for a fixed drop
	/// source. Returns false when no custom items have been registered for that
	/// source or the loot pool is not ready yet.
	/// </summary>
	bool TryGetModDropSourceCategory(ModItemDropSource source, out string category);

	/// <summary>
	/// Returns the mod-authored <see cref="BlockInfo"/> for a custom tile
	/// index, or null when the block is vanilla/unregistered. The
	/// <c>WorldGeneration.GetBlockInfo</c> patch uses this to let the original
	/// switch continue handling every vanilla block while supplying behavior for
	/// static custom tiles.
	/// </summary>
	BlockInfo? TryGetCustomBlockInfo(ushort block);
}

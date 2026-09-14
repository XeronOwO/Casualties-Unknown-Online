using CasualtiesUnknownOnline.Abstractions;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The mod-content half of the patch bridge (<see cref="IModContentPatchBridge"/>):
/// it forwards a patch's template / drop-source / tile-behaviour questions to the
/// content providers that own them. Its own class because the surface is the
/// mod-content registry seam, not the adapter's runtime state — and because
/// keeping it off <see cref="GameAdapterBridge"/> is what holds that class under
/// the architecture line gate.
/// </summary>
internal sealed class ModContentPatchBridge(GameAdapterDomains domains) : IModContentPatchBridge
{
	public bool TryResolveItemTemplate(string id, out GameObject? template) =>
		domains.ItemContent.TryResolveTemplate(id, out template);

	public bool TryResolveBuildingTemplate(string id, out GameObject? template) =>
		domains.BuildingContent.TryResolveTemplate(id, out template);

	public void ApplyCustomBuildingInstanceHooks(string id, GameObject instance) =>
		domains.BuildingContent.ApplyInstanceHook(id, instance);

	public bool TryGetModDropSourceCategory(ModItemDropSource source, out string category) =>
		domains.ItemContent.TryGetDropSourceCategory(source, out category);

	public BlockInfo? TryGetCustomBlockInfo(ushort block) =>
		domains.TileContent.TryGetBlockInfo(block);
}

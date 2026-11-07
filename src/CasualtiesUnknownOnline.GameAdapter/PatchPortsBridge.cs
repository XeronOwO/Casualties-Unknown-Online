using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The per-domain patch ports the frozen aggregate deliberately does not carry: the fluid domain's
/// tick/drink/custom-liquid surface, the layer advance's delegation, the end-of-layer panel's close,
/// the save cut's panel entry, the drag-use cue, the item category's data anchor and refusal report,
/// the wear placement's refusal report, and the spectator mode's death-screen entry.
///
/// Extracted from <see cref="GameAdapterBridge"/> as the architecture watchlist's named seam; the
/// aggregate keeps the frozen <see cref="IPatchBridge"/> surface, and <see cref="PatchBridge"/>
/// resolves each port by casting this object. Every member is a one-line forward to its domain.
/// </summary>
internal sealed class PatchPortsBridge(GameAdapterDomains domains) : IFluidPatchPort, ILayerAdvancePatchPort, IItemCategoryPatchPort, IWearablePatchPort, IDragUseCuePatchPort, ISpectatorPatchPort, ISaveCutPatchPort, ILayerPanelPatchPort
{
	// ===== Fluid =====

	public void OnFluidFixedUpdate() => domains.FluidSync.OnFluidFixedUpdate();

	public void OnFluidDrinkReported(Vector2Int pos) => domains.FluidSync.OnDrinkReported(pos);

	public bool TryRenderCustomLiquids(FluidManager manager) =>
		domains.LiquidTileRender.TryRender(manager);

	public bool TryGetCustomLiquidColor(byte worldByte, out Color color) =>
		domains.LiquidTileContent.TryGetDisplayColor(worldByte, out color);

	public bool TryGetCustomWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type) =>
		domains.LiquidTileContent.TryGetWaterInfo(worldByte, out buoyancy, out drag, out type);

	public bool TryGetCustomLiquidName(byte worldByte, out string name, out string description) =>
		domains.LiquidTileContent.TryGetDisplayName(worldByte, out name, out description);

	public bool TryDrinkCustomLiquid(FluidManager fluid, Vector2Int pos, Body body) =>
		domains.LiquidTileDrink.TryDrink(fluid, pos, body);

	public void ApplyLiquidTileBodyTouch(Body body) =>
		domains.LiquidTileBodyTouch.Apply(body);

	// ===== Layer advance =====

	/// <summary>The member's own descent the session owns (its end-of-layer choice or the drill pod's): the local regeneration must not follow (the layer is the host's capture), and the native two-layer flag rides the request.</summary>
	public bool TryDelegateLocalAdvance(bool twice) => domains.LayerAdvance.TryDelegateLocalAdvance(twice);

	/// <summary>A local descent that is NOT suppressed: on the host of a live session it is the shared edge every member follows, and the presentation window opens with it.</summary>
	public void OnHostDescent(bool twice)
	{
		domains.Run.OnHostDescentStarted();
		domains.LayerAdvance.OnHostDescent(twice);
	}

	// ===== End-of-layer panel =====

	/// <summary>
	/// The end-of-layer panel's close, asked right after the game's own panel write
	/// (<see cref="Patches.WorldGenerationPanelClosePatch"/>): the native latch re-opens whatever a later phase closed.
	/// </summary>
	public void CloseTheEndOfLayerPanelIfThisClientMustNotShowIt() => domains.LayerPanel.CloseThePanelThisClientMustNotShow();

	/// <summary>
	/// The end-of-layer panel's "Save and exit" takes CUO's own cut instead of the game's native save (the user's
	/// 2026-10-24 ruling: the native save is meaningless to CUO). The panel's entry is REPLACED, so the native body
	/// must not run at all — it would write `save.sv`, advance the depth without generating and leave the world in one
	/// go, none of which CUO wants. The cut itself is the DEFERRED menu return the game's own leave already uses: the
	/// request is recorded here and `SaveCutSeam` takes the cut at the pump's frame-end seam and only then leaves, so a
	/// cut can never read a half-left world (an inline "arm then leave" was read on the machine: the seam found no
	/// world to cut because the leave had already happened).
	/// </summary>
	public bool TakeWorldCutInsteadOfTheNativeSave()
	{
		if (WorldGeneration.world == null) // Unity object — ==
		{
			return false;
		}

		domains.MenuReturn.Request(domains.Session.Role, RunMenuReturnOrigin.PlayerLeave, inWorld: true);
		domains.Log.LogInformation("[LayerChoice] save-and-exit records the deferred menu return: the seam takes the CUO cut and then leaves, so the native save is never written.");
		return true;
	}

	// ===== Drag-use cue =====

	public void UpdateDragUseCue(Item? dragItem) => domains.DragUseCue.Update(dragItem);

	// ===== Item category =====

	/// <summary>The item category's data anchor, forwarded from the Runtime's own item data: the static patches read it here, the shared classifiers read it through <c>StandingItems</c>.</summary>
	public bool IsWorldItemRegistered(ulong itemId) => domains.Items.IsWorldItemRegistered(itemId);

	/// <summary>The refusal half of the same port: the pickup gate refused a local gesture because its target is a standing item object.</summary>
	public void ReportStandingItemGestureRefused(Item item, string source) =>
		domains.Log.LogWarning("[StandingItem] {Type} (id {ItemId}) refused by {Source} — a standing item object is another member's carried item and is never reachable by a local gesture.",
			item.id, item.GetComponent<ItemInstanceId>()?.Id ?? 0, source);

	// ===== Wear placement =====

	/// <summary>The wear placement port's one report: a read of or gesture on a wearable whose declared limb this body does not carry was refused — the null the game's own wear family dereferences (<c>Body.cs:1493-1494</c>, <c>:1541</c>, <c>:1558</c>, <c>:1575</c>).</summary>
	public void ReportWearPlacementRefused(string itemId, string limbName) =>
		domains.Log.LogWarning("[Wear] refused: item {Item} declares wear limb '{Limb}', which no limb of this body carries — the game's own placement would dereference the missing limb (Body.cs:1493-1494, :1541, :1558, :1575).",
			itemId, limbName);

	// ===== Spectator =====

	/// <summary>The death receipt's menu button (the ruling's second entrance): a dead player on the receipt enters
	/// the spectator mode instead of the menu load that follows the click.</summary>
	public bool TryEnterFromDeathScreen()
	{
		var camera = PlayerCamera.main;
		if (camera == null || camera.body == null || camera.body.alive || !camera.didDeathScreen) // Unity objects — ==
		{
			return false;
		}

		return domains.Spectator.EnterSpectatorMode();
	}
}

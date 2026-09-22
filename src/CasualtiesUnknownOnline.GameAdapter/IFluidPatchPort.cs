using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The fluid domain's patch port: what a <c>FluidManager</c> / liquid-tile hook
/// may reach — the session simulation tick, the local drink report, the
/// custom-liquid-byte resolution and application, and the per-second tile-touch
/// re-application. A patch that needs the fluid domain reads
/// <c>PatchBridge.Fluid</c> and thereby states that dependency, instead of
/// reaching the whole <see cref="IPatchBridge"/> aggregate
/// (<c>review/patch-bridge-domain-ports.md</c>, stage 1).
/// <para>
/// The aggregate does NOT compose this port and does not declare these members:
/// a call written against <see cref="IPatchBridge"/> cannot reach the fluid
/// surface at all, so the migration is enforced by the compiler rather than by
/// convention, and the port cannot degrade into the aggregate under a new name.
/// The custom-liquid byte resolution sits here rather than in
/// <see cref="IModContentPatchBridge"/> because every consumer is a
/// <c>FluidManager</c> hook and the fluid domain owns the byte table.
/// </para>
/// <para>
/// The seam resolves it by casting the bound bridge, which the only
/// implementation (<c>GameAdapterBridge</c>) satisfies; the shape gate pins that
/// class declaration, so a bridge that stopped serving the port fails there.
/// </para>
/// </summary>
internal interface IFluidPatchPort
{
	/// <summary>A fluid fixed-update tick — the session replaces the game's per-side simulation (host: the multi-member pass over every member's viewport; guest: nothing — the grid only changes through the streamed regions).</summary>
	void OnFluidFixedUpdate();

	/// <summary>The local player drank (DrinkLiquid ran with the full local effect) — report the consumed cell (guest → host; host → broadcast).</summary>
	void OnFluidDrinkReported(Vector2Int pos);

	/// <summary>
	/// <c>FluidManager.RenderFluids</c> is about to render. Returns true when
	/// custom liquid tiles are present and the adapter rendered them (the
	/// original must be skipped); false keeps the vanilla render path.
	/// </summary>
	bool TryRenderCustomLiquids(FluidManager manager);

	/// <summary>Resolve the display colour for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomLiquidColor(byte worldByte, out Color color);

	/// <summary>Resolve water info for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomWaterInfo(byte worldByte, out float buoyancy, out float drag, out int type);

	/// <summary>Resolve display name/description for a custom world-fluid byte. Returns false for vanilla bytes.</summary>
	bool TryGetCustomLiquidName(byte worldByte, out string name, out string description);

	/// <summary>
	/// <c>FluidManager.DrinkLiquid</c> is about to run on a custom world-fluid
	/// byte. Returns true when the adapter applied the drink (the original must
	/// be skipped); false lets the vanilla method handle it.
	/// </summary>
	bool TryDrinkCustomLiquid(FluidManager fluid, Vector2Int pos, Body body);

	/// <summary>
	/// <c>Body.HandleVariableUpdates</c> finished — re-apply the local body's
	/// per-second liquid-tile touch rates. The projection class filters to the
	/// local body.
	/// </summary>
	void ApplyLiquidTileBodyTouch(Body body);
}

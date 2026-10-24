using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The end-of-layer panel's close, in the frame the game opened it. <c>WorldGeneration.Update</c> opens
/// <c>savePanel</c> with a latch — <c>if (!this.savePanel.activeSelf &amp;&amp; … &amp;&amp; PlayerCamera.main.body.y &lt;
/// -halfHeight + 3.1f) this.savePanel.SetActive(true)</c> — that tests only the LOCAL body's height, so it re-opens any
/// panel a later phase closed. The host rule needs the panel shown only when the share of LIVING players at the floor
/// reaches <c>LayerCompleteLivingShare</c>, so the close runs immediately after that write, still inside the game's own
/// update: the panel then ends the frame closed instead of flipping.
///
/// <para>
/// The OPEN half stays with the late-frame pump (<c>ILayerPanelVisibilityPump</c>), because the latch can never open the
/// panel for the case the rule exists for — a host whose own body never reached the floor while the rest of the living
/// group did. Two writers, one rule (<c>LayerPanelVisibility</c>), each in the phase that can express its half.
/// </para>
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "Update")]
internal static class WorldGenerationPanelClosePatch
{
	private static void Postfix() => PatchBridge.LayerPanel?.CloseTheLayerPanelIfTheShareIsNotMet();
}

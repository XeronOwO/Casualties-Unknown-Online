using BepInEx;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The plug-in shell: it exists so a BepInEx client loads this assembly as a plug-in too, which keeps the mod a
/// normal BepInEx package; CUO discovers the <see cref="RecursiveCraftingMod"/> entry in the same assembly on its
/// own. It does no patching and touches no game type beyond Unity's logger, so a CUO-less client loads and idles
/// here with nothing to break — and the entry, not this shell, binds the log sink (this Awake may not run when
/// CUO discovers the assembly by itself).
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class RecursiveCraftingPlugin : BaseUnityPlugin
{
	private void Awake() =>
		Logger.LogInfo("[Recursive] plug-in shell loaded; " + (CraftingPanelInstaller.Ensure()
			? "recipe-panel third state installed."
			: "recipe-panel third state FAILED to install (see the warnings).") + " /craftchain is registered by the CUO mod entry.");
}

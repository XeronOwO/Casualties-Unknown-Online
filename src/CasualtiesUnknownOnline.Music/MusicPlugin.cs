using BepInEx;

namespace CasualtiesUnknownOnline.Music;

/// <summary>
/// The plug-in shell: it exists so a BepInEx client loads this assembly as a normal plug-in package; CUO
/// discovers the <see cref="MusicMod"/> entry inside it on its own. No patching, no game types — a CUO-less client
/// loads and idles here with nothing to break.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class MusicPlugin : BaseUnityPlugin
{
	private void Awake() =>
		Logger.LogInfo("[Music] plug-in shell loaded; /music is registered by the CUO mod entry.");
}

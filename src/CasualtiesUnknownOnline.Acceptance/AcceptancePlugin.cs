using BepInEx;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The acceptance channel's plug-in shell: it exists so a BepInEx client loads this assembly, which is what makes the
/// instruments inside it reachable through HotRepl's in-process evaluator. A production install does not contain this
/// assembly at all — the shipped mod never references it and the deploy script only copies it for a batch.
///
/// <para>
/// Nothing is patched on load: a batch installs the channel explicitly (<c>virtual-input mode=install</c>), so a client
/// that happens to have this file keeps the real keyboard and mouse until a batch says otherwise.
/// </para>
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class AcceptancePlugin : BaseUnityPlugin
{
	private void Awake() => Logger.LogInfo("[Acceptance] channel plug-in loaded; nothing is patched until a batch installs it.");
}

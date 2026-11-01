using BepInEx;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The acceptance channel's plug-in shell: it exists so a BepInEx client loads this assembly, which is what makes the
/// instruments inside it reachable through HotRepl's in-process evaluator. A production install does not contain this
/// assembly at all — the shipped mod never references it and the deploy script only copies it for a batch.
///
/// <para>
/// Only the passive recorder is patched on load: it records nothing until a batch arms it, so a client that happens to have
/// this file pays nothing and keeps the real keyboard and mouse. The staged input channel is NOT installed here — a batch
/// installs that one explicitly, because it answers the game's own input queries.
/// </para>
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class AcceptancePlugin : BaseUnityPlugin
{
	private void Awake()
	{
		// The recorder's patches are attributes on the type and nothing applies them unless something asks. Without this
		// the ring buffer stays empty and a batch reads zero for a sound the game really played (measured 2026-11-02: two
		// clients answered count=0 for every Sound.Play of the batch).
		new Harmony("CasualtiesUnknownOnline.Acceptance").PatchAll(typeof(SoundPlayRecorder));
		Logger.LogInfo("[Acceptance] channel plug-in loaded; the recorder is armed, input is staged only when a batch installs it.");
	}
}

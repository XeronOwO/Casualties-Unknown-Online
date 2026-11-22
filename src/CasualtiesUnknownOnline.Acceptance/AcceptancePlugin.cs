using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

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
	private Harmony? _harmony;

	private void Awake()
	{
		// A batch drives two clients at once; the host's window is usually behind the member's. Unity stops
		// advancing frames for a backgrounded window by default, which leaves time-based rows reading a stale
		// pause ramp or a frozen clock. The acceptance channel is test-only, so keep its frames live while it is
		// behind another client (measured 2026-11-15: pauseProgress 0.006 after three seconds).
		Application.runInBackground = true;

		// The client that comes up last raises its own window over the person's work; it belongs behind, and the client is
		// the only place that knows about every start — a case restarts a client minutes into a run, after anything the

		// The recorder's patches are attributes on the type and nothing applies them unless something asks. Without this
		// the ring buffer stays empty and a batch reads zero for a sound the game really played (measured 2026-11-02: two
		// clients answered count=0 for every Sound.Play of the batch).
		_harmony = new Harmony("CasualtiesUnknownOnline.Acceptance");
		// One channel's patch failing must not blind the others: the evaluator's game-assembly reference is installed
		// last here, so an exception in an earlier install once left every recipe unable to name a game type
		// (measured 2026-11-22: an AmbiguousMatchException on a two-overload method produced CS0103 in every recipe).
		Try("the sound recorder", () => SoundPlayRecorder.Install(_harmony!));
		Try("the block writer", () => BlockWriteRecorder.Install(_harmony!));
		Try("the facial probe", () => FacialBodyProbe.Install(_harmony!));
		Try("the remote view seam", () => RemoteViewRedPatch.Install(_harmony!));
		Try("the remote view trace", () => RemoteViewTracePatch.Install(_harmony!));
		Try("the swing counter", () => SwingProbePatch.Install(_harmony!));
		Try("the evaluator's game reference", () => HotReplRoslynGameReferencePatch.TryInstall(_harmony!));
		Logger.LogInfo("[Acceptance] channel plug-in loaded; the recorder is armed, input is staged only when a batch installs it.");
	}

	private void Try(string what, Action install)
	{
		try
		{
			install();
		}
		catch (Exception error)
		{
			Logger.LogError($"[Acceptance] {what} did not install: {error.GetType().Name}: {error.Message}");
		}
	}

	private void Update()
	{
		// HotRepl's Roslyn evaluator type is loaded lazily; keep trying until it is present. The patch only
		// changes the evaluator's compilation options, so a late installation is safe.
		if (_harmony != null)
		{
			HotReplRoslynGameReferencePatch.TryInstall(_harmony);
		}
	}
}

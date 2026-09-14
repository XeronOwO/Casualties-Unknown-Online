using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The menu-exit trigger: <c>PlayerCamera.ToMainMenu()</c> is the game's "leave
/// the world now" action — a public method whose body is one line,
/// <c>SceneManager.LoadScene("PreGen")</c>. It is reached from the tutorial's
/// pause exit (<c>PauseHandler.cs:66</c>), the console's <c>saveandquit</c>
/// (<c>ConsoleScript.cs:792</c>), the layer-end panel's save-and-exit
/// (<c>WorldGeneration.cs:1029</c>; no CODE caller in the decompiled game) and
/// the death screen's MAIN MENU button — the last two are scene-wired
/// UnityEvents in <c>level1</c>, invisible to a source grep.
///
/// Loading the menu scene destroys the live world, so a mid-run cut can never be
/// taken after that call — the cut has to happen while the world is still alive.
/// The prefix therefore records the leave and skips the original; the frame-end
/// seam (<c>Run.SaveCutSeam</c>) takes the cut and performs the leave on the
/// pump, exactly as it already does for a host's session-teardown-driven return.
/// This is what gives SOLO play a menu-exit trigger at all: it has no session
/// teardown event to ride, only the game's own leave action.
///
/// The world half of the decision is checked HERE (the patch owns the game read);
/// the adapter answers only whether the seam will act on the leave, and false
/// means the original load runs — no world to leave, this is the seam's own
/// recorded leave being honoured, or the seam would drop the request (a session
/// took over), in which case suppressing the load would strand the player.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.ToMainMenu))]
internal static class PlayerCameraMenuExitPatches
{
	private static bool Prefix() =>
		!(HarmonyTraverse.HasLiveWorld && (PatchBridge.Impl?.TryDeferMenuReturn(hasLiveWorld: true) ?? false));
}

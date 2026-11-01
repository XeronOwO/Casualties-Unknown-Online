using HarmonyLib;
using TMPro;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Records the game's OWN key-bind capture, so a caller can ask "is a row waiting for a key right now" without guessing.
///
/// <para>
/// <c>SettingsMenu.WaitForKeyPress(SettingKeybind keybind, TextMeshProUGUI txt)</c> writes <c>txt.text = "..."</c> and loops
/// until <c>Input.GetKeyDown</c> fires for some key code, then restores the label and applies the bind
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/SettingsMenu.cs</c>). The only state the menu keeps is that TEXT, and an
/// unbound action shows the same "..." as its value, so scanning labels for "..." answers true with no capture at all
/// (measured 2026-11-02: the settings menu open on its Video tab read as a waiting capture, and every Enter was refused).
/// This prefix records the LABEL the capture itself is writing, and the answer is "that label still reads '...'".
/// </para>
///
/// <para>
/// The coroutine's end is not observable from a prefix, so the answer is derived rather than stored: a capture that
/// finished has restored the label, which makes the recorded label stop reading "...". A capture abandoned mid-way leaves
/// the label at "..." and the answer stays true, which is the safe direction — the key is still the game's in that state.
/// </para>
/// </summary>
[HarmonyPatch(typeof(SettingsMenu), "WaitForKeyPress")]
internal static class SettingsKeybindCapturePatch
{
	/// <summary>
	/// The label the running capture last wrote. Read through <see cref="IsWaiting"/> only: a stale instance from an
	/// earlier menu is harmless because the answer also requires the label to still read "...".
	/// </summary>
	private static TextMeshProUGUI? _waitingLabel;

	/// <summary>True while the label the game's own capture is writing still shows its "waiting for a key" text.</summary>
	internal static bool IsWaiting =>
		_waitingLabel != null // Unity object — ==
		&& _waitingLabel.text == "...";

	private static void Prefix(TextMeshProUGUI txt) => _waitingLabel = txt;
}

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The game's own run-settings PRESET to apply when this client starts a run — the host's configured answer to "which of the game's difficulties
/// should a world made from CUO use".
///
/// <para>
/// It exists because the game already ships what a scripted acceptance run needs: <c>RunSettings.presets</c> holds `normal`, `relaxed`, `paradise`,
/// `desolate` and `unchipped` (<c>reversing/Assembly-CSharp/Assembly-CSharp/RunSettings.cs</c>), and <c>PreRunScript.ApplyPreset(string)</c>
/// (<c>PreRunScript.cs:126-140</c>) is the game's own way to load one: it clears the setting dictionary, seeds it from `normal`, overlays the chosen
/// preset's values, and refreshes the sliders. A scripted batch that starts a world should therefore ask the GAME for its paradise preset instead of
/// spawning supplies into an empty world (the user's ruling, 2026-10-26).
/// </para>
///
/// <para>
/// Static, because the hook that needs it (<see cref="Patches.PreRunScriptStartRunPatch"/>) is a Harmony prefix that runs before any adapter
/// instance is involved in the decision — the same shape the intro cover's decision uses. Empty means "whatever the player picked on the game's own
/// pre-run screen": CUO changes nothing.
/// </para>
/// </summary>
public static class RunPresetDecision
{
	/// <summary>The preset name to apply at the host's own run start, or empty/null to leave the game's menu choice alone.</summary>
	public static string? Preset { get; set; }

	/// <summary>The name the game's own pre-run preset list carries for the generous preset, quoted from `RunSettings.cs` (`presetName = "paradise"`).</summary>
	public const string Paradise = "paradise";
}

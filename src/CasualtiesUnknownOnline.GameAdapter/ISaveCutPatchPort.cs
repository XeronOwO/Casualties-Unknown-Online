namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The save half's patch port: what the native <c>WorldGeneration.SaveAndExit</c> hook may reach. The native entry
/// writes <c>SaveSystem.SaveGame()</c> — the game's own <c>save.sv</c> — and the user's 2026-10-24 ruling is that
/// CUO's saves are the only ones CUO reads, so a client with CUO running must NOT produce one: the entry takes a
/// CUO world cut instead and then leaves to the main menu, which is what the native body's last step does.
///
/// <para>
/// The panel's two entries are otherwise the host's alone (`LayerPanelChoicePolicy`), so in an active session this
/// hook is the HOST's click; in solo play it is the only player's.
/// </para>
/// </summary>
internal interface ISaveCutPatchPort
{
	/// <summary>
	/// The panel's "Save and exit" was pressed. Takes CUO's own world cut (the armed cut the pump takes at its last
	/// step, decision 167) and returns true — the caller must skip the native body, which would write
	/// <c>save.sv</c>, advance the depth without generating and return to the menu. False (CUO not running, no
	/// world) lets the native entry run as the game wrote it.
	/// </summary>
	bool TakeWorldCutInsteadOfTheNativeSave();
}

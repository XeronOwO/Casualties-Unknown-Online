namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The character picker's patch port: the static picker patches cannot receive
/// the session or the logger by construction, so this port carries the session
/// view of the solo unlock store and the one refusal a session win makes.
///
/// <para>
/// The policy is <see cref="Runtime.GameAdapter.CharacterUnlockPolicy"/>'s. The
/// game reads <c>wonchar0</c>/<c>wonchar1</c> in <c>PreRunScript.Awake</c>,
/// <c>Start</c> and <c>HandleCharacterSelect</c>, and writes the key in
/// <c>WorldGeneration.SetGameWon</c>; this port is the only session-aware
/// answer those static patches can reach.
/// </para>
/// </summary>
internal interface ICharacterUnlockPatchPort
{
	/// <summary>True while this client's picker belongs to a multiplayer session.</summary>
	bool InSession { get; }

	/// <summary>Re-apply the session view to the native picker: every character is pickable, the lock tooltip is off.</summary>
	void ApplyPicker(PreRunScript pre);

	/// <summary>True when a session win must not write the solo unlock; the patch then skips the native write.</summary>
	bool SuppressWinWrite();
}

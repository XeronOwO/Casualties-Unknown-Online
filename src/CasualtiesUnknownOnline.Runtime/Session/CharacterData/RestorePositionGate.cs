namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// Guest-side restore-position gate: the position of a reconnect restore must
/// apply exactly ONCE per body — on that body's first frame — so a re-sent
/// restore never teleports the already-restored body a second time. The host
/// sends the saved character from BOTH the reconnect handshake and the
/// InWorld edge, so the same body routinely receives two restores back-to-back
/// (observed live: a 0.5 s double teleport). Pure state: the Game Adapter owns
/// the actual transform write, this owns the "should I apply" verdict — the
/// position reset binds to the BODY leaving the world, never to a restore
/// arriving.
/// </summary>
public sealed class RestorePositionGate
{
	private bool _applied;

	/// <summary>Whether the current body still needs its restore position applied.</summary>
	public bool ShouldApplyPosition => !_applied;

	/// <summary>The position was applied — a re-sent restore must not apply it again.</summary>
	public void MarkPositionApplied() => _applied = true;

	/// <summary>The body left the world (death, menu, disconnect) — the next body's restore position applies again.</summary>
	public void OnBodyLeft() => _applied = false;
}

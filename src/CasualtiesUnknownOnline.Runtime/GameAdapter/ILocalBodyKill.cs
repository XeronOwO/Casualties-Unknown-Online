namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The one scene capability behind the console cheat verb <c>/kill</c>: put THIS client's own body
/// at zero brain health and report the resulting state so the host and every peer see the body die.
///
/// <para>
/// The operation is the game's own: its console verb <c>kill</c> is
/// <c>PlayerCamera.main.body.brainHealth = 0f</c> followed by a log line
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c>), and <c>Body.alive</c> is
/// <c>brainHealth &gt; 0f</c>. Nothing else is set: bleeding, consciousness, the death presentation
/// and every downstream system already read that one field, which is why the native verb does not
/// touch them either.
/// </para>
///
/// <para>
/// It acts on the LOCAL body only, because that is where the body exists (the judgment-ownership
/// rule) and because the native verb is local too. A build with no game scene registers
/// <see cref="Session.PlayerInteraction.UnavailableLocalBodyKill"/> and the verb says it cannot
/// kill a body instead of silently doing nothing.
/// </para>
/// </summary>
public interface ILocalBodyKill
{
	/// <summary>
	/// Kill the local player's body now and report it — zero brain health, then the ordinary
	/// character snapshot so peers converge. A refusal names its ground (no world, no local body).
	/// </summary>
	LocalBodyKillOutcome Kill();
}

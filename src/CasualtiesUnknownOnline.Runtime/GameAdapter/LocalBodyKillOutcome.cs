namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The answer to one <c>/kill</c> application on THIS client's own body: whether the body was put
/// at zero brain health, the value it held before (so the console can say what it did), and the
/// ground when there was no body to kill.
///
/// <para>
/// It is one small record rather than a state machine because the operation is one write: the
/// game's own console verb <c>kill</c> is exactly <c>PlayerCamera.main.body.brainHealth = 0f</c>
/// plus a log line (<c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c>, the
/// <c>new Command("kill", "Instantly kills the player character by setting their brain health to
/// 0.", …)</c> registration), and <c>Body.alive</c> is <c>brainHealth &gt; 0f</c>.
/// </para>
/// </summary>
public readonly record struct LocalBodyKillOutcome(bool Applied, float BrainHealthBefore, string? Refusal)
{
	/// <summary>The body was put at zero brain health; <paramref name="before"/> is what it held.</summary>
	public static LocalBodyKillOutcome Killed(float before) => new(true, before, null);

	/// <summary>Nothing was written — <paramref name="reason"/> says why (no world, no local body).</summary>
	public static LocalBodyKillOutcome Refused(string reason) => new(false, 0f, reason);
}

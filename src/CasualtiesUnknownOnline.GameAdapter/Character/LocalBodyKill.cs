using CasualtiesUnknownOnline.Runtime.GameAdapter;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The console <c>/kill</c> cheat verb applied to THIS client's own body.
///
/// <para>
/// One write, because the game's own verb is one write: its console command
/// <c>kill</c> is <c>PlayerCamera.main.body.brainHealth = 0f</c> and a log line
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/ConsoleScript.cs</c>), and <c>Body.alive</c> is
/// <c>brainHealth &gt; 0f</c> — the bleeding, consciousness and death presentation all read that
/// one field, which is why the native verb touches nothing else either.
/// </para>
///
/// <para>
/// After the write the ordinary character snapshot goes out (through the same owner-state event the
/// limb latch uses), so the host and every peer converge on the dead body without a second channel.
/// </para>
/// </summary>
internal sealed class LocalBodyKill(
	CharacterDataSync characterDataSync,
	ILogger<LocalBodyKill> log) : ILocalBodyKill
{
	private readonly CharacterDataSync _characterDataSync = characterDataSync;
	private readonly ILogger<LocalBodyKill> _log = log;

	/// <inheritdoc />
	public LocalBodyKillOutcome Kill()
	{
		var camera = PlayerCamera.main;
		if (camera == null) // Unity object — ==
		{
			_log.LogWarning("[Kill] refused: no player camera, so there is no local body.");
			return LocalBodyKillOutcome.Refused("this client has no player camera, so there is no local body to kill.");
		}

		var body = camera.body;
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[Kill] refused: the player camera holds no body (still loading the run).");
			return LocalBodyKillOutcome.Refused("this client has no body yet (still loading the run).");
		}

		var before = body.brainHealth;
		body.brainHealth = 0f;
		_characterDataSync.ReportBodyStateEvent(body, "kill-verb");
		_log.LogInformation("[Kill] local body killed: brain health {Before:0.#} -> 0 (alive={Alive}).", before, body.alive);
		return LocalBodyKillOutcome.Killed(before);
	}
}

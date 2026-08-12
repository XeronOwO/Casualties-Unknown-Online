namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The turret-fired replay timeline — the pure decision behind
/// TrapStateActions.ApplyTurretFired (the #131 glitch's fix). A TurretFired
/// event replays the triggering side's engagement chain with its TIMING: at
/// t = 0 the warning (the trigger side's discovery moment, beep), at
/// t = ShotDelaySeconds the shot (rifleshot + particles + tracer — the
/// trigger side's beepTime &gt;= 0.5 firing moment). The post-fire STATE is set
/// immediately at the warning: didShoot starts the game's 15 s reload
/// (TurretScript.cs:30-53 — a peer in range gets beeped but NOT shot during
/// the reload), didBeep pins the discovery branch off (the 0.5 s window is
/// silent like single-player). timeSinceFired at the warning is 3 s, NOT 0:
/// the fire skin and lamp are driven by it (fireSprite while &lt; 2 s,
/// fireLight 80 - timeSinceFired*300, TurretScript.cs:26-28) and must light
/// at the FIRING moment — setting 0 at the warning lit them 0.5 s early (the
/// observed glitch, dc5e00d). 3 s keeps the sprite off (&gt; 2), the lamp off
/// (80 - 900 &lt; 0) and the reload reset at bay (timeSinceFired &gt; 15 is not
/// reached); the delayed fire visuals set 0 at the firing moment, together
/// with the shot visuals.
/// </summary>
internal static class TurretReplayTimeline
{
	/// <summary>timeSinceFired at the WARNING — the fire skin/lamp must stay dark.</summary>
	internal const float WarningTimeSinceFired = 3f;

	/// <summary>timeSinceFired at the FIRING moment — the skin/lamp light together with the shot visuals.</summary>
	internal const float FiringTimeSinceFired = 0f;

	/// <summary>The warning-to-shot delay — the trigger side's beepTime firing moment.</summary>
	internal const float ShotDelaySeconds = 0.5f;

	/// <summary>The game's reload — a peer in range is beeped but not shot during it (TurretScript.cs:30-53).</summary>
	internal const float ReloadSeconds = 15f;

	/// <summary>One warning event's state transition: the written timeSinceFired + the consumed flags.</summary>
	internal static (float TimeSinceFired, bool DidShoot, bool DidBeep, float ShotDelay) OnWarning() =>
		(WarningTimeSinceFired, true, true, ShotDelaySeconds);
}

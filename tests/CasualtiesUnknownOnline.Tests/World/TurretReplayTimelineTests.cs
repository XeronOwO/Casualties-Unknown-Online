using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The turret replay timeline (#131/dc5e00d): the fire skin and lamp are
/// driven by timeSinceFired (fireSprite while &lt; 2 s, fireLight
/// 80 - timeSinceFired*300, TurretScript.cs:26-28) and must light at the
/// FIRING moment, not the warning — the 3 s warning value keeps them dark,
/// the 0 s firing value lights them together with the shot visuals. The
/// game's 15 s reload keeps a peer in range beeped-but-not-shot. The
/// decision table is pure and locked against the game's constants.
/// </summary>
public class TurretReplayTimelineTests
{
	[Fact]
	public void Warning_Sets3s_Not0()
	{
		var (timeSinceFired, didShoot, didBeep, shotDelay) = TurretReplayTimeline.OnWarning();

		// The #131 fix: 0 at the warning lit the skin/lamp 0.5 s early.
		Assert.True(timeSinceFired > 2f, "the fire skin must stay dark at the warning (sprite while < 2 s)");
		Assert.True(80 - timeSinceFired * 300 < 0, "the fire lamp must stay dark at the warning (80 - timeSinceFired*300 < 0)");
		Assert.True(timeSinceFired < TurretReplayTimeline.ReloadSeconds, "the reload reset (timeSinceFired > 15) must not be reached at the warning");
	}

	[Fact]
	public void Warning_ConsumesBothFlags()
	{
		var (_, didShoot, didBeep, _) = TurretReplayTimeline.OnWarning();
		Assert.True(didShoot, "didShoot starts the game's 15 s reload — a peer in range is beeped but NOT shot");
		Assert.True(didBeep, "didBeep pins the discovery branch off — the 0.5 s window is silent like single-player");
	}

	[Fact]
	public void Firing_LandsAtTheShotDelay_WithZero()
	{
		// The 0.5 s coroutine sets 0 at the FIRING moment (the trigger side's
		// beepTime >= 0.5 firing moment, TurretScript.cs:40) — the skin/lamp/
		// shot visuals light together.
		Assert.Equal(0f, TurretReplayTimeline.FiringTimeSinceFired);
		Assert.Equal(0.5f, TurretReplayTimeline.ShotDelaySeconds);
		Assert.Equal(15f, TurretReplayTimeline.ReloadSeconds);
	}

	[Fact]
	public void WarningCarriesTheShotDelay()
	{
		var (_, _, _, shotDelay) = TurretReplayTimeline.OnWarning();
		Assert.Equal(TurretReplayTimeline.ShotDelaySeconds, shotDelay);
	}
}

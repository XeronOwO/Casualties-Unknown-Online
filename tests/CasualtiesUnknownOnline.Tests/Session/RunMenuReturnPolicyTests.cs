using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// L0 locks for the menu-return decision: who may cut, and whether the frame-end
/// seam will actually leave. The second half is the invariant the scene-load
/// interception depends on — suppressing the game's own leave is only safe when
/// the seam will perform it, or the player's action is consumed and they stay in
/// the world.
/// </summary>
public class RunMenuReturnPolicyTests
{
	[Fact]
	public void HostInWorld_ReturnsSaveAndMenu()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.Host, inWorld: true);

		Assert.Equal(RunMenuReturnMode.SaveAndMenu, mode);
	}

	[Fact]
	public void GuestInWorld_ReturnsMenuOnly()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.Guest, inWorld: true);

		Assert.Equal(RunMenuReturnMode.MenuOnly, mode);
	}

	[Fact]
	public void NoRoleInWorld_ReturnsSaveAndMenu_SoloIsItsOwnSaveAuthority()
	{
		var mode = RunMenuReturnPolicy.Decide(SessionRole.None, inWorld: true);

		Assert.Equal(RunMenuReturnMode.SaveAndMenu, mode);
	}

	[Fact]
	public void InMenu_ReturnsNone_ForAnyRole()
	{
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.Host, inWorld: false));
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.Guest, inWorld: false));
		Assert.Equal(RunMenuReturnMode.None, RunMenuReturnPolicy.Decide(SessionRole.None, inWorld: false));
	}

	[Fact]
	public void WouldLeaveWorld_TrueOnlyForALeavableWorld()
	{
		// The interception's precondition: a live world, a camera, and a mode that
		// asks for something. The session is NOT part of it — only a TEARDOWN request
		// is dropped for a session that took over (DecideFlush), and the interception
		// only ever records the player's own leave.
		Assert.True(RunMenuReturnPolicy.WouldLeaveWorld(RunMenuReturnMode.SaveAndMenu, inWorld: true, cameraAvailable: true));
		Assert.True(RunMenuReturnPolicy.WouldLeaveWorld(RunMenuReturnMode.MenuOnly, inWorld: true, cameraAvailable: true));

		Assert.False(RunMenuReturnPolicy.WouldLeaveWorld(RunMenuReturnMode.SaveAndMenu, inWorld: false, cameraAvailable: true));
		Assert.False(RunMenuReturnPolicy.WouldLeaveWorld(RunMenuReturnMode.SaveAndMenu, inWorld: true, cameraAvailable: false));
		Assert.False(RunMenuReturnPolicy.WouldLeaveWorld(RunMenuReturnMode.None, inWorld: true, cameraAvailable: true));
	}

	[Fact]
	public void DecideFlush_NothingPending_IsNone()
	{
		var flush = RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.None, RunMenuReturnOrigin.PlayerLeave, inWorld: true, sessionActive: false, cameraAvailable: true);

		Assert.Equal(RunMenuReturnFlush.None, flush);
	}

	[Fact]
	public void DecideFlush_SoloLeave_LeavesTheWorld()
	{
		var flush = RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.PlayerLeave, inWorld: true, sessionActive: false, cameraAvailable: true);

		Assert.Equal(RunMenuReturnFlush.Leave, flush);
	}

	[Fact]
	public void DecideFlush_TeardownRequestedAgainstANewSession_IsDropped()
	{
		// A teardown request belonged to the session that is ending: a session that
		// took over in the meantime makes it stale.
		var flush = RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.SessionTeardown, inWorld: true, sessionActive: true, cameraAvailable: true);

		Assert.Equal(RunMenuReturnFlush.Clear, flush);
	}

	[Fact]
	public void DecideFlush_PlayerLeave_IsNeverDroppedForALiveSession()
	{
		// A host alone in a lobby IS a live session (SessionActive is set the moment
		// the lobby exists), and the player's own leave must still cut and leave —
		// otherwise that exit silently does nothing.
		Assert.Equal(
			RunMenuReturnFlush.Leave,
			RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.PlayerLeave, inWorld: true, sessionActive: true, cameraAvailable: true));
	}

	[Fact]
	public void DecideFlush_HostPull_LeavesEvenWhileTheSessionIsStillUp()
	{
		// The guest is pulled when the host reported InMenu, and the host's lobby is
		// still alive then: the session is active and the guest must STILL be pulled
		// out of a world whose host is gone.
		Assert.Equal(
			RunMenuReturnFlush.Leave,
			RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.MenuOnly, RunMenuReturnOrigin.HostPull, inWorld: true, sessionActive: true, cameraAvailable: true));
	}

	[Fact]
	public void DecideFlush_WorldWentWithoutTheRequest_IsDropped()
	{
		Assert.Equal(
			RunMenuReturnFlush.Clear,
			RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.PlayerLeave, inWorld: false, sessionActive: false, cameraAvailable: true));
		Assert.Equal(
			RunMenuReturnFlush.Clear,
			RunMenuReturnPolicy.DecideFlush(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.PlayerLeave, inWorld: true, sessionActive: false, cameraAvailable: false));
	}

	[Fact]
	public void DecideFlush_AnInterceptionPrecondition_AlwaysLeaves()
	{
		// THE invariant that makes the suppression safe: whatever the session does in
		// the frame between the record and the seam, a request the interception
		// recorded (the player's own leave) is never dropped while the world is
		// leavable — so the suppressed scene load is always replayed.
		foreach (var mode in new[] { RunMenuReturnMode.SaveAndMenu, RunMenuReturnMode.MenuOnly })
		{
			foreach (var sessionActive in new[] { false, true })
			{
				Assert.True(RunMenuReturnPolicy.WouldLeaveWorld(mode, inWorld: true, cameraAvailable: true));
				Assert.Equal(
					RunMenuReturnFlush.Leave,
					RunMenuReturnPolicy.DecideFlush(mode, RunMenuReturnOrigin.PlayerLeave, inWorld: true, sessionActive, cameraAvailable: true));
			}
		}
	}

	[Fact]
	public void ShouldSuppressSceneLoad_OnlyForARecordedDeferral()
	{
		// The Harmony prefix's verdict: true skips the original load.
		Assert.True(MenuExitInterception.ShouldSuppressSceneLoad(hasLiveWorld: true, replayingLeave: false, wouldSeamLeave: true));

		// No world → the game's own load runs (the menu is already up, or no run).
		Assert.False(MenuExitInterception.ShouldSuppressSceneLoad(hasLiveWorld: false, replayingLeave: false, wouldSeamLeave: true));

		// The seam performing the leave it recorded: the load MUST run.
		Assert.False(MenuExitInterception.ShouldSuppressSceneLoad(hasLiveWorld: true, replayingLeave: true, wouldSeamLeave: true));

		// The seam would drop the request: never suppress — that would strand the player.
		Assert.False(MenuExitInterception.ShouldSuppressSceneLoad(hasLiveWorld: true, replayingLeave: false, wouldSeamLeave: false));
	}
}

using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// L0 locks for the deferred menu-return request: a caller only sets one-shot
/// intent (mode + origin); the seam decides whether to act on it.
/// </summary>
public class RunMenuReturnRequestTests
{
	[Fact]
	public void Request_ThenPending_YieldsTheModeAndOrigin()
	{
		var request = new RunMenuReturnRequest();

		request.Request(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.PlayerLeave);

		Assert.True(request.IsPending);
		Assert.Equal(RunMenuReturnMode.SaveAndMenu, request.Pending);
		Assert.Equal(RunMenuReturnOrigin.PlayerLeave, request.Origin);
	}

	[Fact]
	public void Clear_EndsTheRequest()
	{
		var request = new RunMenuReturnRequest();
		request.Request(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.SessionTeardown);

		request.Clear();

		Assert.False(request.IsPending);
		Assert.Equal(RunMenuReturnMode.None, request.Pending);
	}

	[Fact]
	public void Request_None_DoesNotArm()
	{
		var request = new RunMenuReturnRequest();

		request.Request(RunMenuReturnMode.None, RunMenuReturnOrigin.PlayerLeave);

		Assert.False(request.IsPending);
		Assert.Equal(RunMenuReturnMode.None, request.Pending);
	}

	[Fact]
	public void Request_OverwritesThePendingModeAndOrigin()
	{
		// The record is a single slot: the newest ask wins (a menu return supersedes
		// an older teardown request for the same leave).
		var request = new RunMenuReturnRequest();
		request.Request(RunMenuReturnMode.SaveAndMenu, RunMenuReturnOrigin.SessionTeardown);

		request.Request(RunMenuReturnMode.MenuOnly, RunMenuReturnOrigin.PlayerLeave);

		Assert.Equal(RunMenuReturnMode.MenuOnly, request.Pending);
		Assert.Equal(RunMenuReturnOrigin.PlayerLeave, request.Origin);
	}
}

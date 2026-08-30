using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// L0 locks for the deferred menu-return request: session teardown only sets
/// one-shot intent; the Update pump consumes it exactly once.
/// </summary>
public class RunMenuReturnRequestTests
{
	[Fact]
	public void Request_ThenConsume_YieldsOneShotMode()
	{
		var request = new RunMenuReturnRequest();

		request.Request(RunMenuReturnMode.SaveAndMenu);
		Assert.True(request.IsPending);

		Assert.True(request.TryConsume(out var mode));
		Assert.Equal(RunMenuReturnMode.SaveAndMenu, mode);
		Assert.False(request.IsPending);
		Assert.False(request.TryConsume(out _));
	}

	[Fact]
	public void Request_MenuOnly_IsReturned()
	{
		var request = new RunMenuReturnRequest();

		request.Request(RunMenuReturnMode.MenuOnly);

		Assert.True(request.TryConsume(out var mode));
		Assert.Equal(RunMenuReturnMode.MenuOnly, mode);
	}

	[Fact]
	public void Request_None_DoesNotArm()
	{
		var request = new RunMenuReturnRequest();

		request.Request(RunMenuReturnMode.None);

		Assert.False(request.IsPending);
		Assert.False(request.TryConsume(out var mode));
		Assert.Equal(RunMenuReturnMode.None, mode);
	}
}

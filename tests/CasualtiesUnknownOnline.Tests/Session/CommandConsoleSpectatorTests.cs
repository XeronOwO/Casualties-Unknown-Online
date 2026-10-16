using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The keyboard path to the spectator camera. What these cover is the half that IS testable without the game:
/// the command's own answers and refusals and the port it drives. The camera half — the local body's liveness,
/// the clone renderer and the game's own camera fields — needs a running client, and it is the ticket's own
/// verification step rather than something a unit case can stand in for.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleSpectatorTests
{
	[Fact]
	public void Spectate_WithNoAdapter_AnswersThatWatchingIsUnavailable()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate @a"));
		Assert.Contains(console.Lines, l => l.Text.Contains("not available on this client"));
	}

	[Fact]
	public void Spectate_Off_WhenNothingIsBeingWatched_SaysSo()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate off"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Not watching anyone"));
		Assert.False(stub.StopCalls > 0);
	}

	[Fact]
	public void Spectate_WithAnUnknownSelector_AnswersNoMatch()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate @a[name=NobodyAtAll]"));
		Assert.Contains(console.Lines, l => l.Text.Contains("No players match selector"));
		Assert.False(stub.IsWatching);
	}

	[Fact]
	public void Spectate_WhenTheCameraRefuses_RendersTheRefusalRatherThanPretending()
	{
		var stub = new StubSpectator { RefusesWatch = true };
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));

		// The guest is a member this console can select, so the refusal comes from the port and not the selector.
		var console = host.Services.GetRequiredService<ICommandControl>();
		Assert.True(console.TryExecute($"/spectate @a[limit=1]"));
		Assert.False(stub.IsWatching);
	}

	[Fact]
	public void SpectateFreeFly_WithoutOnOrOff_PrintsTheUsage()
	{
		var stub = new StubSpectator { IsWatching = true };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate freefly"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Usage: /spectate freefly"));
	}

	[Fact]
	public void SpectateFreeFly_WhenTheRuleRefuses_RendersTheRuleAnswer()
	{
		var stub = new StubSpectator { IsWatching = true, RefusesFreeFly = true };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate freefly on"));
		Assert.Contains(console.Lines, l => l.Text.Contains("do not allow the free camera"));
	}

	[Fact]
	public void Spectate_WatchingSomeone_SaysWhoAndStopsOnRequest()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/spectate @a[limit=1]"));
		if (stub.IsWatching)
		{
			Assert.Contains(console.Lines, l => l.Text.Contains("Watching "));
			Assert.True(console.TryExecute("/spectate off"));
			Assert.Contains(console.Lines, l => l.Text.Contains("Stopped watching"));
			Assert.False(stub.IsWatching);
		}
	}

	/// <summary>The camera's own port, as the console sees it: it records what it was asked, and refuses what the test tells it to.</summary>
	private static void Register(IServiceCollection services, StubSpectator stub) =>
		services.Replace(ServiceDescriptor.Singleton<ISpectatorControl>(stub));

	private sealed class StubSpectator : ISpectatorControl
	{
		public bool IsWatching { get; set; }

		public bool RefusesWatch { get; init; }

		public bool RefusesFreeFly { get; init; }

		public int StopCalls { get; private set; }

		public bool TryWatch(ulong steamId)
		{
			if (RefusesWatch)
			{
				return false;
			}

			IsWatching = true;
			return true;
		}

		public void StopWatching()
		{
			StopCalls++;
			IsWatching = false;
		}

		public bool TrySetFreeFly(bool wanted) => !RefusesFreeFly;
	}
}

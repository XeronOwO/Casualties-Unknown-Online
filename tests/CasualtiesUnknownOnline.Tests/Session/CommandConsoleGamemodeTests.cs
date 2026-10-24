using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.CommandConsoleTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The mode switch's half that needs no game process: the command's own answers and the port it drives. The mode
/// itself — the camera, the suppression and the possessed HUD — needs a running client and is the ticket's
/// acceptance, not something a unit case can stand in for.
/// </summary>
[Trait("Category", "Integration")]
public class CommandConsoleGamemodeTests
{
	[Fact]
	public void Gamemode_WithNoAdapter_AnswersThatTheModeIsUnavailable()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode spectator"));
		Assert.Contains(console.Lines, l => l.Text.Contains("not available on this client"));
	}

	[Fact]
	public void Gamemode_Spectator_EntersTheModeAndSaysHowToWatch()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode spectator"));
		Assert.True(stub.IsSpectator);
		Assert.Contains(console.Lines, l => l.Text.Contains("Spectator mode on"));
	}

	[Fact]
	public void Gamemode_Survivor_LeavesTheMode()
	{
		var stub = new StubSpectator { IsSpectator = true };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode survivor"));
		Assert.False(stub.IsSpectator);
		Assert.Equal(1, stub.ExitCalls);
		Assert.Contains(console.Lines, l => l.Text.Contains("Normal play."));
	}

	[Fact]
	public void Gamemode_WhenTheModeRefuses_SaysWhyInsteadOfPretending()
	{
		var stub = new StubSpectator { RefusesEnter = true };
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode spectator"));
		Assert.False(stub.IsSpectator);
		Assert.Contains(console.Lines, l => l.Text.Contains("unavailable here"));
	}

	[Fact]
	public void Gamemode_WithNoArgument_PrintsTheUsageAndTheCurrentMode()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Usage: /gamemode survivor|spectator (currently survivor)"));
	}

	[Fact]
	public void Gamemode_WithAnUnknownMode_NamesTheUsage()
	{
		var stub = new StubSpectator();
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services => Register(services, stub));
		var console = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(console.TryExecute("/gamemode creative"));
		Assert.Contains(console.Lines, l => l.Text.Contains("Unknown mode 'creative'"));
	}

	private static void Register(IServiceCollection services, StubSpectator stub) =>
		services.Replace(ServiceDescriptor.Singleton<ISpectatorControl>(stub));

	private sealed class StubSpectator : ISpectatorControl
	{
		public bool IsWatching { get; set; }

		public ulong WatchingSteamId { get; set; }

		public bool IsSpectator { get; set; }

		public bool IsFreeFly { get; set; }

		public bool RefusesEnter { get; init; }

		public int ExitCalls { get; private set; }

		public bool TryWatch(ulong steamId)
		{
			IsWatching = true;
			return true;
		}

		public void StopWatching() => IsWatching = false;

		public bool TrySetFreeFly(bool wanted) => true;

		public bool EnterSpectatorMode()
		{
			if (RefusesEnter)
			{
				return false;
			}

			IsSpectator = true;
			return true;
		}

		public void ExitSpectatorMode()
		{
			ExitCalls++;
			IsSpectator = false;
			IsWatching = false;
		}
	}
}

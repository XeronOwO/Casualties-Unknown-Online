using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The cheat family for MOD commands: a definition that declares <c>CommandPermission.Cheat</c> is refused by the same two host rulings every built-in cheat verb
/// follows — the switch must be open and the roster must name the player — on the side that typed the line AND on the host that would run it.
/// </summary>
[Trait("Category", "Integration")]
public class ConsoleCommandCheatPermissionTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	[Fact]
	public void AMemberWithoutTheGrant_IsRefusedLocally_AndTheHostNeverRunsIt()
	{
		var (host, guest) = Pair(switchOpen: true);
		var hostMod = CommandMod(host);
		_ = host.Services.GetRequiredService<ICommandControl>();

		// A refusal answers false and the line names the ground; the host's handler never ran.
		Assert.False(guest.Services.GetRequiredService<ICommandControl>().TryExecute("/ccheat payload"));

		Assert.Contains(
			guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Key == "cheat.refused.not_on_roster");
		Assert.DoesNotContain(hostMod.ConsoleExecutions, e => e.Name == "ccheat");
	}

	[Fact]
	public void AMemberWithAClosedSwitch_IsRefusedLocally_WithoutARequest()
	{
		var (host, guest) = Pair(switchOpen: false);
		var hostMod = CommandMod(host);
		_ = host.Services.GetRequiredService<ICommandControl>();
		Assert.True(host.Services.GetRequiredService<ICommandControl>().TryExecute($"/op {GuestId}"));

		Assert.False(guest.Services.GetRequiredService<ICommandControl>().TryExecute("/ccheat payload"));

		Assert.Contains(
			guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Key == "cheat.refused.switch_closed");
		Assert.DoesNotContain(hostMod.ConsoleExecutions, e => e.Name == "ccheat");
	}

	[Fact]
	public void AfterBothRulings_TheRelayedCheatCommandRunsOnTheHost()
	{
		var (host, guest) = Pair(switchOpen: true);
		var hostMod = CommandMod(host);
		_ = host.Services.GetRequiredService<ICommandControl>();
		Assert.True(host.Services.GetRequiredService<ICommandControl>().TryExecute($"/op {GuestId}"));

		var accepted = guest.Services.GetRequiredService<ICommandControl>().TryExecute("/ccheat payload");

		Assert.True(accepted, string.Join(" | ", guest.Services.GetRequiredService<ICommandControl>().Lines.Select(l => l.Key ?? l.Text)));
		Assert.Equal(GuestId, Assert.Single(hostMod.ConsoleExecutions, e => e.Name == "ccheat").LocalSteamId);
	}

	[Fact]
	public void ACheatCommandTheCallerMayNotRun_IsNotOffered()
	{
		// The list and the dispatcher ask ONE predicate: a command the console would refuse is not offered by name either.
		var (_, guest) = Pair(switchOpen: true);
		var completion = guest.Services.GetRequiredService<ICommandCompletionSource>();

		Assert.DoesNotContain(completion.Suggest("/cc"), s => s.Text == "ccheat");
		Assert.Contains(completion.Suggest("/cr"), s => s.Text == "crelay");
	}

	/// <summary>A pair whose host rules carry the cheat switch; the announced set a member reads is built from it, like the game's own configuration.</summary>
	private static (TestNode Host, TestNode Guest) Pair(bool switchOpen) =>
		TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: services =>
			services.AddSingleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowMemberCheatVerbs = switchOpen })));

	private static TestCommandMod CommandMod(TestNode node) =>
		(TestCommandMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestCommandMod);
}

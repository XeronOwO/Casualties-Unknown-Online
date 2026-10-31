using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// Stage 3 of the console contract: a definition that declared <c>CommandScope.HostRelayed</c> runs on the HOST when a member types it, and the member prints the
/// host's answer in the member's own language. The tests use a real host/guest pair (the same transport the session runs), so "the guest never runs it" and "the
/// answer arrives keyed" are observed facts rather than claims.
/// </summary>
/// <remarks>
/// The host's console is resolved first on purpose: a relayed line is answered by the console's own command registry, and the session constructs that console
/// with its UI at startup. A host that never built one refuses the line with the "host does not run this" answer — pinned by the last test, because that is what a
/// half-started host really does and a silent drop is the failure worth catching.
/// </remarks>
[Trait("Category", "Integration")]
public class ConsoleCommandRelayTests
{
	private const ulong HostId = 76561198000000001;
	private const ulong GuestId = 76561198000000002;
	private const ulong LobbyId = 10977524123456789;

	[Fact]
	public void GuestRelayedCommand_RunsOnlyOnTheHost_AndAnswersTheGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostMod = CommandMod(host);
		var guestMod = CommandMod(guest);
		_ = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(guest.Services.GetRequiredService<ICommandControl>().TryExecute("/crelay x"));

		// The host ran it, with the GUEST as the requester, and the guest never ran it locally.
		var execution = Assert.Single(hostMod.ConsoleExecutions, e => e.Name == "crelay");
		Assert.Equal(GuestId, execution.LocalSteamId);
		Assert.DoesNotContain(guestMod.ConsoleExecutions, e => e.Name == "crelay");

		// The answer reached the guest as a keyed line, so the guest's client renders the sentence in its own language.
		var line = guest.Services.GetRequiredService<ICommandControl>()
			.Lines
			.Last(l => l.Key == "test.commands.relay_answer");
		Assert.Equal(ConsoleLineKind.Success, line.Kind);
	}

	[Fact]
	public void HostRelayedCommand_OnTheHost_RunsInPlace()
	{
		// The host's own typed line is not relayed to itself: the same definition runs locally, with the host as the requester.
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostMod = CommandMod(host);
		_ = host.Services.GetRequiredService<ICommandControl>();

		Assert.True(host.Services.GetRequiredService<ICommandControl>().TryExecute("/crelay y"));

		Assert.Equal(HostId, Assert.Single(hostMod.ConsoleExecutions, e => e.Name == "crelay").LocalSteamId);
	}

	[Fact]
	public void AHostWithoutItsConsole_RefusesTheRelayedLineInsteadOfDroppingIt()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		_ = CommandMod(host);

		Assert.True(guest.Services.GetRequiredService<ICommandControl>().TryExecute("/crelay x"));

		Assert.Contains(
			guest.Services.GetRequiredService<ICommandControl>().Lines,
			line => line.Key == "console.command.relay_not_hosted");
	}

	private static TestCommandMod CommandMod(TestNode node) =>
		(TestCommandMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestCommandMod);
}

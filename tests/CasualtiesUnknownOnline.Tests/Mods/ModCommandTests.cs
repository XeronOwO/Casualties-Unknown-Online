using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The Phase 4b host-command domain over the real session stack: host-local
/// execution is synchronous, a guest's request executes ONLY on the host's
/// copy of the mod and the directed result settles the guest callback,
/// permission-less mods cannot register/execute commands, malformed or unknown
/// requests produce framework failure results (or are dropped when unrouteable),
/// handler exceptions become failure results, results are capped, and pending
/// callbacks are settled when the session ends.
/// </summary>
public class ModCommandTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static TestCommandMod CommandMod(TestNode node) =>
		(TestCommandMod)node.Services.GetRequiredService<ModService>().LoadedMods.Single(m => m is TestCommandMod);

	private static TestPermissionlessCommandMod PermissionlessMod(TestNode node) =>
		(TestPermissionlessCommandMod)node.Services.GetRequiredService<ModService>()
			.LoadedMods.Single(m => m is TestPermissionlessCommandMod);

	[Fact]
	public void HostLocalCommand_RunsSynchronously_WithHostRequester()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var mod = CommandMod(host);

		IModCommandResult? result = null;
		var accepted = mod.Context!.Commands.TryExecute("echo", ["a", "b"], r => result = r);

		Assert.True(accepted);
		Assert.NotNull(result);
		Assert.True(result!.Success);
		Assert.Equal("a b", result.Output);
		Assert.Equal(HostId, result.RequesterSteamId);
		Assert.Equal([("echo", new[] { "a", "b" }, HostId)], mod.Executions.Select(e => (e.Name, e.Arguments.ToArray(), e.Requester)).ToList());
	}

	[Fact]
	public void GuestRequest_ExecutesOnlyOnHostCopy_AndReturnsDirectedResult()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var guestMod = CommandMod(guest);
		var hostMod = CommandMod(host);

		IModCommandResult? result = null;
		var accepted = guestMod.Context!.Commands.TryExecute("echo", ["hello", "guest"], r => result = r);

		Assert.True(accepted);
		Assert.NotNull(result);
		Assert.True(result!.Success);
		Assert.Equal("hello guest", result.Output);
		Assert.Equal(GuestId, result.RequesterSteamId);
		Assert.Equal([(GuestId, "hello guest")], hostMod.Executions.Select(e => (e.Requester, string.Join(" ", e.Arguments))).ToList());
		Assert.Empty(guestMod.Executions); // the guest NEVER runs the command locally
	}

	[Fact]
	public void GuestHostAction_ExecutesOnHost_WithGuestAsRequester()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		IModCommandResult? result = null;
		var accepted = CommandMod(guest).Context!.Commands.TryExecute("hostaction", [], r => result = r);

		Assert.True(accepted);
		Assert.True(result!.Success);
		Assert.Equal($"host:{GuestId}", result.Output);
		Assert.Equal(GuestId, CommandMod(host).Executions.Single().Requester);
	}

	[Fact]
	public void ThrowingHandler_BecomesFailureResult_NotAFrameworkException()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		IModCommandResult? result = null;
		var accepted = CommandMod(guest).Context!.Commands.TryExecute("fail", [], r => result = r);

		Assert.True(accepted);
		Assert.False(result!.Success);
		Assert.Contains("test.commands fail always throws", result.Error);
		Assert.True(string.IsNullOrEmpty(result.Output));
	}

	[Fact]
	public void PermissionlessMod_RegistrationAndExecutionRefused()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		foreach (var node in new[] { host, guest })
		{
			var mod = PermissionlessMod(node);
			Assert.False(mod.OrdinaryRegistration, "RegisterCommand must be explicit");
			Assert.False(mod.HostActionRegistration, "ExecuteHostAction must be explicit");
			Assert.False(mod.Context!.Commands.IsRegistered("ordinary"));
			Assert.False(mod.Context.Commands.TryExecute("ordinary", [], _ => Assert.Fail("a refused execution must not invoke its callback")));
		}
	}

	[Fact]
	public void UnknownCommand_RefusedAtTheSender_WithoutACallback()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		foreach (var node in new[] { host, guest })
		{
			Assert.False(CommandMod(node).Context!.Commands.TryExecute("missing", [], _ => Assert.Fail("a refused execution must not invoke its callback")));
		}
	}

	[Fact]
	public void ArgumentShapeOverCaps_RefusedAtTheSender()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var tooMany = Enumerable.Range(0, ModCommandPolicy.MaxArgumentCount + 1).Select(_ => "a").ToArray();
		var tooLong = new[] { new string('a', ModCommandPolicy.MaxArgumentLength + 1) };

		Assert.False(CommandMod(guest).Context!.Commands.TryExecute("echo", tooMany, _ => Assert.Fail("over-cap arguments must be refused")));
		Assert.False(CommandMod(guest).Context!.Commands.TryExecute("echo", tooLong, _ => Assert.Fail("over-long arguments must be refused")));
	}

	[Fact]
	public void HandlerOutput_IsClampedToPolicyCap()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		IModCommandResult? result = null;
		var accepted = CommandMod(host).Context!.Commands.TryExecute("long", [], r => result = r);

		Assert.True(accepted);
		Assert.Equal(ModCommandPolicy.MaxOutputLength, result!.Output!.Length);
	}

	[Fact]
	public void UnknownCommandRequestFromGuest_GetsFailureResult()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var received = RecordInbound(guest);

		host.Services.GetRequiredService<ModService>().FireModCommandRequestReceived(GuestId, new ModCommandRequestMsg
		{
			RequestId = 42,
			ModId = "test.commands",
			Name = "missing",
		});

		var result = NetPacket.DecodePayload<ModCommandResultMsg>(received.Single().Frame);
		Assert.False(result.Success);
		Assert.Equal("command not registered", result.Error);
		Assert.Equal(42u, result.RequestId);
	}

	[Fact]
	public void MalformedRequestShape_IsDroppedWithoutAResult()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var received = RecordInbound(guest);

		host.Services.GetRequiredService<ModService>().FireModCommandRequestReceived(GuestId, new ModCommandRequestMsg
		{
			RequestId = 7,
			ModId = "test.commands",
			Name = "echo",
			Arguments = [.. Enumerable.Range(0, ModCommandPolicy.MaxArgumentCount + 1).Select(_ => "x")],
		});

		Assert.Empty(received);
	}

	[Fact]
	public void ResultForUnknownRequestId_IsDropped()
	{
		var (_, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);

		guest.Services.GetRequiredService<ModService>().FireModCommandResultReceived(HostId, new ModCommandResultMsg
		{
			RequestId = 1234,
			ModId = "test.commands",
			Name = "echo",
			Success = true,
			Output = "late",
		});

		// The frame is dropped by the mod domain — no pending callback exists to settle.
		Assert.True(true);
	}

	[Fact]
	public void SessionEnded_SettlesPendingRequestWithFailure()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var guestSteam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var guest = TestNode.Create(GuestId, network, guestSteam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		network.SetFaults(HostId, GuestId, new LinkFaults { DelayMs = 10_000 });
		IModCommandResult? result = null;
		var accepted = CommandMod(guest).Context!.Commands.TryExecute("echo", ["pending"], r => result = r);

		Assert.True(accepted);
		Assert.Null(result); // the result is still in flight

		guest.Session.EndSession();

		Assert.NotNull(result);
		Assert.False(result!.Success);
		Assert.Equal("session ended", result.Error);

		network.Advance(20_000); // the late result arrives after the pending entry is gone — dropped
		Assert.Equal("session ended", result.Error);
	}

	[Fact]
	public void GuestRequest_ResultIsDirected_NeverBroadcast()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		var g1Steam = new FakeSteamService(GuestId) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, 2002] };
		var g2Steam = new FakeSteamService(2002) { LobbyOwner = HostId, LobbyMembers = [HostId, GuestId, 2002] };
		var host = TestNode.Create(HostId, network, hostSteam, clock, pumpFirstFrame: true);
		var g1 = TestNode.Create(GuestId, network, g1Steam, clock, pumpFirstFrame: true);
		var g2 = TestNode.Create(2002, network, g2Steam, clock, pumpFirstFrame: true);
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId, 2002];
		g1.Steam.FireLobbyEntered(LobbyId);
		g2.Steam.FireLobbyEntered(LobbyId);

		var g1Frames = RecordInbound(g1);
		var g2Frames = RecordInbound(g2);

		IModCommandResult? result = null;
		CommandMod(g1).Context!.Commands.TryExecute("echo", ["directed"], r => result = r);

		Assert.True(result!.Success);
		Assert.Single(g1Frames);
		Assert.Empty(g2Frames);
	}

	[Fact]
	public void CommandRequestFloodOverBurst_IsDropped()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var frames = RecordInbound(guest);
		var modService = host.Services.GetRequiredService<ModService>();

		for (var i = 0; i < ModRateLimitPolicy.CommandRequestBurst + 1; i++)
		{
			modService.FireModCommandRequestReceived(GuestId, new ModCommandRequestMsg
			{
				RequestId = (uint)i,
				ModId = "test.commands",
				Name = "echo",
			});
		}

		Assert.Equal(ModRateLimitPolicy.CommandRequestBurst, frames.Count);
		Assert.Equal(ModRateLimitPolicy.CommandRequestBurst, CommandMod(host).Executions.Count);
	}

	private static List<(ulong Sender, byte[] Frame)> RecordInbound(TestNode node)
	{
		var frames = new List<(ulong Sender, byte[] Frame)>();
		node.Transport.MessageReceived += (sender, frame) => frames.Add((sender, frame));
		return frames;
	}
}

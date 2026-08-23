using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Host ban is the second admin slice: the host sends a dedicated Banned
/// message to the target, removes it from the presence table, persists the
/// SteamID, and rejects its later handshakes. Ban is host-only, never applies
/// to the host itself, and an unbanned player may rejoin normally.
/// </summary>
public class HostBanTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostBan_SendsDedicatedMessageAndRemovesMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var bans = new List<BannedMsg>();
			guest.Transport.MessageReceived += (_, frame) =>
			{
				if ((NetMsg)frame[0] == NetMsg.Banned)
				{
					bans.Add(NetPacket.DecodePayload<BannedMsg>(frame));
				}
			};

			var hostBan = host.Services.GetRequiredService<IHostBanService>();
			Assert.True(hostBan.Ban(guest.SteamId, "test ban"));
			var ban = Assert.Single(bans);
			Assert.Equal("test ban", ban.Reason);
			Assert.DoesNotContain(host.Session.Members, m => m.SteamId == guest.SteamId);
			Assert.False(guest.Session.SessionActive);
			Assert.True(hostBan.IsBanned(guest.SteamId));
		}
	}

	[Fact]
	public void HostBan_OnlyHostCanBanNonLocalKnownMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var hostBan = host.Services.GetRequiredService<IHostBanService>();
			var guestBan = guest.Services.GetRequiredService<IHostBanService>();

			// A guest cannot ban. A host cannot ban itself. An unknown member
			// cannot be banned. A second ban attempt on the same SteamID is a no-op.
			Assert.False(guestBan.Ban(host.SteamId, "not allowed"));
			Assert.False(hostBan.Ban(host.SteamId, "not allowed"));
			Assert.False(hostBan.Ban(9999, "not allowed"));

			Assert.True(hostBan.Ban(guest.SteamId, "now"));
			Assert.False(hostBan.Ban(guest.SteamId, "again"));
			Assert.True(host.Session.SessionActive); // the host keeps its own session
			Assert.False(guest.Session.SessionActive);
		}
	}

	[Fact]
	public void HostBan_RejectsRejoinAndUnbanAllowsRejoin()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var hostBan = host.Services.GetRequiredService<IHostBanService>();
			Assert.True(hostBan.Ban(guest.SteamId, "bye"));

			// The banned player leaves the lobby and tries to re-enter. The
			// host must not create a presence row for a banned SteamID.
			guest.Steam.FireLobbyEntered(LobbyId);
			Assert.DoesNotContain(host.Session.Members, m => m.SteamId == guest.SteamId);

			// After an unban the same rejoin flow is accepted again.
			Assert.True(hostBan.Unban(guest.SteamId));
			guest.Steam.FireLobbyEntered(LobbyId);
			Assert.Contains(host.Session.Members, m => m.SteamId == guest.SteamId);
		}
	}

	[Fact]
	public void HostBan_PersistsAcrossHostNodeRestart()
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", "host-ban", Guid.NewGuid().ToString("N"), "host-bans.bin");
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, hostBanFile: path);
		using (host)
		using (guest)
		{
			Assert.True(host.Services.GetRequiredService<IHostBanService>().Ban(guest.SteamId, "persist"));
		}

		// A fresh host process using the same file must load the ban.
		var network = new FakeNetwork(clock: new FakeClock());
		var steam = new FakeSteamService(HostId) { LobbyOwner = HostId, LobbyMembers = [HostId] };
		using var restartedHost = TestNode.Create(HostId, network, steam, pumpFirstFrame: true, hostBanFile: path);
		var loaded = restartedHost.Services.GetRequiredService<IHostBanService>();
		Assert.True(loaded.IsBanned(GuestId));
		Assert.Contains(GuestId, loaded.BannedSteamIds);
	}
}

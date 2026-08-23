using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Host kick is the first admin slice: the host sends a dedicated Kicked
/// message to the target, removes it from the presence table, and the guest
/// tears its session down immediately. The remaining members are untouched (the
/// entity domain handles the PlayerLeave fan-out on the host side).
/// </summary>
public class KickedTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostKick_SendsDedicatedMessageAndRemovesMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			var kicks = new List<KickedMsg>();
			guest.Transport.MessageReceived += (_, frame) =>
			{
				if ((NetMsg)frame[0] == NetMsg.Kicked)
				{
					kicks.Add(NetPacket.DecodePayload<KickedMsg>(frame));
				}
			};

			Assert.True(host.Session.KickMember(guest.SteamId, "test kick"));
			var kick = Assert.Single(kicks);
			Assert.Equal("test kick", kick.Reason);
			Assert.DoesNotContain(host.Session.Members, m => m.SteamId == guest.SteamId);
			Assert.False(guest.Session.SessionActive);
		}
	}

	[Fact]
	public void HostKick_OnlyHostCanKickNonLocalKnownMember()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			// A guest cannot kick. A host cannot kick itself. An unknown member cannot be kicked.
			Assert.False(guest.Session.KickMember(host.SteamId, "not allowed"));
			Assert.False(host.Session.KickMember(host.SteamId, "not allowed"));
			Assert.False(host.Session.KickMember(9999, "not allowed"));

			// The real host→guest kick still works after the rejected attempts.
			Assert.True(host.Session.KickMember(guest.SteamId, "now"));
			Assert.True(host.Session.SessionActive); // the host keeps its own session
			Assert.False(guest.Session.SessionActive);
		}
	}
}

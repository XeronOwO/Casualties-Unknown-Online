using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Locks the single protocol message registry: every <see cref="NetMsg"/> value
/// must be registered (so a new message cannot silently fall through), every
/// registered entry must carry an explicit direction and payload type, and
/// unregistered ids must fail closed at the receiver instead of defaulting to
/// valid (the old PacketReceiver.IsValidDirection behavior).
/// </summary>
public class NetMessageRegistryTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void EveryNetMsg_IsRegistered()
	{
		var all = Enum.GetValues(typeof(NetMsg)).Cast<NetMsg>().ToHashSet();
		var registered = NetMessageRegistry.All.Keys.ToHashSet();

		var missing = all.Except(registered).ToList();
		Assert.True(missing.Count == 0,
			$"every NetMsg must be registered in NetMessageRegistry; missing: [{string.Join(", ", missing)}]");

		var extra = registered.Except(all).ToList();
		Assert.True(extra.Count == 0,
			$"NetMessageRegistry must not contain undefined NetMsg values; extra: [{string.Join(", ", extra)}]");
	}

	[Fact]
	public void EveryRegisteredMessage_HasExplicitDirectionAndPayloadType()
	{
		foreach (var entry in NetMessageRegistry.All)
		{
			Assert.True(Enum.IsDefined(typeof(NetMessageDirection), entry.Value.Direction),
				$"{entry.Key} has an undefined NetMessageDirection.");
			Assert.NotNull(entry.Value.PayloadType);
		}
	}

	[Fact]
	public void UnregisteredMessageId_IsRejectedOnReceive()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostReceiver = host.Services.GetRequiredService<PacketReceiver>();
		var guestReceiver = guest.Services.GetRequiredService<PacketReceiver>();

		Assert.False(NetMessageRegistry.TryGet((NetMsg)200, out _));
		Assert.False(hostReceiver.IsValidDirection((NetMsg)200));
		Assert.False(guestReceiver.IsValidDirection((NetMsg)200));
	}

	[Fact]
	public void UnregisteredMessageId_IsRejectedOnSend()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var sender = host.Services.GetRequiredService<PacketSender>();

		Assert.Throws<InvalidOperationException>(() => sender.TrySend(GuestId, (NetMsg)200));
		Assert.Throws<InvalidOperationException>(() => sender.SendToAll([GuestId], (NetMsg)200, null));
	}
}

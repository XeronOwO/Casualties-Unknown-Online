using System.Collections.Generic;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The fake transport's own behaviour contract: reliable ordering, unreliable
/// loss, virtual-clock delay, duplicates, link-down — the failure space the
/// production transports live in and the tests below build on.
/// </summary>
public class TransportTests
{
	private const ulong Host = 1001;
	private const ulong Guest = 2001;

	private static (FakeNetwork Network, FakeTransport Host, FakeTransport Guest) CreatePair()
	{
		var network = new FakeNetwork();
		var host = new FakeTransport(Host, network);
		var guest = new FakeTransport(Guest, network);
		return (network, host, guest);
	}

	[Fact]
	public void Reliable_ReachesPeerInOrder()
	{
		var (network, host, guest) = CreatePair();
		var received = new List<byte>();
		guest.MessageReceived += (_, frame) => received.Add(frame[0]);

		for (byte i = 0; i < 5; i++)
		{
			host.SendTo(Guest, [i], reliable: true);
		}

		Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, received);
	}

	[Fact]
	public void Unreliable_DropRate_LosesSomeMessages()
	{
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { UnreliableDropRate = 0.5 });
		var received = 0;
		guest.MessageReceived += (_, _) => received++;

		for (var i = 0; i < 20; i++)
		{
			host.SendTo(Guest, [(byte)i], reliable: false);
		}

		Assert.True(received > 0, "some messages must get through");
		Assert.True(received < 20, "some messages must drop");
	}

	[Fact]
	public void Delayed_DeliveredAfterClockAdvance()
	{
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { DelayMs = 100 });
		var received = 0;
		guest.MessageReceived += (_, _) => received++;

		host.SendTo(Guest, [(byte)1], reliable: true);
		Assert.Equal(0, received); // not yet due

		network.Advance(99);
		Assert.Equal(0, received); // still not due

		network.Advance(1);
		Assert.Equal(1, received);
	}

	[Fact]
	public void Duplicate_DeliversTwice()
	{
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { Duplicate = true });
		var received = 0;
		guest.MessageReceived += (_, _) => received++;

		host.SendTo(Guest, [(byte)1], reliable: true);

		Assert.Equal(2, received); // retransmission-style duplicate
	}

	[Fact]
	public void LinkDown_SendToReturnsFalse_AndNothingArrives()
	{
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { Down = true });
		var received = 0;
		guest.MessageReceived += (_, _) => received++;

		var result = host.SendTo(Guest, [(byte)1], reliable: true);

		Assert.False(result);
		Assert.Equal(0, received);
	}

	[Fact]
	public void HandlerSends_DoNotReenterTheFlushingBatch()
	{
		// SteamTransport.Poll drains a batch without a handler's own sends being
		// delivered back into the SAME poll. A nested FlushDue from a handler's
		// no-delay send would deliver B inside A's handler (A,C,B) and corrupt
		// every test that reasons about whole-handler atomicity.
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { DelayMs = 100 });
		var delivered = new List<byte>();
		guest.MessageReceived += (_, frame) =>
		{
			delivered.Add(frame[0]);
			if (frame[0] == 1)
			{
				guest.SendTo(Host, [(byte)3], reliable: true); // guest echoes inside A's handler
			}
		};
		host.MessageReceived += (_, frame) => delivered.Add(frame[0]);

		host.SendTo(Guest, [(byte)1], reliable: true); // A
		host.SendTo(Guest, [(byte)2], reliable: true); // B (same due time, queued before the echo)
		network.Advance(100);

		Assert.Equal(new byte[] { 1, 2, 3 }, delivered); // A completes, then B, then the echo
	}

	[Fact]
	public void DifferentDelays_ProduceReorder()
	{
		var (network, host, guest) = CreatePair();
		network.SetFaults(Host, Guest, new LinkFaults { DelayMs = 100 });
		var received = new List<byte>();
		guest.MessageReceived += (_, frame) => received.Add(frame[0]);

		host.SendTo(Guest, [(byte)1], reliable: false); // arrives at t+100
		network.SetFaults(Host, Guest, new LinkFaults());
		host.SendTo(Guest, [(byte)2], reliable: false); // arrives now

		network.Advance(100);

		Assert.Equal(new byte[] { 2, 1 }, received); // out of order
	}
}

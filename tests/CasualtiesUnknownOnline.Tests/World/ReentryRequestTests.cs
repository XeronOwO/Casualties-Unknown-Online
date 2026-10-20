using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The world re-entry ask's wire decision (ticket <c>layer-change-member-recovery</c>): a member
/// the host pulled out of the world asks to be taken back in, and the host answers with the SAME
/// entry instruction every other entry uses. The entry instruction is an edge, so a member whose
/// invite was lost (or discarded by its own entry gate) had no way back; the ask is what makes the
/// entry convergent.
/// <para>
/// What this suite proves is the admission and the answer's identity: a handshaken member the host
/// holds OUT of the world is admitted, a member the host still holds IN the world is refused (its
/// scene re-report is the settling path), the per-sender cooldown bounds the answer's cost, and an
/// admitted ask travels the same answer seam a member's entry does — <c>SendWorldJoinTo</c>, the
/// ordinary targeted instruction, not a second entry mechanism. The ADAPTER type that calls it is
/// Unity-side and not exercised here: its decision is <c>ReentryPolicyTests</c>, its window is
/// <c>ReentryWindowTests</c>, and its real-machine row is the acceptance batch that drops one
/// <c>WorldJoin</c> and reads the member come back.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class ReentryRequestTests
{
	private static List<ulong> ListenForAsks(TestNode host)
	{
		var asks = new List<ulong>();
		host.Services.GetRequiredService<IReentryControl>().ReentryRequested += sender => asks.Add(sender);
		return asks;
	}

	private static void DeliverAsk(ItemSimWorld w, ulong from) =>
		w.Driver.Network.Deliver(from, w.Host.SteamId, NetPacket.Encode(NetMsg.ReentryRequest, new ReentryRequestMsg()));

	[Fact]
	public void AMembersAsk_ReachesTheHostForAMemberItHoldsOutOfTheWorld()
	{
		using var w = ItemSimWorld.Create();
		var asks = ListenForAsks(w.Host);
		w.SetInWorld(w.G1, false);

		Assert.True(w.G1.Services.GetRequiredService<IReentryControl>().TrySendReentryRequest());
		w.Driver.Tick(33);

		Assert.Equal([w.G1.SteamId], asks);
	}

	[Fact]
	public void AnAskTheHostHoldsInWorld_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var asks = ListenForAsks(w.Host);
		w.SetInWorld(w.G1, true);

		Assert.True(w.G1.Services.GetRequiredService<IReentryControl>().TrySendReentryRequest());
		w.Driver.Tick(33);

		Assert.Empty(asks);
	}

	[Fact]
	public void AnAskFromAPeerThatIsNotAMember_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var asks = ListenForAsks(w.Host);

		const ulong Stranger = 999_000_222UL;
		DeliverAsk(w, Stranger);
		w.Driver.Tick(33);

		Assert.Empty(asks);
	}

	[Fact]
	public void AnAskInsideTheCooldown_IsDroppedAndOneAfterIt_IsAdmitted()
	{
		using var w = ItemSimWorld.Create();
		var asks = ListenForAsks(w.Host);
		w.SetInWorld(w.G1, false);

		DeliverAsk(w, w.G1.SteamId);
		w.Driver.Tick(33);
		Assert.Single(asks);

		// A second ask inside the 2 s admission bound is one client talking to itself: the answer it
		// already has (an entry instruction or a named refusal) is the answer to this one.
		DeliverAsk(w, w.G1.SteamId);
		w.Driver.Tick(33);
		Assert.Single(asks);

		w.Driver.Tick(ReentryRequestChannel.MinIntervalMs);
		DeliverAsk(w, w.G1.SteamId);
		w.Driver.Tick(33);
		Assert.Equal([w.G1.SteamId, w.G1.SteamId], asks);
	}

	[Fact]
	public void TheHostsOwnAsk_TravelsNowhere()
	{
		using var w = ItemSimWorld.Create();
		var asks = ListenForAsks(w.Host);

		// The host's own entry is its own click — there is no host to ask.
		Assert.False(w.Host.Services.GetRequiredService<IReentryControl>().TrySendReentryRequest());
		w.Driver.Tick(33);

		Assert.Empty(asks);
	}

	[Fact]
	public void AnAdmittedAsk_IsAnsweredWithTheOrdinaryEntryInstruction()
	{
		using var w = ItemSimWorld.Create();
		w.SetInWorld(w.G1, false);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);

		var joins = 0;
		w.G1.Services.GetRequiredService<IWorldControl>().WorldJoinReceived += _ => joins++;
		w.Host.Services.GetRequiredService<IReentryControl>().ReentryRequested += sender =>
			w.Host.Services.GetRequiredService<IWorldControl>().SendWorldJoinTo(sender);

		Assert.True(w.G1.Services.GetRequiredService<IReentryControl>().TrySendReentryRequest());
		w.Driver.Tick(33);

		// This suite wires the answer SEAM the adapter's handler runs on (the admitted event →
		// SendWorldJoinTo) because the adapter type itself is Unity-side: what it proves is that the
		// seam sends the ordinary instruction — the targeted WorldJoin that promises the run baseline
		// which follows it — not that a second entry mechanism exists. That the handler calls it is
		// ReentryPolicyTests' decision plus the acceptance batch's row.
		Assert.Equal(1, joins);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.WorldJoin));
	}
}

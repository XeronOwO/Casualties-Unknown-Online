using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The end-of-layer choice's wire decision (ticket
/// <c>layer-complete-choice-for-members</c>): a member's choice reaches the host
/// as ONE request stamped with its kernel generation, and the host admits it only
/// from a handshaken member and only for the layer it is in.
/// <para>
/// What this suite proves is the arbitration, not the drive: the host's advance
/// itself is the game's own coroutine and is covered by the adapter-side policy
/// (<c>LayerAdvancePolicyTests</c>) and the real-machine row. The stamp is the
/// same identity the direct world reports carry, so the refusals below are the
/// ones a second member's click and a peer without a run baseline produce.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class LayerAdvanceRequestTests
{
	private static List<ulong> ListenForChoices(TestNode host)
	{
		var choices = new List<ulong>();
		host.Services.GetRequiredService<ILayerAdvanceControl>().LayerAdvanceRequested += sender => choices.Add(sender);
		return choices;
	}

	private static void DeliverRequest(ItemSimWorld w, ulong from, LayerAdvanceRequestMsg msg) =>
		w.Driver.Network.Deliver(from, w.Host.SteamId, NetPacket.Encode(NetMsg.LayerAdvanceRequest, msg));

	[Fact]
	public void AMembersChoice_ReachesTheHostForTheLayerTheSessionIsIn()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33); // the member follows this host's baseline before choosing

		Assert.True(w.G1.Services.GetRequiredService<ILayerAdvanceControl>().TrySendLayerAdvanceRequest());
		w.Driver.Tick(33);

		Assert.Equal([w.G1.SteamId], choices);
	}

	/// <summary>A peer with no committed run baseline cannot name the layer it is asking about, so nothing is sent and the caller is told — otherwise the member would suppress its own descent for a request that never existed.</summary>
	[Fact]
	public void AChoiceWithNothingToStamp_IsNotSent()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);

		Assert.False(w.G1.Services.GetRequiredService<ILayerAdvanceControl>().TrySendLayerAdvanceRequest());
		w.Driver.Tick(33);

		Assert.Empty(choices);
	}

	[Fact]
	public void AChoiceForALayerTheHostHasLeft_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33);

		// The member's stamp was taken before the descent the host has since
		// committed — the request names the layer the session already left.
		DeliverRequest(w, w.G1.SteamId, new LayerAdvanceRequestMsg { Generation = WorldGenerationReports.StampOf(w.Host, layerOverride: 1) });
		w.Driver.Tick(33);

		Assert.Empty(choices);
	}

	[Fact]
	public void AChoiceWithNoGenerationStamp_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33);

		// A peer with no committed run baseline cannot name the layer it is asking
		// about, so there is nothing to arbitrate it against.
		DeliverRequest(w, w.G1.SteamId, new LayerAdvanceRequestMsg());
		w.Driver.Tick(33);

		Assert.Empty(choices);
	}

	[Fact]
	public void AChoiceFromAPeerThatIsNotAMember_IsRefused()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33);

		const ulong Stranger = 999_000_111UL;
		DeliverRequest(w, Stranger, new LayerAdvanceRequestMsg { Generation = WorldGenerationReports.StampOf(w.Host) });
		w.Driver.Tick(33);

		Assert.Empty(choices);
	}

	[Fact]
	public void TheHostsOwnChoice_TravelsNowhere()
	{
		using var w = ItemSimWorld.Create();
		var choices = ListenForChoices(w.Host);
		var guestChoices = ListenForChoices(w.G1);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		w.Driver.Tick(33);

		// The host's own click IS the session's advance — the adapter drives the
		// game's entry directly, so no request is produced on either side.
		Assert.False(w.Host.Services.GetRequiredService<ILayerAdvanceControl>().TrySendLayerAdvanceRequest());
		w.Driver.Tick(33);

		Assert.Empty(choices);
		Assert.Empty(guestChoices);
	}
}

using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The player state stream bandwidth regression: the host builds one stream per
/// recipient and omits the recipient's own player entry. A client already owns
/// its local state, so echoing that entry back is pure redundant bytes on every
/// high-frequency frame. The stream must still carry the host and every other
/// member so remote clones keep rendering.
/// </summary>
public class StateStreamBandwidthTests
{
	[Fact]
	public void HostPlayerStream_OmitsRecipientOwnState_ButKeepsOthers()
	{
		using var world = ItemSimWorld.Create();
		world.Host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 2f));
		world.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 4f));
		world.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(5f, 6f));

		var g1Streams = new List<WireStateStream>();
		var g2Streams = new List<WireStateStream>();
		world.G1.Transport.MessageReceived += (_, frame) => CollectPlayerStreams(frame, g1Streams);
		world.G2.Transport.MessageReceived += (_, frame) => CollectPlayerStreams(frame, g2Streams);

		// Let entity sync start and produce a steady stream (and the first
		// join snapshots) before asserting.
		for (var i = 0; i < 20; i++)
		{
			world.Driver.Tick(33);
		}

		var g1Entity = world.G1.Services.GetRequiredService<IEntitySyncControl>().LocalPlayer.EntityId;
		var g2Entity = world.G2.Services.GetRequiredService<IEntitySyncControl>().LocalPlayer.EntityId;
		var hostEntity = world.Host.Services.GetRequiredService<IEntitySyncControl>().LocalPlayer.EntityId;

		var g1States = g1Streams.SelectMany(s => s.PlayerStates).ToList();
		var g2States = g2Streams.SelectMany(s => s.PlayerStates).ToList();

		Assert.NotEmpty(g1Streams);
		Assert.NotEmpty(g2Streams);
		Assert.NotEmpty(g1States);
		Assert.NotEmpty(g2States);

		Assert.DoesNotContain(g1States, p => p.EntityId.Epoch == g1Entity.Epoch && p.EntityId.Counter == g1Entity.Counter);
		Assert.DoesNotContain(g2States, p => p.EntityId.Epoch == g2Entity.Epoch && p.EntityId.Counter == g2Entity.Counter);

		Assert.Contains(g1States, p => p.EntityId.Epoch == hostEntity.Epoch && p.EntityId.Counter == hostEntity.Counter);
		Assert.Contains(g2States, p => p.EntityId.Epoch == hostEntity.Epoch && p.EntityId.Counter == hostEntity.Counter);
	}

	private static void CollectPlayerStreams(byte[] frame, List<WireStateStream> into)
	{
		if (frame.Length < 1 || frame[0] != (byte)NetMsg.KernelEnvelope)
		{
			return;
		}

		var envelope = NetPacket.DecodePayload<ProtocolFrame>(frame);
		if (envelope.StateStream?.Header.PayloadType != WirePayloadType.PlayerStateStream)
		{
			return;
		}

		into.Add(envelope.StateStream.Stream);
	}
}

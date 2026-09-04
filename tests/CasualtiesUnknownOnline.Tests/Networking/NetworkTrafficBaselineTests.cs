using System.Diagnostics;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// Network traffic baseline regression suite. It locks the per-payload
/// percentile math at the pure tracker layer, verifies the real
/// <see cref="KernelEnvelopeHandler"/> path records kernel payload stats on a
/// live host/guest pair, and measures the checkpoint split/assemble/restore
/// shape that the bandwidth-reduction tickets will need before optimizing.
/// </summary>
public class NetworkTrafficBaselineTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void PayloadTracker_ComputesP50P95FrequencyPerPayloadType()
	{
		var tracker = new NetworkTrafficTracker(1000);

		tracker.RecordSend(1, NetMsg.KernelEnvelope, 100, true, WirePayloadType.ItemSnapshotStream);
		tracker.RecordSend(1, NetMsg.KernelEnvelope, 200, true, WirePayloadType.ItemSnapshotStream);
		tracker.RecordSend(1, NetMsg.KernelEnvelope, 300, true, WirePayloadType.ItemSnapshotStream);
		tracker.RecordReceivePayload(1, WirePayloadType.ItemSnapshotStream, 100);
		tracker.RecordReceivePayload(1, WirePayloadType.ItemSnapshotStream, 200);
		tracker.RecordReceivePayload(1, WirePayloadType.ItemSnapshotStream, 300);

		var window = tracker.Snapshot();

		var send = AssertPayload(window.SendByPayloadType, WirePayloadType.ItemSnapshotStream);
		Assert.Equal(3, send.Count);
		Assert.Equal(600, send.Bytes);
		Assert.Equal(200, send.P50Bytes);
		Assert.Equal(300, send.P95Bytes);
		Assert.Equal(100, send.MinBytes);
		Assert.Equal(300, send.MaxBytes);

		var receive = AssertPayload(window.ReceiveByPayloadType, WirePayloadType.ItemSnapshotStream);
		Assert.Equal(3, receive.Count);
		Assert.Equal(600, receive.Bytes);
		Assert.Equal(200, receive.P50Bytes);
		Assert.Equal(300, receive.P95Bytes);
	}

	[Fact]
	public void LivePair_RecordsPerPeerBytesAndPerPayloadStats()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 2f));
			guest.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 4f));

			// The first host pump starts member sync and the join snapshots;
			// measure the steady-state window that follows.
			host.Update();
			for (var elapsed = 0; elapsed < 1000; elapsed += 10)
			{
				host.Clock.Advance(10);
				host.Update();
			}

			var hostMonitor = host.Services.GetRequiredService<NetworkTrafficMonitor>();
			var guestMonitor = guest.Services.GetRequiredService<NetworkTrafficMonitor>();

			var hostWindow = hostMonitor.CurrentWindow;
			var guestWindow = guestMonitor.CurrentWindow;

			Assert.True(hostWindow.ByPeer.TryGetValue(GuestId, out var hostPeer));
			Assert.True(hostPeer.SendCount > 0, "host must send steady-state frames to the guest");
			Assert.True(hostPeer.SendBytes > 0);

			Assert.True(guestWindow.ByPeer.TryGetValue(HostId, out var guestPeer));
			Assert.True(guestPeer.ReceiveCount > 0, "guest must receive the host stream");
			Assert.True(guestPeer.ReceiveBytes > 0);

			Assert.True(hostWindow.SendByPayloadType.ContainsKey(WirePayloadType.PlayerStateStream),
				"host send stats must include the 20 Hz player stream");
			Assert.True(guestWindow.ReceiveByPayloadType.ContainsKey(WirePayloadType.PlayerStateStream),
				"guest receive stats must include the 20 Hz player stream");

			var sent = hostWindow.SendByPayloadType[WirePayloadType.PlayerStateStream];
			var received = guestWindow.ReceiveByPayloadType[WirePayloadType.PlayerStateStream];
			Assert.True(sent.Count > 0);
			Assert.True(sent.Bytes > 0);
			Assert.True(sent.P50Bytes > 0);
			Assert.True(sent.P95Bytes >= sent.P50Bytes);
			Assert.True(received.Count > 0);
			Assert.True(received.Bytes > 0);
			Assert.True(received.P50Bytes > 0);
			Assert.True(received.P95Bytes >= received.P50Bytes);
		}
	}

	[Fact]
	public void CheckpointBaseline_RecordsChunkCountSizeAndRestoreTime()
	{
		var checkpoint = CreateCheckpoint(600);
		var chunks = WireCheckpointAssembler.Split(checkpoint);

		Assert.True(chunks.Count > 1, "600 items must split into more than one checkpoint chunk");
		Assert.Equal(chunks.Count, chunks[0].ChunkCount);
		Assert.Equal(600, chunks.Sum(c => c.Items.Count));

		var frames = chunks
			.Select(chunk => NetPacket.Encode(NetMsg.KernelEnvelope, new ProtocolFrame
			{
				Kind = EnvelopeKind.Checkpoint,
				Checkpoint = new CheckpointEnvelope
				{
					Header = new EnvelopeHeader { PayloadType = WirePayloadType.CheckpointChunk },
					Checkpoint = chunk,
				},
			}))
			.ToList();

		var totalBytes = frames.Sum(f => f.Length);
		var restoredCheckpoint = WireCheckpointAssembler.Assemble(chunks);
		var restoreKernel = new GameStateKernel(new RunEpoch(1));
		var started = Stopwatch.GetTimestamp();
		var result = restoreKernel.Restore(restoredCheckpoint);
		var restoreMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

		Assert.True(totalBytes > 0);
		Assert.True(result.Success);
		Assert.Equal(600, restoreKernel.QueryItems().Count);
		Assert.True(restoreMs >= 0);
	}

	private static GameCheckpoint CreateCheckpoint(int itemCount)
	{
		var epoch = new RunEpoch(1);
		var kernel = new GameStateKernel(epoch);
		var actor = new ActorId(1001);
		for (var i = 0; i < itemCount; i++)
		{
			var decision = kernel.Execute(
				new SpawnItemCommand(
					new OperationId((ulong)(i + 1)),
					actor,
					epoch,
					AuthorityKind.HostOnly,
					new ItemIdentity((ulong)i, "shell"),
					ItemLocation.World((float)i, 0f),
					0,
					new ItemData(1f, false, -1, [], [])),
				new CommandContext(epoch, actor));
			Assert.True(decision.IsAccepted, $"spawn {i} must be accepted");
		}

		return kernel.CreateCheckpoint();
	}

	private static NetworkTrafficWindow.PayloadTraffic AssertPayload(
		System.Collections.Generic.IReadOnlyDictionary<WirePayloadType, NetworkTrafficWindow.PayloadTraffic> map,
		WirePayloadType payloadType)
	{
		Assert.True(map.TryGetValue(payloadType, out var traffic), $"missing payload stats for {payloadType}");
		return traffic!;
	}
}

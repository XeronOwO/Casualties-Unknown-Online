using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

/// <summary>
/// End-to-end proof that the Stage 1 adaptive rate service actually moves the
/// production host fan-out: with a high-RTT peer-health observation, the
/// loss-tolerant overwrite streams (player, enemy, tutorial claw) must send at
/// a reduced cadence over the real fake network. Reliable/control traffic is
/// not routed through this service and is intentionally untouched.
/// </summary>
public class AdaptiveStreamRateIntegrationTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HighPeerRtt_ReducesLossTolerantStreamCadence()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		using (host)
		using (guest)
		{
			host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 2f));
			guest.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 4f));

			// First pump starts member sync and sends join snapshots; count from
			// the next pump so the measured window is the steady-state cadence.
			host.Update();

			var hostMonitor = host.Services.GetRequiredService<NetworkTrafficMonitor>();
			hostMonitor.RecordPingSent(GuestId, sendTicks: 1000, nowMs: 1000);
			hostMonitor.RecordPong(GuestId, rttMs: 500f, echoTicks: 1000);

			host.Services.GetRequiredService<ITutorialClawControl>().PublishTutorialClawState(
				new TutorialClawStateMsg
				{
					HandPosX = 1f,
					HandPosY = 2f,
					HandPosCurrentX = 3f,
					HandPosCurrentY = 4f,
					GrabKind = TutorialClawStateMsg.GrabNone,
					Material = TutorialClawStateMsg.MaterialOpen,
				});

			var playerFrames = 0;
			var enemyFrames = 0;
			var tutorialFrames = 0;
			guest.Transport.MessageReceived += (_, frame) =>
			{
				if (frame.Length < 1)
				{
					return;
				}

				if (frame[0] == (byte)NetMsg.TutorialClawState)
				{
					tutorialFrames++;
					return;
				}

				if (frame[0] != (byte)NetMsg.KernelEnvelope)
				{
					return;
				}

				var envelope = NetPacket.DecodePayload<ProtocolFrame>(frame);
				if (envelope.StateStream is null)
				{
					return;
				}

				if (envelope.StateStream.Header.PayloadType == WirePayloadType.PlayerStateStream)
				{
					playerFrames++;
				}
				else if (envelope.StateStream.Header.PayloadType == WirePayloadType.EnemyStateStream)
				{
					enemyFrames++;
				}
			};

			for (var elapsed = 0; elapsed < 1000; elapsed += 10)
			{
				host.Clock.Advance(10);
				host.Update();
			}

			// Critical pressure with priority 1: 20 Hz * 0.25 = 5 Hz.
			// With priority 2: 20 Hz * 0.15 = 3 Hz. The old fixed 20 Hz would
			// produce ~18-23 frames for player/enemy and ~18-23 for tutorial.
			Assert.InRange(playerFrames, 1, 9);
			Assert.InRange(enemyFrames, 1, 9);
			Assert.InRange(tutorialFrames, 1, 7);
		}
	}
}

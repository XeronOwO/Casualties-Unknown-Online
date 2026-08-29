using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The formerly hard-coded 20 Hz cadence is now the configured
/// <see cref="StateStreamOptions"/>. This runs the production EntitySync and
/// EnemySync pumps over the fake network and counts the actual host→guest
/// snapshots per second — an options change through DI must move the real
/// send throttle, not just a settings property.
/// </summary>
public class StateStreamFrequencyTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Theory]
	[InlineData(20, 18, 23)]
	[InlineData(10, 8, 12)]
	[InlineData(5, 4, 7)]
	public void PlayerAndEnemyStreams_FollowTheConfiguredFrequency(
		int hz, int minPlayerFrames, int maxPlayerFrames)
	{
		var options = new MutableOptionsMonitor<StateStreamOptions>(
			new StateStreamOptions { StateStreamHz = hz });
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId,
			extraRegistrations: s => s.Replace(
				ServiceDescriptor.Singleton<IOptionsMonitor<StateStreamOptions>>(options)));
		using (host)
		using (guest)
		{
			host.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(1f, 2f));
			guest.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene", new NetVector2(3f, 4f));

			// The first host pump starts both member syncs and sends the join
			// snapshots; count from the next pump so the measured window is the
			// steady-state cadence.
			host.Update();
			var playerFrames = 0;
			var enemyFrames = 0;
			guest.Transport.MessageReceived += (_, frame) =>
			{
				if (frame.Length < 1)
				{
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

			Assert.InRange(playerFrames, minPlayerFrames, maxPlayerFrames);
			Assert.InRange(enemyFrames, minPlayerFrames, maxPlayerFrames);
		}
	}
}

using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Steamworks;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// The send path's refusal handling, driven through the Steam seam: a peer whose
/// queue refuses every send costs a bounded number of Steam calls and a bounded
/// number of log lines — never one of each per attempt, which is how Run E's host
/// wrote 1.3 GB of log and stopped answering its own evaluator.
/// </summary>
public class SteamTransportRefusalTests
{
	private const ulong Peer = 7001;

	private static SteamTransport CreateTransport(
		FakeSteamSendChannel channel,
		FakeClock clock,
		RecordingLogger<SteamTransport> log) =>
		new(new FakeSteamService(1), channel, clock, log);

	[Fact]
	public void SustainedRefusals_CostBoundedSteamCallsAndLogLines()
	{
		var clock = new FakeClock();
		var log = new RecordingLogger<SteamTransport>();
		var channel = new FakeSteamSendChannel { Result = EResult.k_EResultLimitExceeded };
		var transport = CreateTransport(channel, clock, log);

		// Ten seconds of the Run E shape: several streams attempt on every frame
		// (a thousand sends per second) while the transport is ticked per frame.
		for (var i = 0; i < 10_000; i++)
		{
			transport.SendTo(Peer, [1, 2, 3], reliable: true);
			clock.Advance(1);
			if (i % 16 == 0)
			{
				transport.FlushSendReports();
			}
		}

		Assert.True(channel.SendCalls <= 20,
			$"a refusing peer must be gated by backoff: {channel.SendCalls} Steam calls for 10 000 attempts");
		Assert.True(log.Entries.Count <= 20,
			$"refusals must aggregate: {log.Entries.Count} log entries for 10 000 attempts");
		Assert.True(channel.SessionQueries <= 2,
			$"the native session query is a diagnostic cost, not a gate input: {channel.SessionQueries} queries for 10 000 attempts");
		Assert.True(channel.RelayQueries <= 2,
			$"the relay query rides the same detailed line: {channel.RelayQueries} queries for 10 000 attempts");
		Assert.Contains(log.Entries, entry => entry.Message.Contains("suppressed attempt(s)"));
		Assert.Contains(log.Entries, entry => entry.Message.Contains("queue full for"));
	}

	[Fact]
	public void Recovery_SendsAgain_AndAnnouncesTheEpisodeOnce()
	{
		var clock = new FakeClock();
		var log = new RecordingLogger<SteamTransport>();
		var channel = new FakeSteamSendChannel { Result = EResult.k_EResultLimitExceeded };
		var transport = CreateTransport(channel, clock, log);

		Assert.False(transport.SendTo(Peer, [1], reliable: true)); // refused; the episode starts
		Assert.Equal(1, channel.SendCalls);
		Assert.False(transport.SendTo(Peer, [1], reliable: true)); // suppressed by the gate
		Assert.False(transport.SendTo(Peer, [1], reliable: true));
		Assert.Equal(1, channel.SendCalls);

		clock.Advance(250);
		channel.Result = EResult.k_EResultOK; // the peer drained
		Assert.True(transport.SendTo(Peer, [1], reliable: true));
		Assert.Equal(2, channel.SendCalls);
		Assert.Equal(1, RecoveryLines(log));

		// The peer is not gated any more, and the recovery is announced exactly once.
		Assert.True(transport.SendTo(Peer, [1], reliable: true));
		Assert.Equal(3, channel.SendCalls);
		Assert.Equal(1, RecoveryLines(log));
	}

	[Fact]
	public void ResetRefusals_ReopensTheGate_SoTheNextSessionStartsClean()
	{
		var clock = new FakeClock();
		var log = new RecordingLogger<SteamTransport>();
		var channel = new FakeSteamSendChannel { Result = EResult.k_EResultLimitExceeded };
		var transport = CreateTransport(channel, clock, log);

		Assert.False(transport.SendTo(Peer, [1], reliable: true)); // refused; the gate closes
		Assert.False(transport.SendTo(Peer, [1], reliable: true)); // suppressed: no Steam call
		Assert.Equal(1, channel.SendCalls);

		((ISendStallSource)transport).ResetRefusals();

		// The next session's first attempt reaches Steam again at the same clock
		// reading — without the reset the closed gate would swallow it.
		Assert.False(transport.SendTo(Peer, [1], reliable: true));
		Assert.Equal(2, channel.SendCalls);
		Assert.False(transport.SendTo(Peer, [1], reliable: true)); // a fresh episode gates again
		Assert.Equal(2, channel.SendCalls);
	}

	[Fact]
	public void Stall_RaisesTheEscalationEdge_OncePerEpisode()
	{
		var clock = new FakeClock();
		var log = new RecordingLogger<SteamTransport>();
		var channel = new FakeSteamSendChannel { Result = EResult.k_EResultLimitExceeded };
		var transport = CreateTransport(channel, clock, log);

		var stalledPeers = new List<ulong>();
		((ISendStallSource)transport).PeerSendStalled += peer => stalledPeers.Add(peer);

		for (var i = 0; i < 40_000; i++)
		{
			transport.SendTo(Peer, [1], reliable: true);
			clock.Advance(1);
			if (i % 16 == 0)
			{
				transport.FlushSendReports();
			}
		}

		Assert.Equal(Peer, Assert.Single(stalledPeers));
		Assert.True(log.HasError("refused every send"));
	}

	private static int RecoveryLines(RecordingLogger<SteamTransport> log) =>
		log.Entries.Count(entry => entry.Level == LogLevel.Information && entry.Message.Contains("recovered"));
}

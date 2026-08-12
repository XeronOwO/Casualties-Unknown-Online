using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The speech channel (the Talker domain — the bubble text is DATA: the final
/// string, the receiver only displays it): a player's bubble reports
/// guest → host and relays source-excluded (its own bubble is local); a
/// trader's bubble is host-broadcast to every member (the host's trader is
/// authoritative). The GameAdapter side (TalkPatch suppression + the clone
/// replay) is the phase-B runtime verification.
/// </summary>
public class SpeechChannelTests
{
	[Fact]
	public void PlayerBubble_RelaysExcludingSource()
	{
		var w = EntityEventSimWorld.Create();
		var hostBubbles = new List<SpeechMsg>();
		var g2Bubbles = new List<SpeechMsg>();
		var g1Bubbles = new List<SpeechMsg>();
		w.Host.Services.GetRequiredService<IWorldControl>().SpeechReceived += (_, msg) => hostBubbles.Add(msg);
		w.G1.Services.GetRequiredService<IWorldControl>().SpeechReceived += (_, msg) => g1Bubbles.Add(msg);
		w.G2.Services.GetRequiredService<IWorldControl>().SpeechReceived += (_, msg) => g2Bubbles.Add(msg);

		w.G1.Services.GetRequiredService<IWorldControl>().SendSpeech(new SpeechMsg { SpeakerSteamId = w.G1.SteamId, Text = "hi there" });

		Assert.True(hostBubbles.Count == 1, $"the host must receive the report, got {hostBubbles.Count}");
		Assert.True(hostBubbles[0].Text == "hi there", "the final string rides through");
		Assert.True(g2Bubbles.Count == 1, $"the other guest must get the relay, got {g2Bubbles.Count}");
		Assert.True(g2Bubbles[0].SpeakerSteamId == w.G1.SteamId, "the speaker key rides through");
		Assert.Empty(g1Bubbles); // source excluded — its own bubble is local
	}

	[Fact]
	public void TraderBubble_BroadcastsToEveryMember()
	{
		var w = EntityEventSimWorld.Create();
		var g1Bubbles = new List<SpeechMsg>();
		var g2Bubbles = new List<SpeechMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().SpeechReceived += (_, msg) => g1Bubbles.Add(msg);
		w.G2.Services.GetRequiredService<IWorldControl>().SpeechReceived += (_, msg) => g2Bubbles.Add(msg);

		w.Host.Services.GetRequiredService<IWorldControl>().BroadcastSpeech(0, new SpeechMsg
		{
			SpeakerSteamId = 0,
			TraderPosition = new NetVector2Msg(5f, 6f),
			Text = "trade talk",
		});

		Assert.True(g1Bubbles.Count == 1 && g2Bubbles.Count == 1,
			$"every member gets the trader bubble (g1: {g1Bubbles.Count}, g2: {g2Bubbles.Count})");
		Assert.True(g1Bubbles[0].TraderPosition!.X == 5f && g1Bubbles[0].TraderPosition!.Y == 6f, "the trader position key rides through");
		Assert.True(g1Bubbles[0].Text == "trade talk", "the final string rides through");
	}
}

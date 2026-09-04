using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;
using HarmonyLib;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The speech domain coordinator: player bubbles report (guest → host) and fan
/// out (the handler relays, source excluded); trader bubbles are host-broadcast
/// (the host's trader is authoritative — the guests' traders are suppressed
/// from talking on their own by TalkPatch and only replay). The replay writes
/// the FINAL string into the peer's clone (currentString + timeSinceTalked = 0
/// — the clone's own Update types it out and clears it exactly like the
/// speaking side, Talker.cs:380-414), wrapped in the RemoteApply scope so a
/// future talk path can never re-report it.
/// </summary>
internal sealed class SpeechSync(IWorldControl world, ISessionControl session, ILogger<SpeechSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ISessionControl _session = session;
	private readonly ILogger<SpeechSync> _log = log;

	internal void BindToSession() => _world.SpeechReceived += OnSpeechReceived;

	internal void Unbind() => _world.SpeechReceived -= OnSpeechReceived;

	/// <summary>The patch-bridge entry: a bubble was spoken (the game method ran
	/// in full — the text is final). A trader's bubble is host-broadcast (the
	/// guests' traders are suppressed — this never fires there); a player's
	/// bubble reports on the guest and broadcasts on the host (its own bubble is
	/// local; there is no Steam self-connection, so the broadcast only reaches
	/// the other members).</summary>
	internal void OnSpeechReported(Talker talker, string text)
	{
		if (!_session.SessionActive || talker == null) // Unity object — ==
		{
			return;
		}

		if (talker.trader != null) // Unity object — ==
		{
			if (_session.Role != SessionRole.Host)
			{
				return; // a guest trader is suppressed by TalkPatch — defensive
			}

			var pos = talker.trader.transform.position;
			_world.BroadcastSpeech(0, new SpeechMsg { SpeakerSteamId = 0, TraderPosition = new NetVector2Msg(pos.x, pos.y), Text = text });
			_log.LogInformation("[Speech] host trader bubble at ({X:0.0},{Y:0.0}).", pos.x, pos.y);
			return;
		}

		var msg = new SpeechMsg { SpeakerSteamId = _session.LocalSteamId, Text = text };
		if (_session.Role == SessionRole.Host)
		{
			_world.BroadcastSpeech(0, msg);
			_log.LogInformation("[Speech] host player bubble broadcast.");
		}
		else
		{
			_world.SendSpeech(msg);
			_log.LogInformation("[Speech] player bubble reported.");
		}
	}

	/// <summary>A bubble arrived: the host — apply to its own clone of the
	/// speaker (the handler already relayed to the other guests); a guest —
	/// apply to its clone. Trader bubbles replay on the trader at the position.</summary>
	private void OnSpeechReceived(ulong sender, SpeechMsg msg)
	{
		if (msg.SpeakerSteamId == 0)
		{
			ReplayOnTrader(msg);
			return;
		}

		ReplayOnClone(msg.SpeakerSteamId, msg.Text);
	}

	/// <summary>The peer's clone of the speaker (named "Character_{SteamId:X}" by
	/// RemoteBodyFactory) types out the bubble.</summary>
	private void ReplayOnClone(ulong steamId, string text)
	{
		var clone = GameObject.Find($"Character_{steamId:X}");
		var body = clone?.GetComponentInChildren<Body>();
		if (body == null || body.talker == null) // Unity objects — ==
		{
			_log.LogInformation("[Speech] clone of {Speaker} not found — dropped.", steamId);
			return;
		}

		Replay(body.talker, text);
		_log.LogInformation("[Speech] replayed on the clone of {Speaker}.", steamId);
	}

	/// <summary>The trader at the position key (position-keyed like the trade
	/// domain — both sides generated the same trader at the same place).</summary>
	private void ReplayOnTrader(SpeechMsg msg)
	{
		var pos = msg.TraderPosition;
		if (pos == null)
		{
			return;
		}

		foreach (var trader in Object.FindObjectsOfType<TraderScript>())
		{
			if (Vector2.Distance(trader.transform.position, new Vector2(pos.X, pos.Y)) < 2f)
			{
				var talker = trader.GetComponent<Talker>();
				if (talker != null) // Unity object — ==
				{
					Replay(talker, msg.Text);
					_log.LogInformation("[Speech] replayed trader bubble at ({X:0.0},{Y:0.0}).", pos.X, pos.Y);
				}

				return;
			}
		}

		_log.LogInformation("[Speech] trader not found at ({X:0.0},{Y:0.0}) — dropped.", pos.X, pos.Y);
	}

	/// <summary>Write the final string into the talker — its own Update types it
	/// out and clears the bubble (Talker.cs:380-414), exactly like the speaking
	/// side. Never calls Talk() — that would re-roll the line/distortion locally.
	/// The TextMeshPro reference is touched dynamically (the TextMeshPro assembly
	/// is not referenced).</summary>
	private static void Replay(Talker talker, string text)
	{
		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			var fields = Traverse.Create(talker);
			fields.Field("currentString").SetValue(text);
			fields.Field("timeSinceTalked").SetValue(0f);
			var bubble = fields.Field("text").GetValue(); // the TextMeshPro — untouched if null (the bubble GameObject is lazily created)
			if (bubble != null)
			{
				Traverse.Create(bubble).Property("text").SetValue("");
			}
		}
	}
}

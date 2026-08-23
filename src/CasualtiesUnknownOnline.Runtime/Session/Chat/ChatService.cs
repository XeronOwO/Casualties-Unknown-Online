using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Chat;

/// <summary>
/// The text-chat domain: a bounded recent-message buffer plus the send path.
/// It is deliberately pure Runtime — no Unity object, no Steamworks — so the
/// same service runs in the full plugin and in the L0 fake-network tests.
/// Wire plumbing lives in <see cref="ChatChannel"/>; this service only reacts
/// to the world channel's receive event and hands UI lines to the overlay.
/// </summary>
public sealed class ChatService : IChatControl
{
	/// <summary>How many recent lines the local buffer keeps (UI panel height bound).</summary>
	private const int MaxRecent = 50;

	private readonly ISessionControl _session;
	private readonly IWorldControl _world;
	private readonly ILogger<ChatService> _log;
	private readonly List<ChatLine> _recent = [];

	public ChatService(ISessionControl session, IWorldControl world, ILogger<ChatService> log)
	{
		_session = session;
		_world = world;
		_log = log;
		_world.ChatReceived += OnChatReceived;
		_session.SessionEnded += Clear;
	}

	public IReadOnlyList<ChatLine> Recent => _recent;

	public event Action<ChatLine>? MessageReceived;

	public bool TrySend(string text)
	{
		if (!_session.SessionActive || _session.Role == SessionRole.None)
		{
			return false;
		}

		var normalized = ChatPolicy.Normalize(text);
		if (!ChatPolicy.IsValid(normalized))
		{
			return false;
		}

		var msg = new ChatMsg
		{
			SenderSteamId = _session.LocalSteamId,
			Text = normalized,
		};

		AddLocal(msg.SenderSteamId, msg.Text);

		if (_session.Role == SessionRole.Host)
		{
			_world.BroadcastChat(msg.SenderSteamId, msg);
		}
		else
		{
			_world.SendChat(msg);
		}

		_log.LogDebug("[Chat] sent local line sender={Sender} len={Length}.", msg.SenderSteamId, msg.Text.Length);
		return true;
	}

	private void OnChatReceived(ulong sender, ChatMsg msg)
	{
		// A received line is either a guest report at the host or a host relay
		// at a guest. The host's relay excludes the author, and the sender's own
		// UI already added the line locally, so an echo of the local SteamId is
		// always a duplicate and is skipped.
		if (msg.SenderSteamId == _session.LocalSteamId)
		{
			return;
		}

		if (!ChatPolicy.IsValid(msg.Text))
		{
			_log.LogWarning("[Chat] dropping invalid received line sender={Sender}.", msg.SenderSteamId);
			return;
		}

		AddLocal(msg.SenderSteamId, msg.Text);
	}

	private void AddLocal(ulong senderSteamId, string text)
	{
		var line = new ChatLine(senderSteamId, text);
		_recent.Add(line);
		if (_recent.Count > MaxRecent)
		{
			_recent.RemoveAt(0);
		}

		_log.LogDebug("[Chat] buffer line sender={Sender} len={Length} now={Count}.", senderSteamId, text.Length, _recent.Count);
		MessageReceived?.Invoke(line);
	}

	private void Clear()
	{
		_recent.Clear();
		_log.LogDebug("[Chat] recent buffer cleared on session end.");
	}
}

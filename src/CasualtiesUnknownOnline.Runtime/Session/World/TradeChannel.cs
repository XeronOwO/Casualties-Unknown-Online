using System;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The trade channel: the trader-state and trader-action message plumbing —
/// extracted from WorldService (the 600-line gate) like EntityEventChannel so
/// the world domain stays one responsibility. The trader's state is
/// host-authoritative: the host computes and broadcasts full state overwrites;
/// a guest reports its locally-executed interactions and applies the
/// overwrites (its provisional local state is replaced).
/// </summary>
public sealed class TradeChannel(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>Host only: send one trader's authoritative state to one member (world entry, the 5 s fallback).</summary>
	public void SendTraderState(ulong targetSteamId, TraderStateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.TraderState, msg);
	}

	/// <summary>Host only: broadcast one trader's authoritative state to every member (an interaction just changed it — the acting side included, its local state was provisional).</summary>
	public void BroadcastTraderState(TraderStateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.TraderState, msg);
	}

	/// <summary>Guest: a trader's authoritative state arrived — apply the full overwrite.</summary>
	public event Action<TraderStateMsg>? TraderStateReceived;

	public void FireTraderStateReceived(TraderStateMsg msg) => TraderStateReceived?.Invoke(msg);

	/// <summary>
	/// Report a locally-executed trader interaction (the game method ran in full
	/// on this side — the player-side effects are immediate): guest → host as a
	/// report (the host executes the trader-side change and broadcasts the
	/// authoritative state to every member).
	/// </summary>
	public void SendTraderAction(TraderActionMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.TraderAction, msg);
	}

	/// <summary>Host: a guest's trader interaction arrived — execute the trader-side change (the adapter's TradeExecutor) and broadcast the state.</summary>
	public event Action<ulong, TraderActionMsg>? TraderActionReceived;

	public void FireTraderActionReceived(ulong sender, TraderActionMsg msg) => TraderActionReceived?.Invoke(sender, msg);

	/// <summary>Guest: send a trader-recruit request to the host (the acting side
	/// has already located its nearest trader; the host owns the gate + revive).</summary>
	public void SendTraderRecruitRequest(TraderRecruitRequestMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.TraderRecruitRequest, msg);
	}

	/// <summary>Host: a guest's trader-recruit request arrived.</summary>
	public event Action<ulong, TraderRecruitRequestMsg>? TraderRecruitRequestReceived;

	public void FireTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg) =>
		TraderRecruitRequestReceived?.Invoke(sender, msg);

	/// <summary>Host only: send the authoritative post-revive body state to the revived player.</summary>
	public void SendTraderRecruitResult(ulong targetSteamId, TraderRecruitResultMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.TraderRecruitResult, msg);
	}

	/// <summary>Guest: the host's trader-recruit result arrived — apply the revive to the local body.</summary>
	public event Action<TraderRecruitResultMsg>? TraderRecruitResultReceived;

	public void FireTraderRecruitResultReceived(TraderRecruitResultMsg msg) =>
		TraderRecruitResultReceived?.Invoke(msg);

	/// <summary>
	/// Report/broadcast a hostile trader swing presentation: a guest reports
	/// its local trader's swing to the host; the host sends its own swing to
	/// every handshaken guest. The source side already played the visual
	/// locally, so the host never sends its own swing back to itself.
	/// </summary>
	public void SendTraderSwing(TraderSwingMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_sender.SendToAll(
				_session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId).Select(m => m.SteamId),
				NetMsg.TraderSwing, msg, reliable: true);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.TraderSwing, msg);
		}
	}

	/// <summary>A hostile trader swing arrived (report or relay) — the receiver replays the animation on its same-position trader.</summary>
	public event Action<ulong, TraderSwingMsg>? TraderSwingReceived;

	public void FireTraderSwingReceived(ulong sender, TraderSwingMsg msg) =>
		TraderSwingReceived?.Invoke(sender, msg);
}

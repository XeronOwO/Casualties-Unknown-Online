using System;
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
}

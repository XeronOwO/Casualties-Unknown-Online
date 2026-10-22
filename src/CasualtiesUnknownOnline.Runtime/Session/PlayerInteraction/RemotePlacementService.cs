using System.Collections.Generic;
using System.Text;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The give gesture's answer-first custody (the user's 2026-10-22 ruling): before the host records anything, the
/// RECEIVING client resolves where its own rules would put the item, and the host commits the destination that
/// answer names. A refusal writes nothing, so an item the destination cannot take never leaves the giver.
/// <para>
/// The host half owns one pending question per give (the requester, the intent and a deadline). The receiving
/// half asks the adapter's own <see cref="IRemotePlacementResolver"/> — the same rules the placement runs — and
/// answers. When the host IS the receiver there is no round trip: the resolver answers in-process.
/// </para>
/// <para>
/// The answer is the placement: the host passes the ANSWERED slot or container to
/// <see cref="PlayerInventoryTakeService.HandleRemoteGiveToBody"/> in its exact mode, so the ledger row states
/// what the receiving client resolved, never the giver's request. A destination that changed between the answer
/// and the commit (the receiver picked something up in that window) is refused by the host before any custody
/// moves and answered to the giver with the reason; the deeper race — the receiver's LIVE body differing from
/// the snapshot the host just wrote — is the ticket's residual and is implemented by the failure report
/// (<c>SendFailed</c>/<c>HandleFailed</c>) plus the return service, not claimed away here.
/// </para>
/// </summary>
internal sealed class RemotePlacementService(
	ISessionControl session,
	PacketSender sender,
	PlayerInventoryTakeService take,
	GivenItemReturnService givenReturn,
	IRemotePlacementResolver? resolver,
	ITimeSource time,
	ILogger log) : ISessionReset
{
	/// <summary>How long the host waits for the receiving client's answer before refusing the give by name.</summary>
	internal const long AnswerTimeoutMs = 15000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerInventoryTakeService _take = take;
	private readonly GivenItemReturnService _givenReturn = givenReturn;
	private readonly IRemotePlacementResolver? _resolver = resolver;
	private readonly ITimeSource _time = time;
	private readonly ILogger _log = log;
	private readonly Dictionary<ulong, PendingGive> _pending = [];
	private readonly Dictionary<ulong, CommittedPlacement> _committed = [];
	private ulong _nextQueryId = 1;

	/// <summary>Host only: one validated give intent is waiting on the receiving client's placement answer.</summary>
	private sealed record PendingGive(ulong Giver, RemoteInventoryIntentMsg Msg, long DeadlineMs);

	/// <summary>Host only: one committed give, kept so the receiving client's failure report can be matched to it.</summary>
	private sealed record CommittedPlacement(ulong Giver, ulong Receiver, long AtMs);

	/// <summary>How long a committed placement stays matchable by a failure report; the report follows the transfer immediately.</summary>
	internal const long CommittedPlacementTtlMs = 60000;

	/// <summary>Host only: ask the receiving client where this give would land, or answer in-process when the host is the receiver.</summary>
	internal void BeginGive(ulong giver, RemoteInventoryIntentMsg msg, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		PruneExpired();

		var receiver = msg.TargetBodySteamId;
		if (receiver == _session.LocalSteamId)
		{
			ResolveHere(giver, msg, item);
			return;
		}

		var queryId = _nextQueryId++;
		_pending[queryId] = new PendingGive(giver, msg, _time.NowMs + AnswerTimeoutMs);
		_sender.Send(receiver, NetMsg.PlayerPlacementQuery, new PlayerPlacementQueryMsg
		{
			QueryId = queryId,
			Item = item,
			RequestedSlot = msg.TargetSlotIndex,
			RequestedContainerInstanceId = msg.TargetContainerInstanceId,
		});
		_log.LogInformation(
			"[Placement] asked {Receiver} where item {ItemId} (id {InstanceId}) can go (query {Query}, slot {Slot}, container {Container}).",
			receiver, item.ItemId, item.InstanceId, queryId, msg.TargetSlotIndex, msg.TargetContainerInstanceId);
	}

	/// <summary>Host only: the receiving client answered a placement question — commit the ANSWERED destination or refuse the giver.</summary>
	internal void HandleAnswer(ulong sender, PlayerPlacementAnswerMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_pending.TryGetValue(msg.QueryId, out var pending))
		{
			_log.LogDebug("[Placement] answer to query {Query} from {Sender} has no pending give (already answered, timed out or from a dead session).",
				msg.QueryId, sender);
			return;
		}

		// The answer is a claim about the destination body's own body: it is only worth reading from THAT client.
		// The sender is checked BEFORE the question is consumed, so a stray answer cannot abort another pair's
		// give (a guessed query id would otherwise remove the real pending entry).
		if (sender != pending.Msg.TargetBodySteamId)
		{
			_log.LogWarning("[Placement] dropped a placement answer for query {Query} from {Sender}: the destination body is {Receiver}.",
				msg.QueryId, sender, pending.Msg.TargetBodySteamId);
			return;
		}

		_pending.Remove(msg.QueryId);

		if (_time.NowMs > pending.DeadlineMs)
		{
			RefuseGiver(pending.Giver, pending.Msg, "the destination body did not answer the placement question in time, so nothing moved.");
			return;
		}

		if (!msg.Accepted)
		{
			RefuseGiver(pending.Giver, pending.Msg, string.IsNullOrWhiteSpace(msg.Reason) ? "the destination body refused the placement." : msg.Reason);
			return;
		}

		// An "accepted" answer must NAME its destination: a slot index or a container. One that names neither is
		// malformed and is refused by name rather than committed as "slot -1".
		if (msg.Slot < 0 && msg.ContainerInstanceId == 0)
		{
			RefuseGiver(pending.Giver, pending.Msg, "the destination body accepted the placement without naming a slot or a container.");
			return;
		}

		Commit(pending.Giver, pending.Msg, new RemotePlacementResolution(true, msg.Slot, msg.ContainerInstanceId, string.Empty));
	}

	/// <summary>Receiving client: the host asks where this give would land — answer with this client's own rules.</summary>
	internal void HandleQuery(ulong sender, PlayerPlacementQueryMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		// The wire direction only proves the local role: a placement question is the host's, and a frame from
		// anyone else is a claim about this body.
		if (sender != _session.HostSteamId)
		{
			_log.LogWarning("[Placement] dropped a placement question from {Sender}: this session's host is {Host}.", sender, _session.HostSteamId);
			return;
		}

		var answer = _resolver is null
			? RemotePlacementResolution.Refused("this client has no game adapter, so it cannot answer where an item would land.")
			: _resolver.Resolve(msg.Item, msg.RequestedSlot, msg.RequestedContainerInstanceId);
		_sender.Send(_session.HostSteamId, NetMsg.PlayerPlacementAnswer, new PlayerPlacementAnswerMsg
		{
			QueryId = msg.QueryId,
			Accepted = answer.Accepted,
			Slot = answer.Slot,
			ContainerInstanceId = answer.ContainerId,
			Reason = ClampReason(answer.Reason),
		});
		_log.LogInformation("[Placement] answered query {Query} for item {ItemId} (id {InstanceId}): {Answer}",
			msg.QueryId, msg.Item.ItemId, msg.Item.InstanceId, Describe(answer));
	}

	/// <summary>Host only: a session boundary invalidates every question and every committed placement it could still receive a failure for.</summary>
	public void ResetSessionState()
	{
		if (_pending.Count > 0)
		{
			_log.LogInformation("[Placement] dropped {Count} unanswered placement question(s) at the session boundary.", _pending.Count);
		}

		_pending.Clear();
		_committed.Clear();
	}

	/// <summary>The host's own body is the receiver: its resolver answers in-process, with no round trip.</summary>
	private void ResolveHere(ulong giver, RemoteInventoryIntentMsg msg, CharacterItemMsg item)
	{
		var answer = _resolver is null
			? RemotePlacementResolution.Refused("this client has no game adapter, so it cannot resolve the placement.")
			: _resolver.Resolve(item, msg.TargetSlotIndex, msg.TargetContainerInstanceId);
		_log.LogInformation("[Placement] resolved item {ItemId} (id {InstanceId}) on the host's own body: {Answer}.",
			item.ItemId, item.InstanceId, Describe(answer));
		if (!answer.Accepted)
		{
			RefuseGiver(giver, msg, answer.Reason);
			return;
		}

		Commit(giver, msg, answer);
	}

	/// <summary>Commit the answer the receiving client gave; the host's exact mode refuses a destination that changed meanwhile.</summary>
	private void Commit(ulong giver, RemoteInventoryIntentMsg msg, RemotePlacementResolution answer)
	{
		// The receiving client may still find its LIVE body unable to take the answered destination (it changed
		// after the answer). The pair is recorded BEFORE the commit publishes the transfer: when the host is the
		// receiving body, its own apply runs synchronously inside the commit and its failure report must find this
		// entry already there.
		PruneCommitted();
		_committed[msg.ItemInstanceId] = new CommittedPlacement(giver, msg.TargetBodySteamId, _time.NowMs);
		var refusal = _take.HandleRemoteGiveToBody(giver, msg.TargetBodySteamId, msg.ItemInstanceId, answer.Slot, answer.ContainerId, exactDestination: true);
		if (refusal is not null)
		{
			_committed.Remove(msg.ItemInstanceId);
			RefuseGiver(giver, msg, refusal);
		}
	}

	/// <summary>Per frame: a question nobody answered is refused at its deadline, and a committed placement stops being matchable.</summary>
	internal void Tick()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		PruneExpired();
		PruneCommitted();
	}

	/// <summary>Receiving client: report that the answered destination is gone, so the host can return the row.</summary>
	internal void SendFailed(ulong itemInstanceId, string reason)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var msg = new PlayerPlacementFailedMsg { ItemInstanceId = itemInstanceId, Reason = ClampReason(reason) };
		if (_session.Role == SessionRole.Host)
		{
			HandleFailed(_session.LocalSteamId, msg);
			return;
		}

		_log.LogWarning("[Placement] answered destination for item {ItemId} is no longer available: {Reason} — asking the host to return it.",
			itemInstanceId, reason);
		_sender.Send(_session.HostSteamId, NetMsg.PlayerPlacementFailed, msg);
	}

	/// <summary>Host only: the receiving client cannot take the answered destination — move the row back to the giver and publish the return.</summary>
	internal void HandleFailed(ulong sender, PlayerPlacementFailedMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_committed.TryGetValue(msg.ItemInstanceId, out var committed))
		{
			_log.LogWarning("[Placement] dropped a placement failure for item {ItemId} from {Sender}: no give this host committed is waiting on it.",
				msg.ItemInstanceId, sender);
			return;
		}

		if (committed.Receiver != sender)
		{
			_log.LogWarning("[Placement] dropped a placement failure for item {ItemId}: {Sender} is not the receiving body {Receiver}.",
				msg.ItemInstanceId, sender, committed.Receiver);
			return;
		}

		_committed.Remove(msg.ItemInstanceId);
		var refusal = _givenReturn.Return(sender, committed.Giver, msg.ItemInstanceId);
		if (refusal is not null)
		{
			_log.LogError("[Placement] could not return item {ItemId} to {Giver} after {Receiver}'s failed placement: {Reason}",
				msg.ItemInstanceId, committed.Giver, sender, refusal);
			return;
		}

		_log.LogInformation("[Placement] returned item {ItemId} to {Giver}: {Receiver} could not take the answered destination ({Reason}).",
			msg.ItemInstanceId, committed.Giver, sender, ClampReason(msg.Reason));
	}

	private void PruneCommitted()
	{
		if (_committed.Count == 0)
		{
			return;
		}

		var now = _time.NowMs;
		List<ulong>? expired = null;
		foreach (var pair in _committed)
		{
			if (now - pair.Value.AtMs > CommittedPlacementTtlMs)
			{
				(expired ??= []).Add(pair.Key);
			}
		}

		if (expired is null)
		{
			return;
		}

		foreach (var itemId in expired)
		{
			_committed.Remove(itemId);
		}
	}

	/// <summary>Answer the giver with the receiving side's own sentence; a host giver already printed its line.</summary>
	private void RefuseGiver(ulong giver, RemoteInventoryIntentMsg msg, string reason)
	{
		var clamped = ClampReason(reason);
		_log.LogWarning("[RemoteIntent] refused {Kind}: {Reason}", msg.Kind, clamped);
		if (giver == _session.LocalSteamId)
		{
			return;
		}

		_sender.Send(giver, NetMsg.InventoryIntentRefused, new PlayerInventoryIntentRefusedMsg
		{
			Kind = (int)msg.Kind,
			ItemInstanceId = msg.ItemInstanceId,
			Reason = clamped,
		});
	}

	/// <summary>
	/// The wire and the logs carry this string verbatim, so a member-authored reason is capped and stripped of
	/// control characters before it reaches another player's UI or the log (the chat row's shape-bound rule).
	/// </summary>
	internal static string ClampReason(string? reason)
	{
		const int maxLength = 200;
		if (string.IsNullOrWhiteSpace(reason))
		{
			return string.Empty;
		}

		var trimmed = reason!.Trim();
		var builder = new StringBuilder(trimmed.Length);
		foreach (var c in trimmed)
		{
			if (!char.IsControl(c))
			{
				builder.Append(c);
			}
		}

		var clean = builder.ToString();
		return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength) + "…";
	}

	private void PruneExpired()
	{
		if (_pending.Count == 0)
		{
			return;
		}

		var now = _time.NowMs;
		List<ulong>? expired = null;
		foreach (var pair in _pending)
		{
			if (now > pair.Value.DeadlineMs)
			{
				(expired ??= []).Add(pair.Key);
			}
		}

		if (expired is null)
		{
			return;
		}

		foreach (var queryId in expired)
		{
			if (_pending.TryGetValue(queryId, out var pending))
			{
				_pending.Remove(queryId);
				RefuseGiver(pending.Giver, pending.Msg, "the destination body did not answer the placement question in time, so nothing moved.");
			}
		}
	}

	private static string Describe(RemotePlacementResolution answer) => answer switch
	{
		{ Accepted: true, ContainerId: not 0 } => $"inside container {answer.ContainerId}",
		{ Accepted: true, Slot: >= 0 } => $"slot {answer.Slot}",
		{ Accepted: true } => "accepted without a named destination",
		_ => $"refused: {answer.Reason}",
	};
}

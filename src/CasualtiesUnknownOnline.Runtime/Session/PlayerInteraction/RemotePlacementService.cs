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
/// moves and answered to the giver with the reason.
/// </para>
/// <para>
/// The deeper race — the receiver's LIVE body differing from the snapshot the host just wrote — is closed by the
/// placement OUTCOME (<c>SendOutcome</c>/<c>HandleOutcome</c>): the receiving client reports the place its own
/// placement actually used, and the host compares that with the row it wrote. A match settles the give and writes
/// nothing; a different place, or none at all, is refused by name and the return service moves the row back to
/// the giver. The comparison is the point: the host never writes durable state from the receiver's word alone, it
/// either confirms the fact it wrote or reverses its own commit.
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

	/// <summary>Host only: one committed give, kept so the receiving client's outcome report can be compared with the place this host wrote.</summary>
	private sealed record CommittedPlacement(ulong Giver, ulong Receiver, long AtMs, int Slot, ulong ContainerId);

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
		// after the answer). The pair — and the place this host is about to write — is recorded BEFORE the commit
		// publishes the transfer: when the host is the receiving body, its own apply runs synchronously inside the
		// commit and its outcome report must find this entry already there.
		PruneCommitted();
		_committed[msg.ItemInstanceId] = new CommittedPlacement(giver, msg.TargetBodySteamId, _time.NowMs, answer.Slot, answer.ContainerId);
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

	/// <summary>
	/// Receiving client: report what this client's placement really did — the place it used, or that it placed
	/// nothing — so the host can compare the outcome with the row it wrote. One outcome per give.
	/// </summary>
	internal void SendOutcome(ulong itemInstanceId, bool applied, int slot, ulong containerInstanceId, string reason)
	{
		if (!_session.SessionActive || !_session.LocalInWorld || itemInstanceId == 0)
		{
			return;
		}

		var msg = new PlayerPlacementOutcomeMsg
		{
			ItemInstanceId = itemInstanceId,
			Applied = applied,
			Slot = applied ? slot : -1,
			ContainerInstanceId = applied ? containerInstanceId : 0,
			Reason = ClampReason(reason),
		};
		if (_session.Role == SessionRole.Host)
		{
			HandleOutcome(_session.LocalSteamId, msg);
			return;
		}

		_log.LogInformation("[Placement] item {ItemId}: {Outcome} — reporting it to the host.", itemInstanceId, DescribeOutcome(msg));
		_sender.Send(_session.HostSteamId, NetMsg.PlayerPlacementOutcome, msg);
	}

	/// <summary>
	/// Host only: the receiving client reported what its placement did. The place it names is compared with the
	/// row this host wrote from the answer: a match settles the give and writes nothing, and anything else — a
	/// different place, an applied outcome that names no place, or nothing applied — is refused by name and the
	/// row goes back to the giver. A report matching no committed give moves nothing at all.
	/// </summary>
	internal void HandleOutcome(ulong sender, PlayerPlacementOutcomeMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (!_committed.TryGetValue(msg.ItemInstanceId, out var committed))
		{
			_log.LogWarning("[Placement] refused the placement outcome for item {ItemId} from {Sender}: no give this host committed is waiting on it, so nothing moved.",
				msg.ItemInstanceId, sender);
			return;
		}

		if (committed.Receiver != sender)
		{
			_log.LogWarning("[Placement] refused the placement outcome for item {ItemId} from {Sender}: this host wrote that row to the receiving body {Receiver}, so nothing moved.",
				msg.ItemInstanceId, sender, committed.Receiver);
			return;
		}

		// One outcome per committed give: the record is taken before anything moves, so a repeated or late report
		// cannot return the same item twice.
		_committed.Remove(msg.ItemInstanceId);

		if (msg.Applied && PlaceMatches(committed, msg))
		{
			_log.LogInformation("[Placement] item {ItemId} is where this host wrote it ({Place}): the placement outcome matches, nothing moved.",
				msg.ItemInstanceId, DescribePlace(committed.Slot, committed.ContainerId));
			return;
		}

		// The reason is member-authored, so it is capped and stripped of control characters HERE as well as on the
		// sending side: the host's log is the one that must not take a member's control characters, and a sender
		// that skips its own clamp must not be able to write them into it.
		ReturnToGiver(msg.ItemInstanceId, committed, sender, DescribeMismatch(committed, msg, ClampReason(msg.Reason)));
	}

	/// <summary>Host only: the outcome contradicts the row — refuse it by name and publish the return to the giver.</summary>
	private void ReturnToGiver(ulong itemInstanceId, CommittedPlacement committed, ulong receiver, string why)
	{
		_log.LogWarning("[Placement] refused the placement outcome from {Receiver} for item {ItemId}: {Why} — returning the row to {Giver}.",
			receiver, itemInstanceId, why, committed.Giver);
		var refusal = _givenReturn.Return(receiver, committed.Giver, itemInstanceId);
		if (refusal is not null)
		{
			_log.LogError("[Placement] could not return item {ItemId} to {Giver} after {Receiver}'s refused placement outcome: {Reason}",
				itemInstanceId, committed.Giver, receiver, refusal);
			return;
		}

		_log.LogInformation("[Placement] returned item {ItemId} to {Giver} after {Receiver}'s refused placement outcome.",
			itemInstanceId, committed.Giver, receiver);
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

	/// <summary>The one comparison: does the place the receiver used equal the place this host wrote?</summary>
	private static bool PlaceMatches(CommittedPlacement written, PlayerPlacementOutcomeMsg msg) =>
		msg.ContainerInstanceId != 0
			? written.ContainerId == msg.ContainerInstanceId
			: written.ContainerId == 0 && written.Slot == msg.Slot;

	private static string DescribePlace(int slot, ulong containerId) =>
		containerId != 0 ? $"container {containerId}" : slot >= 0 ? $"slot {slot}" : "no place";

	private static string DescribeMismatch(CommittedPlacement written, PlayerPlacementOutcomeMsg msg, string reason)
	{
		if (!msg.Applied)
		{
			return string.IsNullOrWhiteSpace(reason) ? "it placed nothing" : $"it placed nothing ({reason})";
		}

		return msg.Slot < 0 && msg.ContainerInstanceId == 0
			? "it reports an applied placement that names neither a slot nor a container"
			: $"it placed the item at {DescribePlace(msg.Slot, msg.ContainerInstanceId)} instead of {DescribePlace(written.Slot, written.ContainerId)}";
	}

	private static string DescribeOutcome(PlayerPlacementOutcomeMsg msg)
	{
		if (msg.Applied)
		{
			return $"placed it at {DescribePlace(msg.Slot, msg.ContainerInstanceId)}";
		}

		return string.IsNullOrWhiteSpace(msg.Reason) ? "placed nothing" : $"placed nothing ({msg.Reason})";
	}
}

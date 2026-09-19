using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The target-body verdict seam of the medical operation family. The host
/// validates what it owns (participants, the operator's item facts, the claims),
/// PARKS the start request here and asks the TARGET's client; the target runs the
/// target half of the preconditions (<see cref="MedicalTargetBodyValidator"/>)
/// against its OWN live body and answers, and the parked request continues on that
/// answer — commit on an accept, the target's own reason on a reject.
/// <para>
/// The liveness bound is not a judgment parameter: it only abandons a request the
/// target never answered, so a peer that is silent leaves the operator with an
/// explicit refusal instead of a start that waits forever. No measured latency,
/// window guess or tolerance enters any verdict.
/// </para>
/// </summary>
internal sealed class MedicalTargetBodyGate(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess access,
	ILocalCharacterCapture localCapture,
	ITimeSource time,
	ILogger log)
{
	/// <summary>
	/// How long a parked request waits for the target's answer before it is
	/// abandoned. It exists so an unanswered request cannot leave the operator's UI
	/// hanging; it never decides what the target's body allows, and it is deliberately
	/// generous compared with a round trip (a slow-but-answering target must not be
	/// treated as a refused one).
	/// </summary>
	private const int AnswerTimeoutMs = 3000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = access;
	private readonly ILocalCharacterCapture _localCapture = localCapture;
	private readonly ITimeSource _time = time;
	private readonly ILogger _log = log;

	private readonly Dictionary<ulong, PendingCheck> _pending = [];
	private ulong _nextRequestId;

	/// <summary>The host's read-only view of parked start requests (the cut policy's probe reads open sessions, not these — a parked request holds no claim and mutates nothing).</summary>
	internal int PendingCount => _pending.Count;

	/// <summary>The target client's answer: whether its own body allows the operation, the reason when it does not, and the live piece count the shrapnel family needs.</summary>
	internal readonly record struct TargetBodyVerdict(bool Accepted, string Reason, int ShrapnelCount);

	private readonly record struct PendingCheck(ulong Operator, ulong Target, long AskedMs, Action<TargetBodyVerdict> OnVerified);

	/// <summary>
	/// Host side: park a start request whose only open question is the target's body.
	/// When the host IS the target the verdict is taken here and now — the host's own
	/// client is the client that owns that body, so no message is needed for it.
	/// </summary>
	internal void Begin(
		ulong operatorId,
		ulong target,
		int limbIndex,
		MedicalOperationKind kind,
		Action<TargetBodyVerdict> onVerified)
	{
		if (target == _session.LocalSteamId)
		{
			onVerified(VerifyLocal(limbIndex, kind));
			return;
		}

		var requestId = ++_nextRequestId;
		_pending[requestId] = new PendingCheck(operatorId, target, _time.NowMs, onVerified);
		_log.LogDebug(
			"[MedicalTargetCheck] asking {Target} whether its body allows {Kind} on limb {Limb} (request {RequestId}, operator {Operator}).",
			target, kind, limbIndex, requestId, operatorId);
		_sender.Send(target, NetMsg.MedicalOperationTargetCheckRequest, new MedicalOperationTargetCheckRequestMsg
		{
			RequestId = requestId,
			OperatorSteamId = operatorId,
			LimbIndex = limbIndex,
			Kind = kind,
		});
	}

	/// <summary>Target side: answer the host's check from THIS client's own body.</summary>
	internal void HandleRequest(ulong senderId, MedicalOperationTargetCheckRequestMsg msg)
	{
		if (_session.Role == SessionRole.Host || senderId != _session.HostSteamId)
		{
			_log.LogWarning("[MedicalTargetCheck] refused a body check from {Sender}: only the host asks.", senderId);
			return;
		}

		var verdict = VerifyLocal(msg.LimbIndex, msg.Kind);
		_log.LogInformation(
			"[MedicalTargetCheck] answered request {RequestId} for {Operator} ({Kind} limb {Limb}): {Verdict}.",
			msg.RequestId, msg.OperatorSteamId, msg.Kind, msg.LimbIndex,
			verdict.Accepted ? "this body allows it" : verdict.Reason);
		_sender.Send(_session.HostSteamId, NetMsg.MedicalOperationTargetCheckAnswer, new MedicalOperationTargetCheckAnswerMsg
		{
			RequestId = msg.RequestId,
			Accepted = verdict.Accepted,
			RejectReason = verdict.Accepted ? null : verdict.Reason,
			ShrapnelCount = verdict.ShrapnelCount,
		});
	}

	/// <summary>Host side: the target's answer resolves the parked request it names.</summary>
	internal void HandleAnswer(ulong senderId, MedicalOperationTargetCheckAnswerMsg msg)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		if (!_pending.TryGetValue(msg.RequestId, out var pending))
		{
			_log.LogWarning("[MedicalTargetCheck] answer {RequestId} from {Sender} matches no parked request.", msg.RequestId, senderId);
			return;
		}

		if (pending.Target != senderId)
		{
			_log.LogWarning(
				"[MedicalTargetCheck] answer {RequestId} came from {Sender}, not from the asked target {Target}; dropped.",
				msg.RequestId, senderId, pending.Target);
			return;
		}

		_pending.Remove(msg.RequestId);
		var verdict = new TargetBodyVerdict(msg.Accepted, msg.RejectReason ?? "", msg.ShrapnelCount);
		_log.LogInformation(
			"[MedicalTargetCheck] request {RequestId} answered by {Target}: {Verdict}.",
			msg.RequestId, senderId, verdict.Accepted ? "this body allows it" : verdict.Reason);
		pending.OnVerified(verdict);
	}

	/// <summary>Host side: abandon requests whose target has not answered within the liveness bound.</summary>
	internal void Tick()
	{
		if (_session.Role != SessionRole.Host || _pending.Count == 0)
		{
			return;
		}

		var now = _time.NowMs;
		foreach (var entry in _pending.ToList())
		{
			if (now - entry.Value.AskedMs < AnswerTimeoutMs)
			{
				continue;
			}

			_pending.Remove(entry.Key);
			_log.LogWarning(
				"[MedicalTargetCheck] request {RequestId} to {Target} was not answered within {Timeout} ms; the start is refused.",
				entry.Key, entry.Value.Target, AnswerTimeoutMs);
			entry.Value.OnVerified(new TargetBodyVerdict(false, "Target did not answer the body check.", 0));
		}
	}

	/// <summary>Host side: a member left — a parked request it was part of cannot resolve any more.</summary>
	internal void OnMemberRemoved(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || _pending.Count == 0)
		{
			return;
		}

		foreach (var entry in _pending.ToList())
		{
			if (entry.Value.Target == steamId)
			{
				_pending.Remove(entry.Key);
				_log.LogWarning(
					"[MedicalTargetCheck] target {Target} left before answering request {RequestId}; the start is refused.",
					steamId, entry.Key);
				entry.Value.OnVerified(new TargetBodyVerdict(false, "Target left before answering the body check.", 0));
			}
			else if (entry.Value.Operator == steamId)
			{
				_pending.Remove(entry.Key);
				_log.LogInformation("[MedicalTargetCheck] operator {Operator} left; request {RequestId} is dropped.", steamId, entry.Key);
			}
		}
	}

	/// <summary>Host side: session teardown drops every parked request (the sessions they belonged to are gone too).</summary>
	internal void Clear() => _pending.Clear();

	/// <summary>
	/// Run the target half against the LOCAL body. The answer always comes from the client
	/// that owns the body; what differs between compositions is where the body is read from.
	/// A composition WITH a live capture is asked for the body and nothing else — a null there
	/// is that client's own answer "my body cannot be read right now", and it is refused
	/// rather than replaced by a stored snapshot, which on a guest is the character it entered
	/// the world with. A composition WITHOUT a live capture has no live source at all, so the
	/// local side's own stored snapshot is the best statement about its own body that exists.
	/// </summary>
	private TargetBodyVerdict VerifyLocal(int limbIndex, MedicalOperationKind kind)
	{
		var data = _localCapture.CaptureLocal();
		if (data is null && !_localCapture.HasLiveCapture)
		{
			data = _access.GetCharacterData(_session.LocalSteamId);
		}

		if (data is null)
		{
			// Either the live body could not be read, or there is no stored snapshot either.
			// Both mean this client cannot answer for its body right now, and an unanswerable
			// target is refused instead of being judged by another client on its behalf.
			return new TargetBodyVerdict(false, "Target body is unavailable.", 0);
		}

		return MedicalTargetBodyValidator.TryValidate(data, kind, limbIndex, out var shrapnelCount, out var reason)
			? new TargetBodyVerdict(true, "", shrapnelCount)
			: new TargetBodyVerdict(false, reason, 0);
	}
}

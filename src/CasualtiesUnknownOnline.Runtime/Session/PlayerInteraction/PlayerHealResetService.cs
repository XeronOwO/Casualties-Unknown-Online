using System;
using System.Collections.Generic;
using System.Text;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The console cheat verb <c>/heal</c>'s mechanics: an immediate, item-free FULL reset of the
/// selected players' state, carried out on each target's OWN client.
///
/// <para>
/// A reset is a mutation of the target's body, so by the judgment-ownership rule the target's own
/// client is where it runs and the host never writes another player's body
/// (<c>docs/en/internals/judgment-ownership.md</c>). The host's job is the permission (its cheat
/// switch and roster, judged for every request that arrives), the roster of targets it can place
/// in this world, and the instruction: a HOST target resets locally at once, a MEMBER target
/// receives <see cref="HealResetInstructionMsg"/> and applies the reset to its own live body. The
/// member then reports the RESULT twice: the ordinary character snapshot, and the authority event
/// <see cref="HealResetAppliedMsg"/> that carries the host-owned continuous values the snapshot may
/// not state. The host writes that event through the character data's authority path, and this
/// service is where the event is judged: its owner must be the sender, and it must answer an
/// instruction the host really issued to that member.
/// </para>
///
/// <para>
/// The two-sided check is the cheat family's, verbatim: a member's own client refuses a closed
/// switch or a roster that does not name it BEFORE anything is sent (the console's
/// <c>CheatVerbGate</c>), and the host judges the same two rulings again for every request that
/// arrives — the client's copy is a prediction, never the verdict. The answer names the target and
/// the outcome (revived, limbs restored, hollow cleared) or the refusal and its ground; it never
/// says a request was sent when the host already refused it.
/// </para>
///
/// <para>
/// The outstanding-instruction table is the authority anchor of the applied event, not a latency
/// judgement: every instruction gets an id, the target echoes it, and one instruction resolves at
/// most one result. The liveness bound only abandons an instruction whose answer never arrives (a
/// target with no body, a lost client), so a stale entry cannot accept a later or forged result;
/// nothing measured about the network decides whether a reset is accepted.
/// </para>
/// </summary>
internal sealed class PlayerHealResetService : IHealResetControl, ISessionReset, IDisposable
{
	/// <summary>A selector cannot name more bodies than a session has members; the cap refuses a hand-built frame before it fans out.</summary>
	internal const int MaxTargets = 32;

	/// <summary>
	/// How long an instruction may wait for its applied event before it is abandoned. It drops an
	/// answer that has not arrived within the bound; the event normally arrives in the same command's
	/// round trip, and no latency value is ever an input to whether a result is accepted.
	/// </summary>
	private const int InstructionTimeoutMs = 30_000;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ICharacterDataControl _characters;
	private readonly PlayerCharacterAccess _access;
	private readonly ILocalBodyReset _localReset;
	private readonly IMemberCommandRules _memberRules;
	private readonly ITimeSource _time;
	private readonly ILogger _log;

	/// <summary>The instructions this host issued to members and has not seen a result for, keyed by target — one outstanding instruction per body.</summary>
	private readonly Dictionary<ulong, PendingReset> _pending = [];

	/// <summary>Test/observability seam (InternalsVisibleTo): instructions this host awaits a result for.</summary>
	internal int PendingInstructionCount => _pending.Count;

	private ulong _nextInstructionId;

	public PlayerHealResetService(
		ISessionControl session,
		PacketSender sender,
		ICharacterDataControl characters,
		ILocalBodyReset localReset,
		IMemberCommandRules memberRules,
		ITimeSource time,
		ILogger<PlayerHealResetService> log)
	{
		_session = session;
		_sender = sender;
		_characters = characters;
		_access = new PlayerCharacterAccess(session, characters);
		_localReset = localReset;
		_memberRules = memberRules;
		_time = time;
		_log = log;
		session.SessionEnded += ResetSessionState;
	}

	private readonly record struct PendingReset(ulong InstructionId, long IssuedMs);

	/// <inheritdoc />
	public event Action<HealResetNotice>? Applied;

	/// <inheritdoc />
	public event Action<HealResetResultMsg>? Answered;

	/// <inheritdoc />
	public string? Reset(IReadOnlyList<ulong> targetSteamIds)
	{
		if (!_session.SessionActive)
		{
			return "Refused: no active session — a cheat verb needs one: the host's own permission is the online switch, and a member's is the host's grant for the session.";
		}

		if (_session.Role == SessionRole.Guest)
		{
			var targets = DistinctTargets(targetSteamIds);
			_sender.Send(_session.HostSteamId, NetMsg.HealResetRequest, new HealResetRequestMsg { TargetSteamIds = targets });
			_log.LogInformation("[HealReset] {Actor} requested a full reset of {Count} player(s): {Targets}.",
				_session.LocalSteamId, targets.Count, string.Join(", ", targets));
			return null;
		}

		return Execute(_session.LocalSteamId, targetSteamIds).Answer;
	}

	/// <inheritdoc />
	public void HandleRequest(ulong sender, HealResetRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_memberRules.CheatVerbsOpenToMembers)
		{
			RefuseRequest(sender, msg, "the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run /heal.");
			return;
		}

		if (!_memberRules.AllowsCheatVerb(sender))
		{
			RefuseRequest(sender, msg, "the host's cheat roster does not name you, so /heal cannot land. The host grants it with /op.");
			return;
		}

		if (msg.TargetSteamIds.Count > MaxTargets)
		{
			RefuseRequest(sender, msg, $"the request names {msg.TargetSteamIds.Count} targets; /heal accepts at most {MaxTargets}.");
			return;
		}

		if (!_session.LocalInWorld)
		{
			RefuseRequest(sender, msg, "this host is not in a world, so it has no body to reset and no world to reach the named targets in.");
			return;
		}

		var outcome = Execute(sender, msg.TargetSteamIds);
		_sender.Send(sender, NetMsg.HealResetResult, new HealResetResultMsg
		{
			Applied = outcome.Applied,
			Answer = outcome.Answer,
		});
	}

	/// <inheritdoc />
	public void HandleInstruction(ulong sender, HealResetInstructionMsg msg)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			_log.LogWarning("[HealReset] a reset instruction arrived while this process is {Role} — dropped.", _session.Role);
			return;
		}

		var outcome = _localReset.Reset(msg.InstructionId);
		var text = DescribeNotice(msg.OperatorSteamId, outcome);
		if (outcome.Applied)
		{
			_log.LogInformation("[HealReset] the host's instruction from {Operator} (instruction {InstructionId}) reset this body: wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}.",
				msg.OperatorSteamId, msg.InstructionId, outcome.WasDead, outcome.RestoredLimbs, outcome.HollowCleared);
		}
		else
		{
			_log.LogWarning("[HealReset] the host's instruction from {Operator} (instruction {InstructionId}) could not be applied here: {Reason}",
				msg.OperatorSteamId, msg.InstructionId, outcome.Refusal);
		}

		Applied?.Invoke(new HealResetNotice(msg.OperatorSteamId, outcome, text));
	}

	/// <inheritdoc />
	public void HandleApplied(ulong sender, HealResetAppliedMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			_log.LogWarning("[HealReset] a reset result arrived while this process is {Role} — dropped.", _session.Role);
			return;
		}

		// The payload names a body; only the transport sender may speak for its own. The limb event's
		// handler carries the same rule for the same reason: a member ID in the payload is a claim.
		if (msg.OwnerSteamId != sender)
		{
			_log.LogWarning("[HealReset] dropped a reset result from {Sender} claiming owner {Owner}.", sender, msg.OwnerSteamId);
			return;
		}

		PruneExpired(_time.NowMs);
		if (!_pending.TryGetValue(sender, out var pending))
		{
			// No instruction this host issued is waiting for this member's result: the frame is
			// unsolicited. Writing it would let any member heal itself on demand, which is the cheat
			// permission's decision, not the wire's.
			_log.LogWarning("[HealReset] dropped an unsolicited reset result from {Sender} (instruction {InstructionId}): this host has no outstanding instruction for that player.",
				sender, msg.InstructionId);
			return;
		}

		if (pending.InstructionId != msg.InstructionId)
		{
			// A stale result (an older instruction answered after a newer one was issued) or a forged
			// id. Either way it is not the instruction that is outstanding, so it writes nothing.
			_log.LogWarning("[HealReset] dropped {Sender}'s reset result for instruction {AnsweredId}; the outstanding instruction is {OutstandingId}.",
				sender, msg.InstructionId, pending.InstructionId);
			return;
		}

		if (!_characters.ApplyHealResetEvent(msg))
		{
			_log.LogWarning("[HealReset] refused {Sender}'s reset result for instruction {InstructionId}; the previous snapshot stands and the instruction stays outstanding until it expires.",
				sender, msg.InstructionId);
			return;
		}

		_pending.Remove(sender);

		_log.LogInformation("[HealReset] {Sender} applied the reset for instruction {InstructionId}.",
			sender, msg.InstructionId);
	}

	/// <inheritdoc />
	public void FireAnswered(HealResetResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[HealReset] an answer arrived while this process is {Role} — dropped.", _session.Role);
			return;
		}

		Answered?.Invoke(msg);
	}

	/// <summary>
	/// The host's shared path for its own console and a member's request: reset the host's own body
	/// when the host is named, instruct every named member's client, and answer in one sentence.
	/// </summary>
	private (bool Applied, string Answer) Execute(ulong actor, IReadOnlyList<ulong> requested)
	{
		var targets = DistinctTargets(requested);
		if (targets.Count == 0)
		{
			return (false, "Refused: the request names no player.");
		}

		var nowMs = _time.NowMs;
		PruneExpired(nowMs);
		var applied = true;
		var parts = new List<string>(targets.Count);
		foreach (var target in targets)
		{
			if (target == _session.LocalSteamId)
			{
				if (!_session.LocalInWorld)
				{
					applied = false;
					parts.Add(RefusedFor(target, "this client is not in a world, so there is no body to reset."));
					continue;
				}

				var outcome = _localReset.Reset(null);
				applied &= outcome.Applied;
				parts.Add(DescribeLocal(target, outcome));
				_log.LogInformation("[HealReset] {Actor} reset the host's own body (wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}, applied={Applied}).",
					actor, outcome.WasDead, outcome.RestoredLimbs, outcome.HollowCleared, outcome.Applied);
				continue;
			}

			if (!_access.IsInWorld(target))
			{
				applied = false;
				parts.Add(RefusedFor(target, "not in this world (left, or no snapshot yet)."));
				_log.LogWarning("[HealReset] {Actor} named {Target}, which is not in this world — refused for that target.", actor, target);
				continue;
			}

			// The instruction id is the host's ticket for the applied event: the target echoes it, and
			// the write is accepted only while this is the outstanding instruction for that body.
			var instructionId = ++_nextInstructionId;
			var wasDead = _access.GetCharacterData(target)?.Health is { Alive: false };
			if (!_sender.TrySend(target, NetMsg.HealResetInstruction, new HealResetInstructionMsg
			{
				OperatorSteamId = actor,
				InstructionId = instructionId,
			}))
			{
				// The transport refused the frame locally: nothing was sent, so no result can answer
				// this instruction and the operator must not read "sent".
				applied = false;
				parts.Add(RefusedFor(target, "the transport did not accept the reset instruction, so nothing was sent."));
				_log.LogWarning("[HealReset] {Actor}'s instruction to {Target} (instruction {InstructionId}) was not accepted by the transport; no reset is pending.",
					actor, target, instructionId);
				continue;
			}

			_pending[target] = new PendingReset(instructionId, nowMs);
			parts.Add(DescribeRemote(target, wasDead));
			_log.LogInformation("[HealReset] {Actor} instructed {Target} to reset its own body (instruction {InstructionId}, wasDead={WasDead}).",
				actor, target, instructionId, wasDead);
		}

		return (applied, string.Join(" ", parts));
	}

	/// <summary>
	/// Abandon instructions whose result never arrived. It is the family's liveness bound: it can
	/// abandon a result the host did not see within the bound, and it never judges what the target's
	/// body may state — timing is not an input to acceptance, only to whether an answerless
	/// instruction stops waiting.
	/// </summary>
	private void PruneExpired(long nowMs)
	{
		if (_pending.Count == 0)
		{
			return;
		}

		List<ulong>? expired = null;
		foreach (var entry in _pending)
		{
			if (nowMs - entry.Value.IssuedMs > InstructionTimeoutMs)
			{
				(expired ??= []).Add(entry.Key);
			}
		}

		if (expired is null)
		{
			return;
		}

		foreach (var target in expired)
		{
			var instructionId = _pending[target].InstructionId;
			_pending.Remove(target);
			_log.LogInformation("[HealReset] instruction {InstructionId} to {Target} expired without an applied result.",
				instructionId, target);
		}
	}

	/// <summary>Session teardown: the instructions belong to the session that issued them (the ISessionReset contract).</summary>
	public void ResetSessionState()
	{
		if (_pending.Count > 0)
		{
			_log.LogInformation("[HealReset] dropping {Count} outstanding reset instruction(s): the session ended.", _pending.Count);
			_pending.Clear();
		}
	}

	public void Dispose() => _session.SessionEnded -= ResetSessionState;

	/// <summary>
	/// Deduplicate a request's target list, drop the zero id, and preserve the caller's order: a
	/// selector expansion and a hand-built frame both arrive here, and resetting one body twice
	/// would only produce two identical instructions.
	/// </summary>
	private static List<ulong> DistinctTargets(IReadOnlyList<ulong> requested)
	{
		var seen = new HashSet<ulong>();
		var targets = new List<ulong>(requested.Count);
		foreach (var target in requested)
		{
			if (target != 0 && seen.Add(target))
			{
				targets.Add(target);
			}
		}

		return targets;
	}

	private void RefuseRequest(ulong requester, HealResetRequestMsg request, string reason)
	{
		_log.LogWarning("[HealReset] request from {Requester} for {Count} target(s) refused: {Reason}",
			requester, request.TargetSteamIds.Count, reason);
		_sender.Send(requester, NetMsg.HealResetResult, new HealResetResultMsg
		{
			Applied = false,
			Answer = "Refused: " + reason,
		});
	}

	private static string DescribeLocal(ulong target, LocalBodyResetOutcome outcome)
	{
		if (!outcome.Applied)
		{
			return RefusedFor(target, outcome.Refusal);
		}

		var clauses = new List<string>(3);
		if (outcome.WasDead)
		{
			clauses.Add("revived from death");
		}

		if (outcome.RestoredLimbs > 0)
		{
			clauses.Add($"{outcome.RestoredLimbs} severed limb(s) restored");
		}

		if (outcome.HollowCleared)
		{
			clauses.Add("hollow cleared");
		}

		return $"Reset {target}: full heal applied{Clauses(clauses)}";
	}

	private static string DescribeRemote(ulong target, bool wasDead) =>
		$"Reset {target}: sent to that player's own client, which applies the full reset and re-reports the state"
		+ (wasDead ? " — it was dead, so it revives." : ".");

	private static string DescribeNotice(ulong operatorSteamId, LocalBodyResetOutcome outcome)
	{
		if (!outcome.Applied)
		{
			return $"A full reset from {operatorSteamId} could not be applied here: {outcome.Refusal}";
		}

		var clauses = new List<string>(3);
		if (outcome.WasDead)
		{
			clauses.Add("revived from death");
		}

		if (outcome.RestoredLimbs > 0)
		{
			clauses.Add($"{outcome.RestoredLimbs} severed limb(s) restored");
		}

		if (outcome.HollowCleared)
		{
			clauses.Add("hollow cleared");
		}

		return $"{operatorSteamId} fully reset your state{Clauses(clauses)}";
	}

	private static string RefusedFor(ulong target, string reason) => $"Refused for {target}: {reason}";

	/// <summary>The parenthetical clause list both sentences use, so the target's line and the operator's answer read the same.</summary>
	private static string Clauses(IReadOnlyList<string> clauses)
	{
		if (clauses.Count == 0)
		{
			return ".";
		}

		var builder = new StringBuilder(" (");
		for (var i = 0; i < clauses.Count; i++)
		{
			if (i > 0)
			{
				builder.Append("; ");
			}

			builder.Append(clauses[i]);
		}

		builder.Append(").");
		return builder.ToString();
	}
}

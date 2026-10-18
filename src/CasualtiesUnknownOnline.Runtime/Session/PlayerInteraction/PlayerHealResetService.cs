using System;
using System.Collections.Generic;
using System.Text;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
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
/// receives <see cref="HealResetInstructionMsg"/> and applies the reset to its own live body, then
/// re-reports the character snapshot the host and every peer already read.
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
/// </summary>
internal sealed class PlayerHealResetService(
	ISessionControl session,
	PacketSender sender,
	ICharacterDataControl characters,
	ILocalBodyReset localReset,
	IMemberCommandRules memberRules,
	ILogger<PlayerHealResetService> log) : IHealResetControl
{
	/// <summary>A selector cannot name more bodies than a session has members; the cap refuses a hand-built frame before it fans out.</summary>
	internal const int MaxTargets = 32;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _access = new(session, characters);
	private readonly ILocalBodyReset _localReset = localReset;
	private readonly IMemberCommandRules _memberRules = memberRules;
	private readonly ILogger _log = log;

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

		var outcome = _localReset.Reset();
		var text = DescribeNotice(msg.OperatorSteamId, outcome);
		if (outcome.Applied)
		{
			_log.LogInformation("[HealReset] the host's instruction from {Operator} reset this body: wasDead={WasDead}, restoredLimbs={Limbs}, hollowCleared={Hollow}.",
				msg.OperatorSteamId, outcome.WasDead, outcome.RestoredLimbs, outcome.HollowCleared);
		}
		else
		{
			_log.LogWarning("[HealReset] the host's instruction from {Operator} could not be applied here: {Reason}", msg.OperatorSteamId, outcome.Refusal);
		}

		Applied?.Invoke(new HealResetNotice(msg.OperatorSteamId, outcome, text));
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

				var outcome = _localReset.Reset();
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

			var wasDead = _access.GetCharacterData(target)?.Health is { Alive: false };
			_sender.Send(target, NetMsg.HealResetInstruction, new HealResetInstructionMsg { OperatorSteamId = actor });
			parts.Add(DescribeRemote(target, wasDead));
			_log.LogInformation("[HealReset] {Actor} instructed {Target} to reset its own body (wasDead={WasDead}).", actor, target, wasDead);
		}

		return (applied, string.Join(" ", parts));
	}

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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The console <c>/summon</c> verb's mechanics: create ONE world object — a world entity, an animal, or a loose
/// item entity — at a position, through the game's own creation call, and answer what was created.
///
/// <para>
/// The HOST is the creation authority, and the reason is the later joiner: only the host's own record is the one
/// the world-entry group carries (the accepted-creation table, the enemy snapshot's runtime facts, the item
/// snapshot), and a member's own creation would be a REPORT the host may reject, whose copy would then die on
/// that refusal. So a member's verb is a REQUEST: the permission (the host's cheat switch and roster), the
/// prefab, the type word and the state are judged HERE, against the host's own content and capture — never on the
/// member's word.
/// </para>
///
/// <para>
/// The position is the member's own fact, resolved on the member's scene before it is sent (its pointer, its
/// body) — the claim is a creation cause, the same class the item domain's own spawn report already is. The
/// request is stamped with the world/layer generation, so a request that outlived a layer boundary is refused by
/// name instead of materializing at layer-relative coordinates in the next layer.
/// </para>
///
/// <para>
/// The created object does not travel on this verb's own pair: an entity and an animal report through
/// <c>EntitySpawned</c>, a loose item entity through the item domain's spawn row — the SAME row and the same world
/// object the give ladder's drop rung makes — and each family's absolute table is what a later joiner reads. This
/// service only creates, judges and answers.
/// </para>
/// </summary>
public sealed class WorldSummonService(
	ISessionControl session,
	PacketSender sender,
	IWorldSummon summon,
	IMemberCommandRules memberRules,
	ItemKernelAuthority kernelAuthority,
	ILocalizationService localization,
	ILogger<WorldSummonService> log) : IWorldSummonControl
{
	/// <summary>Where this side's world/layer generation comes from — the same kernel run baseline every stamped world report carries.</summary>
	private readonly KernelWorldGenerationSource _generations = new(kernelAuthority);

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly IWorldSummon _summon = summon;
	private readonly IMemberCommandRules _memberRules = memberRules;
	private readonly ILocalizationService _localization = localization;
	private readonly ILogger<WorldSummonService> _log = log;

	/// <summary>
	/// One answer part: a catalogue key plus its values, and the English rendering of the same key at Debug so a log can answer
	/// "what did the member read". The <c>labelKey</c> parameter name is the localization census's convention: a literal passed
	/// here is a READ of the catalogue, which is what stops an orphan key from shipping.
	/// </summary>
	private ConsoleAnswerPartMsg AnswerPart(string labelKey, params string[] args)
	{
		_log.LogDebug("[Summon] answer part: {Line}", _localization.Format(labelKey, args));
		var key = labelKey;
		return new ConsoleAnswerPartMsg { Key = key, Args = [.. args] };
	}

	/// <summary>
	/// A refusal this service OWNS, as the answer parts the member's console renders: the KEY plus its arguments, so the
	/// reader's language wins. The text stays on the result for the host's log and as the fallback.
	/// </summary>
	private SummonResult RefuseByRule(string labelKey, string reason, params string[] args)
	{
		_log.LogWarning("[Summon] refused: {Reason} (the member reads: {Line})", reason, _localization.Format(labelKey, args));
		return new SummonResult(false, $"Refused: {reason}", 0, [AnswerPart(labelKey, args)]);
	}

	/// <summary>
	/// A refusal the answer cannot render in the reader's language — the layer stamp's own description, the creation port's
	/// A refusal whose sentence the catalogue owns when its source names a KEY (the creation port's grounds do since 2026-10-28); a source that still
	/// answers free text (a layer stamp's description, the prefab parse) falls back to the generic line, and the detail stays in the log.
	/// </summary>
	private SummonResult RefuseUnconverted(string reason, GrantRefusal? keyed = null)
	{
		_log.LogWarning("[Summon] refused: {Reason}", reason);
		if (keyed is not null)
		{
			var keyedArgs = new string[keyed.Args.Count];
			for (var i = 0; i < keyedArgs.Length; i++)
			{
				keyedArgs[i] = keyed.Args[i];
			}

			return new SummonResult(false, $"Refused: {reason}", 0, [AnswerPart(keyed.Key, keyedArgs)]);
		}

		return new SummonResult(false, $"Refused: {reason}", 0, [AnswerPart("answer.refused_see_host_log")]);
	}

	/// <inheritdoc />
	public event Action<WorldSummonResultMsg>? Answered;

	/// <inheritdoc />
	public ConsoleAnswer Summon(string prefabId, WorldSummonKind kind, WorldSummonPosition position, string stateText)
	{
		if (!_session.SessionActive)
		{
			return ConsoleAnswer.Refused(
				"Refused: no active session — a cheat verb needs one: the host's own permission is the online switch, and a member's is the host's grant for the session.",
				"cheat.refused.no_session",
				"summon");
		}

		if (!TryResolvePosition(position, out var x, out var y, out var positionRefusal))
		{
			var reason = positionRefusal ?? "this client's scene cannot answer that position.";
			_log.LogWarning("[Summon] {Actor} -> {Prefab} refused before a position was resolved: {Reason}", _session.LocalSteamId, prefabId, reason);
			return ConsoleAnswer.Refused($"Refused: {reason}", "summon.answer.refused_position", reason);
		}

		if (_session.Role == SessionRole.Guest)
		{
			// The position is resolved HERE, on the client whose pointer or body was named: the host has no
			// access to this scene's pointer, and the member's own answer is the only one that can be right.
			_sender.Send(_session.HostSteamId, NetMsg.WorldSummonRequest, new WorldSummonRequestMsg
			{
				PrefabId = prefabId,
				Kind = kind,
				State = stateText,
				X = x,
				Y = y,
				Generation = _generations.Stamp(),
			});
			_log.LogInformation("[Summon] {Actor} requested {Kind} {Prefab} at ({X:F1},{Y:F1}){State} from the host.",
				_session.LocalSteamId, kind, prefabId, x, y, stateText.Length == 0 ? "" : " with state " + stateText);
			return ConsoleAnswer.Silent;
		}

		// The outcome's PARTS are the answer, exactly as the give verb's: one line per catalogue key, rendered by whichever client reads it.
		var outcome = Apply(_session.LocalSteamId, prefabId, kind, stateText, x, y);
		var parts = new List<ConsoleAnswerPart>(outcome.Parts.Count);
		foreach (var part in outcome.Parts)
		{
			parts.Add(new ConsoleAnswerPart(part.Key, [.. part.Args]));
		}

		return outcome.Applied ? ConsoleAnswer.Ok(outcome.Answer, parts) : ConsoleAnswer.Refused(outcome.Answer, parts);
	}

	/// <inheritdoc />
	public void HandleSummonRequest(ulong sender, WorldSummonRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_session.LocalInWorld)
		{
			// The host has no layer to create in — the same ground the member's own scene check answers, reached
			// from the wire. Answered rather than swallowed: a refusal that never travels looks like a lost request.
			RefuseRequest(sender, msg, RefuseByRule("summon.answer.refused_host_not_in_world", "this host is not in a world, so there is no layer to create anything in."));
			return;
		}

		if (WorldReportGenerationGate.Relate(msg.Generation, _generations.Current, "WorldSummonRequest", sender, _log)
			== WorldGenerationRelation.Stale)
		{
			// The position is layer-relative and the host has moved on: creating here would put the object at
			// the old layer's coordinates in the new one. The reporter is answered so the refusal is visible
			// where the verb was typed.
			RefuseRequest(sender, msg, RefuseUnconverted($"the request belongs to {WorldReportGeneration.Describe(msg.Generation)} while this host is at "
				+ $"{WorldReportGeneration.Describe(_generations.Current)} — positions are layer-relative, so nothing was created; run /summon again here."));
			return;
		}

		if (!_memberRules.CheatVerbsOpenToMembers)
		{
			RefuseRequest(sender, msg, RefuseByRule("summon.answer.refused_switch_off", "the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run /summon."));
			return;
		}

		if (!_memberRules.AllowsCheatVerb(sender))
		{
			RefuseRequest(sender, msg, RefuseByRule("summon.answer.refused_not_in_roster", "the host's cheat roster does not name you, so /summon cannot land. The host grants it with /op."));
			return;
		}

		if (msg.State.Length > ItemStateText.MaxLength)
		{
			RefuseRequest(sender, msg, RefuseByRule(
				"summon.answer.refused_too_much_state",
				$"the request carries {msg.State.Length} character(s) of state; at most {ItemStateText.MaxLength} are accepted.",
				msg.State.Length.ToString(CultureInfo.InvariantCulture),
				ItemStateText.MaxLength.ToString(CultureInfo.InvariantCulture)));
			return;
		}

		SendAnswer(sender, msg, Apply(sender, msg.PrefabId, msg.Kind, msg.State, msg.X, msg.Y));
	}

	/// <inheritdoc />
	public void FireSummonAnswered(WorldSummonResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[Summon] an answer for {Prefab} arrived while this process is {Role} — dropped.", msg.PrefabId, _session.Role);
			return;
		}

		Answered?.Invoke(msg);
	}

	/// <summary>
	/// One creation on the HOST, shared by its own console and a member's request: re-parse the state (the wire's
	/// text is the member's claim), hand the prefab and the type word to the scene seam — which enforces the word
	/// against the game's own tables, binds the state and creates it — and write the sentence both ends read.
	/// </summary>
	private SummonResult Apply(ulong actor, string prefabId, WorldSummonKind kind, string stateText, float x, float y)
	{
		if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
		{
			// The point is a claim (the host's own console included): a hand-crafted frame bypasses the parser's
			// finite check, and a NaN would poison every position written from the creation.
			var reason = "the requested position is not a finite point, so nothing was created.";
			LogRefused(actor, prefabId, x, y, reason);
			return RefuseUnconverted(reason);
		}

		if (!ItemStateText.TryParse(stateText, out var state, out _, out var parseError))
		{
			LogRefused(actor, prefabId, x, y, parseError!);
			return RefuseUnconverted(parseError!);
		}

		var outcome = _summon.TrySummon(prefabId, kind, state, x, y);
		if (!outcome.Created)
		{
			var reason = outcome.Refusal?.Text ?? $"'{prefabId}' could not be created by this build.";
			LogRefused(actor, prefabId, x, y, reason);
			return RefuseUnconverted(reason, outcome.Refusal);
		}

		var what = outcome.InstanceId != 0
			? $"{prefabId} (#{outcome.InstanceId.ToString(CultureInfo.InvariantCulture)})"
			: prefabId;
		var stateClause = outcome.State.Length == 0 ? "" : " with state " + outcome.State;
		var answer = Describe(what, x, y, stateClause);

		_log.LogInformation("[Summon] {Actor} summoned {Kind} {Prefab}{State} at ({X:F1},{Y:F1}).",
			actor, kind, what, stateClause, x, y);
		return new SummonResult(true, answer, outcome.InstanceId, PartsOf(prefabId, outcome.InstanceId, x, y, outcome.State));
	}

	/// <summary>
	/// The same answer as catalogue PARTS, so the member's console prints it in the member's own language: what was created
	/// (an item entity's id included), where it really is, and — when the operator gave it a state — that state as its own line.
	/// The text beside it stays for the host's log and as the fallback rendering.
	/// </summary>
	private List<ConsoleAnswerPartMsg> PartsOf(string prefabId, ulong instanceId, float x, float y, string state)
	{
		var part = instanceId != 0
			? AnswerPart("summon.answer.item_entity", prefabId, instanceId.ToString(CultureInfo.InvariantCulture), x.ToString("F1", CultureInfo.InvariantCulture), y.ToString("F1", CultureInfo.InvariantCulture))
			: AnswerPart("summon.answer.prefab", prefabId, x.ToString("F1", CultureInfo.InvariantCulture), y.ToString("F1", CultureInfo.InvariantCulture));
		var parts = new List<ConsoleAnswerPartMsg> { part };

		if (state.Length > 0)
		{
			parts.Add(AnswerPart("summon.answer.state", state));
		}

		return parts;
	}

	/// <summary>
	/// The one sentence both ends print: what was created (an item entity's id included), where it really is (the
	/// point the creation used, never the argument the operator typed) and the state the object really carries.
	/// </summary>
	private static string Describe(string what, float x, float y, string stateClause)
	{
		var builder = new StringBuilder();
		builder.Append("Summoned ").Append(what)
			.Append(" at (").Append(x.ToString("F1", CultureInfo.InvariantCulture))
			.Append(", ").Append(y.ToString("F1", CultureInfo.InvariantCulture)).Append(')')
			.Append(stateClause)
			.Append('.');
		return builder.ToString();
	}

	/// <summary>The position the creation really uses: a literal point as typed, or the scene seam's answer for a keyword.</summary>
	private bool TryResolvePosition(WorldSummonPosition position, out float x, out float y, out string? refusal)
	{
		if (position.Anchor is { } anchor)
		{
			return _summon.TryResolveAnchor(anchor, out x, out y, out refusal);
		}

		x = position.X;
		y = position.Y;
		refusal = null;
		return true;
	}

	// A refusal keeps the text path (its reasons are a separate sweep): no parts, so the member's console prints the sentence
	private void RefuseRequest(ulong requester, WorldSummonRequestMsg request, SummonResult refusal)
	{
		_log.LogWarning("[Summon] request from {Requester} for {Prefab} at ({X:F1},{Y:F1}) refused.", requester, request.PrefabId, request.X, request.Y);
		SendAnswer(requester, request, refusal);
	}

	/// <summary>
	/// Send the answer to a member's request. The answer shares the host log's REASON wording — the same reason
	/// reads identically on both ends — but it is its own sentence: the log line names the actor and carries the
	/// verb prefix, the answer carries the verdict and what was created. The APPLIED case is logged where the
	/// creation happened; this is only the transport.
	/// </summary>
	private void SendAnswer(ulong requester, WorldSummonRequestMsg request, SummonResult result)
	{
		_sender.Send(requester, NetMsg.WorldSummonResult, new WorldSummonResultMsg
		{
			PrefabId = request.PrefabId,
			ItemInstanceId = result.InstanceId,
			Applied = result.Applied,
			Answer = result.Answer,
			// The answer as catalogue keys: the member's console renders each part in the member's own language.
			Parts = result.Parts,
		});
	}

	private void LogRefused(ulong actor, string prefabId, float x, float y, string reason) =>
		_log.LogWarning("[Summon] {Actor} -> {Prefab} at ({X:F1},{Y:F1}) refused: {Reason}", actor, prefabId, x, y, reason);

	/// <summary>What one summon produced: the answer both ends print, and the identity only the host and its tests need.</summary>
	private readonly record struct SummonResult(bool Applied, string Answer, ulong InstanceId, List<ConsoleAnswerPartMsg> Parts);
}

using System;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The console <c>/gamerule</c> verb's write path: the host writes its own config entry
/// and answers with the value the entry holds afterwards; a member sends the request and
/// its answer arrives on the wire.
///
/// <para>
/// A rule is the HOST's own switch (decision 263), so the host is the only writer. A
/// member's local entry is overridden by the host's announced set, so a local write would
/// look applied and do nothing — the request/answer pair exists instead of that lie. The
/// host judges the member's request against the same two rulings every cheat verb reads
/// (<see cref="IMemberCommandRules"/>: the switch, then the roster) and the same value
/// grammar the member's own console ran, then writes through the same
/// <see cref="IHostRulesEditor"/> the Admin page and the host's own verb use. Every writer
/// ends at the config entry, so decision 263's change edge announces the new set to every
/// member exactly once.
/// </para>
///
/// <para>
/// The answer is the value IN FORCE, read back from <see cref="IHostRules"/> after the
/// write: the config layer may clamp or refuse a write underneath the editor, and an
/// answer that repeated the requested value would then claim a change the config does not
/// hold. The log line and the console sentence carry the same facts (the actor, the rule,
/// the value written and the value in force); they are two sentences, and the log is the
/// one that names who wrote.
/// </para>
/// </summary>
internal sealed class HostRuleChangeService(
	ISessionControl session,
	IHostRules hostRules,
	IHostRulesEditor editor,
	IMemberCommandRules memberRules,
	PacketSender sender,
	ILogger<HostRuleChangeService> log) : IHostRuleChangeControl
{
	private readonly ISessionControl _session = session;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IHostRulesEditor _editor = editor;
	private readonly IMemberCommandRules _memberRules = memberRules;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<HostRuleChangeService> _log = log;

	public event Action<HostRuleChangeResultMsg>? Answered;

	/// <inheritdoc />
	public string? Change(HostRuleId rule, string canonicalValue)
	{
		var row = HostRuleCensus.Of(rule);
		if (_session.Role == SessionRole.Guest)
		{
			_sender.Send(_session.HostSteamId, NetMsg.HostRuleChangeRequest, new HostRuleChangeRequestMsg
			{
				Rule = (int)rule,
				Value = canonicalValue,
			});
			_log.LogInformation("[Rules] {Actor} asked the host to set {Rule} = {Value}; the answer will print where the verb was typed.",
				_session.LocalSteamId, row.Name, canonicalValue);
			return null;
		}

		if (_session.Role != SessionRole.Host)
		{
			// The console's cheat gate refuses this before the verb runs; this arm is the
			// invariant made observable rather than a second permission rule (a process with
			// no host role has no config entry to write on anyone's behalf).
			_log.LogWarning("[Rules] /gamerule {Rule} = {Value} reached the write path in role {Role} — nothing was written.",
				row.Name, canonicalValue, _session.Role);
			return $"No host is applying this change in role {_session.Role}; nothing was written.";
		}

		return Apply(_session.LocalSteamId, row, canonicalValue).Answer;
	}

	/// <inheritdoc />
	public void HandleRequest(ulong sender, HostRuleChangeRequestMsg msg)
	{
		// Role and session only: a rule write needs no world, and the host's own verb applies in
		// exactly the same states. A world guard here would silently swallow a member's request in
		// the lobby or between runs — the one thing this family's answers exist to prevent.
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_memberRules.CheatVerbsOpenToMembers)
		{
			RefuseRequest(sender, msg, "the host's cheat switch (AllowMemberCheatVerbs) is off, so no member may run /gamerule.");
			return;
		}

		if (!_memberRules.AllowsCheatVerb(sender))
		{
			RefuseRequest(sender, msg, "the host's cheat roster does not name you, so /gamerule cannot land. The host grants it with /op.");
			return;
		}

		if (!Enum.IsDefined(typeof(HostRuleId), (HostRuleId)msg.Rule))
		{
			var answer = $"Unknown game rule id {msg.Rule}.";
			_log.LogWarning("[Rules] a request from {Requester} was refused: {Reason}", sender, answer);
			SendAnswer(sender, new ChangeOutcome(false, answer));
			return;
		}

		SendAnswer(sender, Apply(sender, HostRuleCensus.Of((HostRuleId)msg.Rule), msg.Value));
	}

	/// <inheritdoc />
	public void FireAnswered(HostRuleChangeResultMsg msg)
	{
		if (_session.Role != SessionRole.Guest)
		{
			_log.LogWarning("[Rules] a change answer arrived while this process is {Role} — dropped.", _session.Role);
			return;
		}

		Answered?.Invoke(msg);
	}

	/// <summary>
	/// The host's one write: re-parse the value with the rule's own grammar, require the
	/// canonical form the request claims, write through the editor, then read the rule back
	/// from the surface in force and answer THAT. Shared by the host's own console and a
	/// member's request, so the two paths can never answer differently.
	/// </summary>
	private ChangeOutcome Apply(ulong actor, HostRuleCensus.Row row, string value)
	{
		if (!HostRuleValueText.TryParse(row, value, out var canonical, out var error))
		{
			_log.LogWarning("[Rules] {Actor} -> {Rule} refused: {Reason}", actor, row.Name, error);
			return new ChangeOutcome(false, $"Game rule {row.Name}: {error}");
		}

		if (!string.Equals(canonical, value, StringComparison.Ordinal))
		{
			var reason = $"'{value}' is not the canonical form of {row.Name}.";
			_log.LogWarning("[Rules] {Actor} -> {Rule} refused: {Reason}", actor, row.Name, reason);
			return new ChangeOutcome(false, $"Game rule {row.Name}: {reason}");
		}

		if (!_editor.TrySet(row.Id, canonical, out var refusal))
		{
			_log.LogWarning("[Rules] {Actor} -> {Rule} could not write {Value}: {Reason}", actor, row.Name, canonical, refusal);
			return new ChangeOutcome(false, $"Game rule {row.Name} was not changed: {refusal}");
		}

		var inForce = HostRuleValueText.Describe(HostRuleSet.From(_hostRules), row);
		if (!string.Equals(inForce, canonical, StringComparison.Ordinal))
		{
			// The entry did not keep what it was given (a config-layer clamp). The answer below
			// names what it really holds; this line is the operator's half of the same fact.
			_log.LogWarning("[Rules] {Actor} wrote {Rule} = {Written} but the entry holds {InForce}; the answer names the value in force.",
				actor, row.Name, canonical, inForce);
		}

		_log.LogInformation("[Rules] {Actor} set {Rule} = {Written}; the entry now holds {InForce}.", actor, row.Name, canonical, inForce);
		return new ChangeOutcome(true, $"Game rule {row.Name} is now set to: {inForce}.");
	}

	/// <summary>
	/// A request refused BEFORE the value is judged — the host's two cheat rulings. Logged
	/// here once with the request's own context and sent back as the same sentence the
	/// requester reads; the ground is always named, so the operator knows whether to ask for
	/// the switch or for a grant.
	/// </summary>
	private void RefuseRequest(ulong requester, HostRuleChangeRequestMsg request, string reason)
	{
		_log.LogWarning("[Rules] request from {Requester} for rule {Rule} refused: {Reason}", requester, request.Rule, reason);
		SendAnswer(requester, new ChangeOutcome(false, $"Refused: {reason}"));
	}

	/// <summary>
	/// Send the answer to the requester. The APPLIED case is logged where the write happened;
	/// this is only the transport, so the same sentence is not logged twice.
	/// </summary>
	private void SendAnswer(ulong requester, ChangeOutcome outcome) =>
		_sender.Send(requester, NetMsg.HostRuleChangeResult, new HostRuleChangeResultMsg
		{
			Applied = outcome.Applied,
			Answer = outcome.Answer,
		});

	/// <summary>What one change produced: the answer both ends print, and the verdict the console colours the line by.</summary>
	private readonly record struct ChangeOutcome(bool Applied, string Answer);
}

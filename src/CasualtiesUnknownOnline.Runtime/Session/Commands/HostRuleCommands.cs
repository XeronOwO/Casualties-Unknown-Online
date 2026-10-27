using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The host-rule command group: <c>/gamerule</c> — the verb that reads one host rule and
/// changes it, shaped after Minecraft's <c>gamerule &lt;rule name&gt; [&lt;value&gt;]</c>.
///
/// <para>
/// It is its own group because its authority is the cheat family's, not the host-only
/// administration group's (<see cref="HostAdminCommands"/>): after the user's 2026-10-24
/// ruling the verb is <see cref="CommandPermission.Anyone"/> at the console and the real
/// gate is <see cref="CheatVerbGate"/> inside the handler — the host passes without either
/// ruling, a member needs the host's switch and its <c>/op</c> roster, and a caller without
/// them reads the gate's own sentence. The write itself is
/// <see cref="IHostRuleChangeControl"/>: the host writes its config entry and answers with
/// the value in force, while a member's verb is a request whose answer arrives on the wire.
/// </para>
///
/// <para>
/// The grammar is the accepted batch's, unchanged: an omitted value queries, a rule name is
/// one of the census's own lowercase names and is case-sensitive, and a value is the rule's
/// own kind. An invalid value is refused HERE, before the gate and before anything is sent,
/// so a member's typo costs no round trip. Every refusal answers with the refusal kind, which
/// is what makes it print in the error colour instead of the success one.
/// </para>
/// </summary>
internal sealed class HostRuleCommands(
	IHostRules hostRules,
	IHostRuleChangeControl changes,
	CheatVerbGate cheatVerbs,
	ILogger log)
{
	private readonly IHostRules _hostRules = hostRules;
	private readonly IHostRuleChangeControl _changes = changes;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ILogger _log = log;

	/// <summary>
	/// The host-rule verb: the value is optional and omitting it queries the rule; a rule name
	/// is one of the census's own lowercase names and is case-sensitive; a value is the rule's
	/// own kind and is refused by naming the accepted set. The write goes through the same
	/// config entry as the Admin page, so decision 263's change edge announces the new set to
	/// every member on the next pump. A member's write is a request to the host: its local
	/// config is never touched, and the host's answer prints where the verb was typed.
	/// </summary>
	[ConsoleCommand("gamerule", "Read or set one host rule, shaped like Minecraft's /gamerule (a cheat verb: the host, or a member the host granted).", CommandPermission.Anyone, "/gamerule <rule name> [<value>]", CommandArgumentKind.Text, CommandArgumentKind.Text, Gate = ConsoleCommandGate.Cheat)]
	internal ConsoleAnswer GameRule(IReadOnlyList<string> args)
	{
		if (args.Count is < 2 or > 3)
		{
			return ConsoleAnswer.Refused("Usage: /gamerule <rule name> [<value>]");
		}

		var row = HostRuleCensus.Find(args[1]);
		if (row is null)
		{
			_log.LogInformation("[Command] /gamerule refused the unknown rule name '{Name}'.", args[1]);
			return ConsoleAnswer.Refused($"Unknown game rule '{args[1]}'. Known rules: {string.Join(", ", HostRuleCensus.Names)}.", "rules.answer.unknown", args[1], string.Join(", ", HostRuleCensus.Names));
		}

		var canonical = "";
		if (args.Count == 3 && !HostRuleValueText.TryParse(row, args[2], out canonical, out var error))
		{
			_log.LogInformation("[Command] /gamerule refused {Rule} = '{Text}': {Reason}", row.Name, args[2], error);
			return ConsoleAnswer.Refused($"Game rule {row.Name}: {error}", "rules.answer.refused_with_reason", row.Name, error ?? "");
		}

		if (_cheatVerbs.TryRefuse("gamerule", out var refusal))
		{
			return refusal;
		}

		if (args.Count == 2)
		{
			var current = HostRuleValueText.Describe(HostRuleSet.From(_hostRules), row);
			_log.LogDebug("[Command] /gamerule queried {Rule} = {Value}.", row.Name, current);
			return ConsoleAnswer.Ok($"Game rule {row.Name} is currently set to: {current}.", "rules.answer.current", row.Name, current);
		}

		return ConsoleAnswer.Ok(_changes.Change(row.Id, canonical));
	}
}

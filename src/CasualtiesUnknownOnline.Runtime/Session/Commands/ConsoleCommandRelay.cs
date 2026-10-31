using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's half of the host-relay seam: the HOST runs a definition a member declared <c>HostRelayed</c>, and the MEMBER prints the answer the host sent
/// back. Its own type because the console service is the line's editor and the panel's owner, and this is neither — it is the one place where a command crosses
/// from the player who typed it to the machine that may run it.
/// </summary>
internal sealed class ConsoleCommandRelay(
	CommandRelayHub hub,
	ConsoleCommandRegistry registry,
	IConsoleLineSink lines,
	ILogger log)
{
	/// <summary>
	/// A MEMBER's side of the seam: when the definition declared <c>HostRelayed</c> and this process is not the host, the line goes to the host and the answer
	/// prints here when it arrives. Returns true when the relay took the line (sent, or refused because it could not be sent), false when the line is this side's
	/// to run — the host's own copy, or a local command that never declared the scope.
	/// </summary>
	internal bool TrySend(CommandDefinition command, IReadOnlyList<string> tokens, bool isHost)
	{
		if (isHost || command.Contract is not { Scope: CommandScope.HostRelayed })
		{
			return false;
		}

		if (command.OwnerModId is not { Length: > 0 } owner || !hub.Submit(owner, command.Name, ArgumentsAfter(tokens)))
		{
			lines.AddKeyedLine("console.command.relay_unavailable", "cannot reach the host", ConsoleLineKind.Error);
		}

		return true;
	}

	private static List<string> ArgumentsAfter(IReadOnlyList<string> tokens)
	{
		var arguments = new List<string>(Math.Max(0, tokens.Count - 1));
		for (var index = 1; index < tokens.Count; index++)
		{
			arguments.Add(tokens[index]);
		}

		return arguments;
	}

	/// <summary>
	/// Runs the definition on the host for the member who asked. The declared permission is the first gate (a host-only command is refused to a member) and then
	/// the definition's own relay answer runs with that member as the requester, so its handler says "you" about the player who typed. Null means this side has no
	/// such definition, and the caller answers with its own refusal.
	/// </summary>
	internal ConsoleAnswer? AnswerOnHost(string name, IReadOnlyList<string> arguments, ulong requester)
	{
		var command = registry.Find(name);
		if (command?.Contract is not { Scope: CommandScope.HostRelayed } contract)
		{
			log.LogWarning("[Console] relayed /{Name} from {Requester} refused: no HostRelayed definition.", name, requester);
			return null;
		}

		if (contract.Permission == CommandPermission.HostOnly)
		{
			return ConsoleAnswer.Refused(ConsoleCommandAuthority.HostOnlyRefusal, "console.command.host_only");
		}

		if (command.RelayAnswer is not { } answer)
		{
			log.LogWarning("[Console] relayed /{Name} from {Requester} refused: the definition has no relay answer.", name, requester);
			return null;
		}

		var tokens = new List<string>(arguments.Count + 1) { name };
		tokens.AddRange(arguments);
		return answer(requester, tokens);
	}

	/// <summary>
	/// Prints the host's answer on the member that asked: one line per part, each rendered by THIS client's catalogue, in the colour the answer says (a refusal is
	/// not a success). The text is the fallback when the catalogue has no entry for a part.
	/// </summary>
	internal void Print(CommandRelayOutcome outcome)
	{
		var kind = outcome.Refused ? ConsoleLineKind.Error : ConsoleLineKind.Success;
		if (outcome.Parts.Count == 0)
		{
			if (!string.IsNullOrWhiteSpace(outcome.Text))
			{
				lines.AddLine(outcome.Text!, kind, notifiable: true);
			}

			return;
		}

		foreach (var part in outcome.Parts)
		{
			var args = new string[part.Args.Count];
			for (var index = 0; index < args.Length; index++)
			{
				args[index] = part.Args[index];
			}

			lines.AddKeyedLine(part.Key, outcome.Text, kind, args);
		}
	}
}

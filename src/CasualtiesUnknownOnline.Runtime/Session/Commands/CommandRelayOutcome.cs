using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// A relayed console command's answer, on the requester's side of the wire: one part per printed line (a key plus its arguments, rendered by the requester's
/// own catalogue) and the plain text the host produced (the fallback, and what a client without the entry reads). A refusal prints in the error colour.
/// </summary>
internal sealed record CommandRelayOutcome(bool Refused, string Text, IReadOnlyList<ConsoleAnswerPart> Parts)
{
	/// <summary>Reads the host's answer off the result frame.</summary>
	internal static CommandRelayOutcome FromResult(ModCommandResultMsg msg)
	{
		var parts = new List<ConsoleAnswerPart>(msg.AnswerParts.Count);
		foreach (var part in msg.AnswerParts)
		{
			var args = new string[part.Args.Count];
			for (var index = 0; index < args.Length; index++)
			{
				args[index] = part.Args[index];
			}

			parts.Add(new ConsoleAnswerPart(part.Key, args));
		}

		return new CommandRelayOutcome(msg.Refused, msg.Output, parts);
	}

	/// <summary>Writes a console answer into the result frame the requester reads.</summary>
	internal static void WriteInto(ModCommandResultMsg msg, ConsoleAnswer answer)
	{
		msg.Refused = answer.Kind == ConsoleAnswerKind.Refusal;
		msg.Output = answer.Text ?? string.Empty;

		var parts = new List<ConsoleAnswerPartMsg>();
		if (answer.HasParts)
		{
			foreach (var part in answer.Parts!)
			{
				parts.Add(new ConsoleAnswerPartMsg { Key = part.Key, Args = [.. part.Args] });
			}
		}
		else if (answer.Key is { Length: > 0 } key)
		{
			var part = new ConsoleAnswerPartMsg { Key = key };
			if (answer.Args is { Count: > 0 } args)
			{
				foreach (var argument in args)
				{
					part.Args.Add(argument);
				}
			}

			parts.Add(part);
		}

		msg.AnswerParts = parts;
	}
}

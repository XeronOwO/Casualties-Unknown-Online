using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's authority predicate: may THIS caller run THIS command? One decision, asked by the
/// completion filter, by <c>/help</c> and (for the declaration half) by the dispatcher, so a command the
/// console hides is exactly a command the console would refuse.
///
/// <para>
/// Two sources, the same ones the handlers enforce: the declared <see cref="CommandPermission"/> (a
/// <see cref="CommandPermission.HostOnly"/> verb is the host's alone) and the declared
/// <see cref="ConsoleCommandGate"/> (a cheat verb asks the family's gate). A mod command declares only its
/// permission — its own runtime gate is not knowable here and is recorded as a limit, not modelled.
/// </para>
///
/// <para>
/// Hiding is a courtesy, never the enforcement: a hidden command typed in full still reaches its handler,
/// which refuses it by name.
/// </para>
/// </summary>
internal sealed class ConsoleCommandAuthority(ISessionControl session, CheatVerbGate cheatVerbs)
{
	/// <summary>The console's one host-only refusal sentence, printed by the dispatcher and by <c>/help</c>.</summary>
	internal const string HostOnlyRefusal = "Permission denied: this command is host-only.";

	/// <summary>The catalogue key of <see cref="HostOnlyRefusal"/>, so the console renders it in the reader's language.</summary>
	internal const string HostOnlyRefusalKey = "console.command.host_only";

	private readonly ISessionControl _session = session;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;

	/// <summary>Whether this caller may run the command. True means the console may offer it, never that the handler will accept every line.</summary>
	internal bool MayRun(CommandDefinition command) => TryRefusal(command) is null;

	/// <summary>
	/// The answer this caller would meet by running the command now, or null when it may run — the refusal as the console's own answer shape, so its
	/// sentence reaches the reader in the reader's language. The cheat arm is the gate's QUIET form: no log line per keystroke or per help row the filter
	/// asks about.
	/// </summary>
	internal ConsoleAnswer? TryRefusal(CommandDefinition command)
	{
		if (command.Permission == CommandPermission.HostOnly && _session.Role != SessionRole.Host)
		{
			return ConsoleAnswer.Refused(HostOnlyRefusal, HostOnlyRefusalKey);
		}

		// The cheat family is declared EITHER way: a built-in verb carries it as its gate, a mod definition as its permission. Both ask the same gate, so the
		// filter and the dispatcher cannot disagree about which family a command belongs to.
		if ((command.Gate == ConsoleCommandGate.Cheat || command.Permission == CommandPermission.Cheat)
			&& _cheatVerbs.TryRefuseQuiet(command.Name, out var refusal))
		{
			return refusal;
		}

		return null;
	}
}

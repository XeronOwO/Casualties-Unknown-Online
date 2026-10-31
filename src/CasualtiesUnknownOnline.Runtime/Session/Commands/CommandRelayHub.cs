using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;


namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The member↔host seam for console commands that declared <see cref="CommandScope.HostRelayed"/>.
/// </summary>
/// <remarks>
/// The two halves live in different domains (the console owns definitions and answers, the mod domain owns the request/result frames), and neither may
/// reference the other — the session container would get a cycle. This hub is the mediator: each half sets the slot it owns at construction and calls the
/// other half's slot through it. A request id is allocated here, so a member can have several relayed commands in flight and each answer finds its way back
/// to the line that asked for it.
/// </remarks>
public sealed class CommandRelayHub
{
	private readonly Dictionary<uint, string> _pending = [];
	private uint _nextRequestId = 1;

	/// <summary>Set by the mod domain: send the request frame to the host. Null until the mod domain is constructed.</summary>
	internal Action<uint, string, string, IReadOnlyList<string>>? Send { get; set; }

	/// <summary>Set by the console on the HOST: run a relayed definition for <c>requester</c> and answer it.</summary>
	internal Func<string, IReadOnlyList<string>, ulong, ConsoleAnswer?>? RunOnHost { get; set; }

	/// <summary>Set by the console on a MEMBER: an answer for a request this console sent has arrived.</summary>
	internal Action<string, CommandRelayOutcome>? Arrived { get; set; }

	/// <summary>Sends one relayed command and remembers which command the answer belongs to. Returns false when no sender is wired (no session).</summary>
	internal bool Submit(string modId, string name, IReadOnlyList<string> arguments)
	{
		if (Send is null)
		{
			return false;
		}

		var requestId = _nextRequestId++;
		_pending[requestId] = name;
		Send(requestId, modId, name, arguments);
		return true;
	}

	/// <summary>
	/// Answers a request that arrived from a member. Returns false when this side has no host runner (a member that somehow received a request).
	/// </summary>
	internal bool AnswerOnHost(string name, IReadOnlyList<string> arguments, ulong requester, out ConsoleAnswer answer)
	{
		var runner = RunOnHost;
		if (runner?.Invoke(name, arguments, requester) is { } produced)
		{
			answer = produced;
			return true;
		}

		answer = ConsoleAnswer.Refused($"the host does not run '/{name}'", "console.command.relay_not_hosted", name);
		return false;
	}

	/// <summary>Hands a result frame's answer to the console that asked for it, in the requester's own language.</summary>
	internal void Deliver(uint requestId, CommandRelayOutcome outcome)
	{
		if (!_pending.TryGetValue(requestId, out var name))
		{
			return;
		}

		_pending.Remove(requestId);

		Arrived?.Invoke(name, outcome);
	}

	/// <summary>Forgets every pending answer — a session ended, so no result can arrive for them.</summary>
	internal void Reset() => _pending.Clear();
}

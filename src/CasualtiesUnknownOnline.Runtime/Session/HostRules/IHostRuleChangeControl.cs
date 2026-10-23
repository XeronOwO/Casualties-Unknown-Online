using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The write half of the console's <c>/gamerule</c> verb after the user's 2026-10-24
/// ruling joined it to the op family: on the host it writes this process's config
/// entry and answers with the value in force; on a member it sends the request to the
/// host and returns null, because the host authors the answer.
///
/// <para>
/// The permission is not this surface's business: the console command runs the cheat
/// family's <c>CheatVerbGate</c> before it calls here, and the host judges the same two
/// rulings again on every request that arrives. This seam only performs or forwards a
/// write that has already been admitted, so a caller that bypassed the gate still lands
/// in the host's own check rather than in a second permission implementation.
/// </para>
///
/// <para>
/// The value is the CANONICAL text <see cref="HostRuleValueText"/> produced. The host
/// re-parses it, writes it and READS THE ENTRY BACK, so its answer names the value the
/// config really holds — a clamp or a refused write can never be answered as if the
/// requested value had landed.
/// </para>
/// </summary>
public interface IHostRuleChangeControl
{
	/// <summary>
	/// One admitted <c>/gamerule</c> write. The host applies it and returns the answer
	/// sentence; a member sends the request and returns null, because the answer arrives
	/// through <see cref="Answered"/>. The caller has already applied the cheat gate.
	/// </summary>
	string? Change(HostRuleId rule, string canonicalValue);

	/// <summary>Host only: a member's change request arrived — judge the host's switch and roster, re-parse the value, write, and answer with the value in force.</summary>
	void HandleRequest(ulong sender, HostRuleChangeRequestMsg msg);

	/// <summary>Member: the host's answer arrived — raise <see cref="Answered"/> so the console prints it where the verb was typed.</summary>
	void FireAnswered(HostRuleChangeResultMsg msg);

	/// <summary>The host answered a change request — the console prints the sentence where the verb was typed.</summary>
	event Action<HostRuleChangeResultMsg>? Answered;
}

using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The console <c>/heal</c> verb's control surface: one invocation from the local console, the
/// host's judgement of an arrived member request, the target client's application of the host's
/// instruction, and the two answers each side prints where the operator (or the target) reads.
/// </summary>
public interface IHealResetControl
{
	/// <summary>
	/// THIS client's body was reset by a host instruction — the target's own console reads what
	/// happened (a member's reset arrives from the host, never from its own command).
	/// </summary>
	event Action<HealResetNotice>? Applied;

	/// <summary>The host's answer to this member's request arrived — the console prints it.</summary>
	event Action<HealResetResultMsg>? Answered;

	/// <summary>
	/// One <c>/heal</c> invocation on this client. The host resets its own body and instructs the
	/// member targets, returning the answer line; a member sends the request and returns null,
	/// because its answer arrives later through <see cref="Answered"/>.
	/// </summary>
	string? Reset(IReadOnlyList<ulong> targetSteamIds);

	/// <summary>Host only: a member's heal request arrived — judge this host's cheat switch and roster, then carry the reset out.</summary>
	void HandleRequest(ulong sender, HealResetRequestMsg msg);

	/// <summary>Target client: the host instructed this client to reset its own body.</summary>
	void HandleInstruction(ulong sender, HealResetInstructionMsg msg);

	/// <summary>Guest only: a heal answer arrived from the host.</summary>
	void FireAnswered(HealResetResultMsg msg);
}

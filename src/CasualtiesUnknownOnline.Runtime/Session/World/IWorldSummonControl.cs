using System;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The console <c>/summon</c> verb's control surface: one invocation from the local console, and the two wire
/// halves a member's invocation needs — the host's judgement of an arrived request, and the answer the console
/// prints where the operator typed the verb.
/// </summary>
public interface IWorldSummonControl
{
	/// <summary>
	/// The host's answer to a member's request arrived — the console prints it. Only a member has one: the
	/// host's own verb returns its answer to its own console directly.
	/// </summary>
	event Action<WorldSummonResultMsg>? Answered;

	/// <summary>
	/// One <c>/summon</c> invocation on this client. The host creates and returns the answer line; a member
	/// resolves the position on its own scene and sends the request, returning null because its answer arrives
	/// later through <see cref="Answered"/>.
	/// </summary>
	string? Summon(string prefabId, WorldSummonKind kind, WorldSummonPosition position, string stateText);

	/// <summary>Host only: a member's summon request arrived — judge this host's cheat switch and roster, then create and answer.</summary>
	void HandleSummonRequest(ulong sender, WorldSummonRequestMsg msg);

	/// <summary>Guest only: a summon answer arrived from the host.</summary>
	void FireSummonAnswered(WorldSummonResultMsg msg);
}

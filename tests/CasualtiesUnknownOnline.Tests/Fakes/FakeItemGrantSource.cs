using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The game's item table behind <c>/give</c>, as a test's own fixture: the three definitions
/// the give suites name, each with the state its prefab would carry, a minted instance id,
/// and a refusal for anything else — the port's own answer without loading the game assembly.
///
/// <para>
/// The state OVERLAY and the bind are the same rules the real port applies
/// (<see cref="FakeItemState"/>): the fixture's document is merged onto the definition's
/// capture and bound before an id exists, so a state the definition cannot carry refuses the
/// give rather than reaching a created item. A definition's capture is deliberately
/// definition-specific — <c>waterbottle</c> is a container whose prefab captured no stack,
/// <c>largebattery</c> carries the <c>BatteryItem</c> component, <c>bandage</c> neither — so a
/// suite can tell "the state was applied" from "it was accepted for an item that never had it".
/// </para>
/// </summary>
internal sealed class FakeItemGrantSource : IItemGrantSource
{
	private ulong _next = 800000;

	/// <summary>Definitions this build cannot resolve (the port's refusal path).</summary>
	internal HashSet<string> Unknown { get; } = new[] { "notanitem" }.ToHashSet();

	/// <summary>Every definition the verb asked for, in order — including the ones it refused.</summary>
	internal List<string> Attempts { get; } = [];

	/// <summary>Every fact this source created, in order.</summary>
	internal List<CharacterItemMsg> Created { get; } = [];

	public bool TryCreate(string definitionId, SnbtValue? state, out CharacterItemMsg? item, out GrantRefusal? refusal)
	{
		Attempts.Add(definitionId);
		item = null;
		refusal = null;
		if (string.IsNullOrWhiteSpace(definitionId) || Unknown.Contains(definitionId))
		{
			refusal = new GrantRefusal($"No item definition '{definitionId}' is known to this game — a verb names a definition from the game's own item table.", "item.answer.refused_unknown_definition", definitionId);
			return false;
		}

		var capture = FakeItemState.Definition(definitionId);
		if (capture is null)
		{
			refusal = new GrantRefusal($"No item definition '{definitionId}' is known to this game — a verb names a definition from the game's own item table.", "item.answer.refused_unknown_definition", definitionId);
			return false;
		}

		var document = state is null
			? FakeItemState.Describe(capture)
			: SnbtTree.Overlay(FakeItemState.Describe(capture), state);
		if (!FakeItemState.TryBind(capture, document, out var bound, out var stateText))
		{
			refusal = new GrantRefusal(
				stateText ?? "the state text was not accepted.",
				"item.answer.refused_state_text",
				stateText ?? string.Empty);
			return false;
		}

		bound.InstanceId = _next++;
		item = bound;
		Created.Add(bound);
		return true;
	}
}

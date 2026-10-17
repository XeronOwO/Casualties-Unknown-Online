using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The game's item table behind <c>/give</c>, as a test's own fixture: the three
/// definitions the give suites name, each with the state its prefab would carry,
/// a minted instance id, and a refusal for anything else — the port's own
/// three-valued answer without loading the game assembly.
///
/// <para>
/// A definition's state is deliberately definition-specific: <c>waterbottle</c>
/// carries one liquid stack and <c>largebattery</c> one component field, exactly
/// the shapes a property write addresses, while <c>bandage</c> carries neither. A
/// suite can therefore tell "the property was applied" from "the property was
/// accepted for an item that never had it" — the latter is the refusal a real
/// capture would produce.
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

	public bool TryCreate(string definitionId, out CharacterItemMsg? item, out string? refusal)
	{
		Attempts.Add(definitionId);
		item = null;
		refusal = null;
		if (string.IsNullOrWhiteSpace(definitionId) || Unknown.Contains(definitionId))
		{
			refusal = $"No item definition '{definitionId}' is known to this game — /give names a definition from the game's own item table.";
			return false;
		}

		item = new CharacterItemMsg
		{
			InstanceId = _next++,
			ItemId = definitionId,
			SlotIndex = -1,
			Condition = 1f,
		};

		if (definitionId == "waterbottle")
		{
			item.Liquids.Add(new LiquidStackMsg { LiquidId = "water", Amount = 250f });
		}

		if (definitionId == "largebattery")
		{
			item.Components.Add(new ComponentStateMsg
			{
				TypeName = "Battery",
				Fields =
				[
					new ComponentFieldMsg { Name = "charge", Kind = SaveableFieldKind.Float, FloatValue = 0.5f },
				],
			});
		}

		Created.Add(item);
		return true;
	}
}

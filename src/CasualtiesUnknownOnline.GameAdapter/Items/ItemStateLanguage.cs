using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The Game Adapter's half of the item state language, as the Runtime port sees it: the
/// document projection, the schema for completion and the definition schema for
/// <c>/give</c>. The game reads and the legality live in <see cref="ItemStateDocument"/>;
/// this class is the port's registration and names no rule of its own.
/// </summary>
internal sealed class ItemStateLanguage : IItemStateLanguage
{
	public bool TryDescribe(ItemData state, out SnbtValue document)
	{
		document = ItemStateDocument.Describe(state);
		return true;
	}

	public bool TryDescribe(CharacterItemMsg capture, out SnbtValue document)
	{
		document = ItemStateDocument.Describe(capture);
		return true;
	}

	public ItemStateSchema? DescribeSchema(ItemData state) => ItemStateDocument.Schema(state);

	public ItemStateSchema? DescribeDefinitionSchema(string definitionId) => ItemStateDocument.DefinitionSchema(definitionId);
}

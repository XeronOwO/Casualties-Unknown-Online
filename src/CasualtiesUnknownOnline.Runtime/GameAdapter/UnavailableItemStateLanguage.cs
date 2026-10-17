using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The default <see cref="IItemStateLanguage"/> of a build with no Game Adapter: the item
/// state's keys and the component shapes are the game's own, so this build can neither
/// describe nor complete one. The verbs answer "no schema" rather than an empty shape
/// that would read as "the item carries nothing".
/// </summary>
public sealed class UnavailableItemStateLanguage : IItemStateLanguage
{
	public bool TryDescribe(ItemData state, out SnbtValue document)
	{
		document = SnbtCompound.Empty;
		return false;
	}

	public bool TryDescribe(CharacterItemMsg capture, out SnbtValue document)
	{
		document = SnbtCompound.Empty;
		return false;
	}

	public ItemStateSchema? DescribeSchema(ItemData state) => null;

	public ItemStateSchema? DescribeDefinitionSchema(string definitionId) => null;
}

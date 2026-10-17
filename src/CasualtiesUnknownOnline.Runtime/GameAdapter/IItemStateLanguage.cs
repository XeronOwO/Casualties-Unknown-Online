using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Game Adapter's half of the item state language (decision 264): the projection of
/// an item's save-shaped state to the SNBT tree the commands speak, and the schema the
/// console completes from.
///
/// <para>
/// The Runtime owns the text, the path and the sentence; this port owns ONLY what the
/// Runtime cannot know — which keys an item's state carries, which component type names
/// and fields a definition or capture has, and the liquid names a stack may hold. The
/// adapter also owns the LEGALITY of a document (the capacity, a component that cannot be
/// dropped, a field's kind), which is why the write and create ports take a tree rather
/// than a typed value: the document is judged where the live item or the prefab is in
/// hand.
/// </para>
///
/// <para>
/// A build without a Game Adapter registers <see cref="UnavailableItemStateLanguage"/>:
/// there is no schema and no projection, so the verbs answer that fact instead of
/// inventing a shape no item in this build has.
/// </para>
/// </summary>
public interface IItemStateLanguage
{
	/// <summary>The document for one recorded (kernel) item state — the form <c>/data get</c> prints on every peer.</summary>
	bool TryDescribe(ItemData state, out SnbtValue document);

	/// <summary>The document for one local capture — the write verbs' value BEFORE the edit.</summary>
	bool TryDescribe(CharacterItemMsg capture, out SnbtValue document);

	/// <summary>The schema of a recorded item state, for the console's completion; null when this build has no adapter.</summary>
	ItemStateSchema? DescribeSchema(ItemData state);

	/// <summary>The schema of a definition (from its prefab), for completing a <c>/give</c> state; null when the build cannot name it.</summary>
	ItemStateSchema? DescribeDefinitionSchema(string definitionId);
}


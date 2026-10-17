using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The local scene as the item-data family sees it: the item THIS client really
/// holds under an instance id, one save-shaped state written into it, and the item
/// the pointer names.
/// The read half of the family reads the kernel record — the host's authority, a
/// member's replay of the committed batches — and a write cannot go that way: a
/// carried item's state is its HOLDER's own report, so the write lands on the local
/// object first and the record follows the report, which is the order a native use
/// already takes (<c>ItemUseSync</c>).
///
/// <para>
/// The port is deliberately small and holds no item vocabulary: WHICH property the
/// operator named, whether the value is legal, which route the row's report takes
/// and what the operator reads back are the console's business (control plane).
/// This answers what the local scene holds and applies one state document to it.
/// </para>
///
/// <para>
/// A build without the Game Adapter — a test host, a headless one — registers
/// <see cref="UnavailableLocalItemAccess"/> instead, so the verb answers "this
/// build cannot reach the local scene" rather than reading as a client that holds
/// nothing.
/// </para>
/// </summary>
public interface ILocalItemAccess
{
	/// <summary>
	/// The local state of the item this client holds under that id, in the same
	/// save-shaped form the wire and the save use — or null when this client holds
	/// no such item: another player's carried item (its local form is a render
	/// proxy, never the fact), an item this client never loaded, or an id the scene
	/// has no object for. The answer is a SNAPSHOT: the caller may edit it and hand
	/// it back as the desired state.
	/// </summary>
	CharacterItemMsg? ReadLocalItem(ulong instanceId);

	/// <summary>
	/// Apply ONE state document to the local item. The adapter owns the schema and the
	/// legality: it binds the document against the live item (and its own capture), refuses
	/// a key, component, field or value the item cannot carry BY NAME, and only then writes.
	/// A refused document changes nothing, and an applied one is re-captured and returned,
	/// so the caller reports the state the object really holds.
	/// </summary>
	LocalItemWriteOutcome WriteLocalItem(ulong instanceId, SnbtValue desired);

	/// <summary>
	/// The item the pointer is over, in the same scene an instance id names — what the
	/// command family's <c>cursor</c> target reads. The answer classifies what the
	/// pointer holds rather than collapsing it to an id, because the ways there is no
	/// id to name have different remedies (move the pointer, or wait for the id the host
	/// assigns when the item enters the item domain). <paramref name="instanceId"/>
	/// carries the id only when the answer is <see cref="LocalCursorItemKind.Resolved"/>;
	/// it is zero otherwise.
	/// </summary>
	/// <remarks>
	/// The pointer here is the one the game's own pickup query reads — the mouse,
	/// resolved into the world — not the console's text caret.
	/// </remarks>
	LocalCursorItemKind ResolveCursorItem(out ulong instanceId);
}

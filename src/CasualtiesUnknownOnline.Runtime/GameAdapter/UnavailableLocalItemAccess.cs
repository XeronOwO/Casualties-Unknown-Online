using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The item-data family without a Game Adapter: there is no local scene to reach, so
/// a read answers "not held", a write is refused as not held, and the pointer names
/// nothing. The Runtime's console composition registers this and the adapter replaces
/// it — the same shape the other adapter seams use — so a build with no adapter answers
/// the verb instead of throwing.
///
/// <para>
/// The operator's answer is the one an EMPTY scene would give, and that is recorded
/// rather than papered over: the two configurations differ by whether a Game Adapter
/// is loaded at all, which the log and the build say, not by anything this verb can
/// see — so the port carries no separate "no scene" result for the verb to word.
/// </para>
/// </summary>
public sealed class UnavailableLocalItemAccess : ILocalItemAccess
{
	/// <summary>No local scene: nothing is held.</summary>
	public CharacterItemMsg? ReadLocalItem(ulong instanceId) => null;

	/// <summary>No local scene: nothing can be written.</summary>
	public LocalItemWriteResult WriteLocalItem(ulong instanceId, CharacterItemMsg desired) => LocalItemWriteResult.NotHeld;

	/// <summary>No local scene: the pointer names nothing.</summary>
	public LocalCursorItemKind ResolveCursorItem(out ulong instanceId)
	{
		instanceId = 0;
		return LocalCursorItemKind.None;
	}
}

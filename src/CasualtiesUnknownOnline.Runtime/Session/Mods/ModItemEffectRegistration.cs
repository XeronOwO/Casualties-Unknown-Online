using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One registered mod-authored item action, filed under the id of the item it was
/// registered for. It carries the owning mod id because two consumers need more
/// than the function itself: the Game Adapter names the mod in the log line a
/// throwing action produces, and the accessors the action is handed are what its
/// write set is judged against.
/// </summary>
internal sealed class ModItemEffectRegistration(string modId, string itemId, ModItemAction action, bool limbUse)
{
	/// <summary>The mod that registered it — the only mod allowed to have registered it.</summary>
	public string ModId { get; } = modId;

	/// <summary>The content id of the item it was registered for.</summary>
	public string ItemId { get; } = itemId;

	/// <summary>The registered function.</summary>
	public ModItemAction Action { get; } = action;

	/// <summary>True for the <c>useLimbAction</c> half, false for the <c>useAction</c> half.</summary>
	public bool IsLimbUse { get; } = limbUse;
}

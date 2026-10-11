using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One registered mod-authored liquid effect, filed under the id of the liquid it
/// was registered for. It carries the owning mod id because two consumers need
/// more than the function itself: the Game Adapter names the mod in the log line a
/// throwing effect produces, and the timed body a mod starts is keyed under a
/// prefix derived from that id so one mod's key can never collide with the game's
/// own timed ops or with another mod's.
/// </summary>
internal sealed class ModLiquidEffectRegistration(string modId, string liquidId, ModLiquidEffect effect, bool limbUse)
{
	/// <summary>The mod that registered it — the only mod allowed to have registered it.</summary>
	public string ModId { get; } = modId;

	/// <summary>The content id of the liquid it was registered for.</summary>
	public string LiquidId { get; } = liquidId;

	/// <summary>The registered function.</summary>
	public ModLiquidEffect Effect { get; } = effect;

	/// <summary>True for the <c>onHealthUse</c> half, false for the <c>onDrink</c> half.</summary>
	public bool IsLimbUse { get; } = limbUse;
}

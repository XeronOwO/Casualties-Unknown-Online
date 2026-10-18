using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The console summon verb's control surface a summon packet handler may use. <see cref="Session"/> rides beside it
/// for the same reason the give answer's handler carries it: the wire direction only proves the LOCAL role, so an
/// answer whose sender is not this session's host is a claim and the handler drops it by name.
/// </summary>
public interface IWorldSummonHandlerContext
{
	IWorldSummonControl WorldSummon { get; }

	ISessionControl Session { get; }
}

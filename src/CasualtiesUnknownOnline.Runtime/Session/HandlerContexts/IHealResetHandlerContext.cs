using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The console heal verb's control surface a heal packet handler may use. <see cref="Session"/>
/// rides beside it for the same reason the give/summon answer handlers carry it: the wire direction
/// only proves the LOCAL role, so a host-addressed frame whose sender is not this session's host is
/// a claim and the handler drops it by name.
/// </summary>
public interface IHealResetHandlerContext
{
	IHealResetControl HealReset { get; }

	ISessionControl Session { get; }
}

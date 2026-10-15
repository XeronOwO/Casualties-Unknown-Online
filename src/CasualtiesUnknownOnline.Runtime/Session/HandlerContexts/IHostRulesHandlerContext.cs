using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>The control surface used by the host-rule announcement handler: the session role the announcement is valid under, and the member-side store it writes.</summary>
public interface IHostRulesHandlerContext
{
	ISessionControl Session { get; }

	/// <summary>The host rules as this client last received them.</summary>
	AnnouncedHostRules HostRules { get; }
}

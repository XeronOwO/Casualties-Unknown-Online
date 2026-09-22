using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// Networking owns two halves that register at two different points of the
/// assembly order, so the module exposes two entry points instead of one:
/// <see cref="AddTransport"/> sits between the container seams and the session
/// (the session is built on the established Steam identity), while
/// <see cref="AddPacketPlane"/> sits after the session because the dispatcher,
/// the handlers and the receiver read the session control surface. The order is
/// the contract — it is what <c>GetServices&lt;ICuoService&gt;()</c> hands the
/// plugin as its update order — so each half is called where its block stood when
/// this composition lived inline, never reordered for tidiness.
/// </summary>
internal static class NetworkingComposition
{
	/// <summary>Registers the Steam and IP-direct transports, the router that exposes the active pair, and the identity ports.</summary>
	internal static void AddTransport(IServiceCollection services)
	{
		services.AddSingleton<SteamService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamService>());
		// SteamTransport takes the concrete SteamService, NOT the router: the
		// router itself composes SteamTransport, so injecting ISteamService here
		// would create a constructor cycle.
		services.AddSingleton(p => new SteamTransport(
			p.GetRequiredService<SteamService>(),
			p.GetRequiredService<ILogger<SteamTransport>>()));
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamTransport>());
		// Non-Steam transport path: TCP IP-direct host/guest. The router exposes
		// the ACTIVE pair (Steam or IP-direct) through the same INetworkTransport /
		// ISteamService contracts; the plugin switches it when an IP session starts.
		services.AddSingleton<IpDirectTransport>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<IpDirectTransport>());
		services.AddSingleton<IpDirectSteamService>();
		services.AddSingleton<CuoNetworkRouter>();
		services.AddSingleton<INetworkTransport>(p => p.GetRequiredService<CuoNetworkRouter>());
		// The save layer asks the ACTIVE transport who "we" are (§2); the router is the
		// only object that knows which transport is live, so it is the implementation.
		services.AddSingleton<ITransportIdentity>(p => p.GetRequiredService<CuoNetworkRouter>());
		services.AddSingleton<ISteamService>(p => p.GetRequiredService<CuoNetworkRouter>());
	}
}

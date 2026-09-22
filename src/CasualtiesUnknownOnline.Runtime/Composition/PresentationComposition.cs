using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The co-op communication surfaces: the text-chat domain and the transient
/// location ping. Both own a small local buffer, react to the world channel's
/// receive event and to the session end, and pump nothing — so they register
/// together and nowhere else. The in-game console is its own module
/// (<see cref="ConsoleComposition"/>): it is a local command surface that rides the
/// chat send path rather than a peer-facing channel.
/// </summary>
internal static class PresentationComposition
{
	/// <summary>Registers the text-chat and location-ping domains.</summary>
	internal static void AddCommunication(IServiceCollection services)
	{
		// Text-chat domain: the bounded recent-message buffer + send path (no
		// pump — it only reacts to the world channel's receive event and session end).
		services.AddSingleton<ChatService>();
		services.AddSingleton<IChatControl>(p => p.GetRequiredService<ChatService>());
		// Location-ping domain: the local one-marker-per-player buffer + the
		// middle-click double-click rule. No pump — it reacts to the world
		// channel's receive event, session end, and UI placement calls.
		services.AddSingleton<LocationPingService>();
		services.AddSingleton<ILocationPingControl>(p => p.GetRequiredService<LocationPingService>());
	}
}

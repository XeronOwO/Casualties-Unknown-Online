using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The session itself and the policy surfaces it carries: session control
/// (identity/flags/presence, created internally), the host-rule flags and their
/// write seam, and the localization service. It registers after the transport —
/// the Steam identity is what a session is built on — and before the packet plane,
/// whose dispatcher reads the session control surface.
/// </summary>
internal static class SessionComposition
{
	/// <summary>Registers session control, host rules and localization.</summary>
	internal static void AddSessionPolicy(IServiceCollection services)
	{
		// Session owns its state (identity/flags/presence, created internally);
		// consumers depend on the narrow ISessionControl surface, registered as
		// a factory so it resolves after the session is built (acyclic graph).
		// The session also reads the mod domain for the handshake list — the
		// IModListProvider factory resolves the registry (built later, no cycle:
		// the registry only depends on the logger).
		services.AddSingleton<SessionService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SessionService>());
		services.AddSingleton<ISessionControl>(p => p.GetRequiredService<SessionService>());

		// Minimal host-rules service: a stateless composition of the host-only
		// flags and the revives/respawn flags. Registered before the handlers so
		// HandshakeHandler can inject it for the late-join gate.
		services.AddSingleton<HostRulesService>();
		services.AddSingleton<IHostRules>(p => p.GetRequiredService<HostRulesService>());
		// The host-rule write seam defaults to unavailable; the plugin replaces
		// it with the BepInEx ConfigEntry-backed editor in extraRegistrations.
		services.AddSingleton<IHostRulesEditor>(new DisabledHostRulesEditor());

		// Localization service: reads the UI language from the config-backed
		// options monitor and falls back to English for missing keys.
		services.AddSingleton<LocalizationService>();
		services.AddSingleton<ILocalizationService>(p => p.GetRequiredService<LocalizationService>());
	}
}

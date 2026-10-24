using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
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
		// flags and the revives/respawn flags, over the host's own configuration on a
		// host and over the announced set on a member (the store is what the
		// announcement handler writes). Registered before the handlers so
		// HandshakeHandler can inject it for the late-join gate.
		services.AddSingleton<AnnouncedHostRules>();
		services.AddSingleton<HostRulesService>();
		services.AddSingleton<IHostRules>(p => p.GetRequiredService<HostRulesService>());
		// The announcing half: a member is told the host's rules the moment its
		// handshake completes and again on every change while the session runs. It is
		// resolved as a singleton so its subscriptions exist for the session's life —
		// the world-entry fan-out takes the narrow port and constructs it with the
		// packet plane — and it joins the pump because the CHANGE edge is the one whose
		// thread is not its own to choose: the callback records, the frame sends.
		services.AddSingleton<HostRulesAnnouncer>();
		services.AddSingleton<IHostRulesAnnouncement>(p => p.GetRequiredService<HostRulesAnnouncer>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<HostRulesAnnouncer>());
		// The admission seam's own question about a member's submission (which verb
		// this host opens to members) is answered from those rules through the
		// Application layer's port: the seam may not reference Runtime configuration
		// types, so the port is what crosses. It has no default implementation on
		// purpose — an unanswered rule must fail at construction rather than read as
		// consent, which is the family's default-OFF rule.
		// The cheat permission rides the same port beside the host rules: the FAMILY
		// SWITCH (one for every cheat verb, not one per verb) says whether the family is
		// open to members, and the roster says WHO may ask — a member needs both.
		// The roster and its announced copy are session-owned state (both drop it on
		// the session-end edge); the announcer subscribes the admission edge and is
		// constructed with the world-entry fan-out, which takes its narrow port.
		services.AddSingleton<CheatPermissions>();
		services.AddSingleton<AnnouncedCheatPermissions>();
		services.AddSingleton<CheatPermissionAnnouncer>();
		services.AddSingleton<ICheatPermissionAnnouncement>(p => p.GetRequiredService<CheatPermissionAnnouncer>());
		services.AddSingleton<IMemberCommandRules, SessionMemberCommandRules>();
		// The host-rule write seam defaults to unavailable; the plugin replaces
		// it with the BepInEx ConfigEntry-backed editor in extraRegistrations.
		services.AddSingleton<IHostRulesEditor>(new DisabledHostRulesEditor());
		// The console /gamerule write path: the host applies a change to its own
		// entry and answers with the value in force; a member's verb is a request
		// whose answer arrives on the wire. It sits after the rule surface and the
		// editor because it writes through them, and before the packet plane whose
		// handlers reach it through the per-message context.
		services.AddSingleton<HostRuleChangeService>();
		services.AddSingleton<IHostRuleChangeControl>(p => p.GetRequiredService<HostRuleChangeService>());

		// Localization service: reads the UI language from the config-backed
		// options monitor and falls back to English for missing keys.
		services.AddSingleton<LocalizationService>();
		services.AddSingleton<ILocalizationService>(p => p.GetRequiredService<LocalizationService>());

		// Session-liveness watchdogs and their notice slot. The host drops a member
		// whose Steam send queue has refused everything past the stall bound and keeps
		// playing; a guest that hears nothing from the host for the silence bound — or
		// whose own sends to the host keep being refused — ends its own session instead
		// of sitting in a frozen world. Both publish a rendered line into the slot the
		// Online UI drains into its status line. Registered after the session, whose
		// control surface they read.
		services.AddSingleton<ISessionNotices, SessionNotices>();
		services.AddSingleton<PeerSendStallWatchdog>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<PeerSendStallWatchdog>());
		services.AddSingleton<GuestHostSilenceWatchdog>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GuestHostSilenceWatchdog>());
		// The ruled courtesy line: the watched player is told, resolved here so the world it listens to is the one
		// the notice handler fires on (a plugin-side subscription failed to see the event).
		services.AddSingleton<SpectatorNoticeService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SpectatorNoticeService>());
	}
}

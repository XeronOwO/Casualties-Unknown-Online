namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The network capability a mod declares in its <see cref="CuoModAttribute"/>.
/// This is the mod's contract with the framework: the handshake consistency
/// check (the host validates the members' mod lists against this) and the
/// direction/routing rules derive from it. <see cref="Unspecified"/> is the
/// default (a forgotten declaration) — the registry REJECTS it at discovery
/// (fail-closed: a mod that does not state its network mode does not load),
/// so a missing declaration can never silently degrade to the most permissive
/// mode.
/// </summary>
public enum NetworkMode
{
	/// <summary>Not declared — rejected at discovery. Never a valid mode.</summary>
	Unspecified = 0,

	/// <summary>Runs only on the local client; no world state. May differ between members (a UI skin).</summary>
	ClientOnly,

	/// <summary>Pure visuals/audio; no gameplay effect. May differ between members.</summary>
	Cosmetic,

	/// <summary>Host-only logic (admin tools, world management). Only the host needs it.</summary>
	HostOnly,

	/// <summary>Both sides run the same logic and the mod's state is synchronized (a shared rules mod).</summary>
	Synchronized,

	/// <summary>The host is the authority for this mod's state; guests report into it.</summary>
	Authoritative,

	/// <summary>Every member must run the same version — missing/mismatched members are rejected at the handshake.</summary>
	RequiresAllPlayers,
}

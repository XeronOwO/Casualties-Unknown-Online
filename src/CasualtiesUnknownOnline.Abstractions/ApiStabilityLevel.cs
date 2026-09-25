namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// How far a public CUO surface is promised to mod authors. The level is declared
/// with <see cref="ApiStabilityAttribute"/> on the type (and, where one member
/// differs from its type, on that member); a surface without the attribute is
/// <see cref="Stable"/>, so a level is a deliberate declaration and never a
/// default that happened. The recorded surface — level included — is
/// <c>docs/contracts/abstractions-api-baseline.txt</c>, and the policy behind the
/// levels is <c>docs/en/reference/modification-policy.md</c>.
/// </summary>
public enum ApiStabilityLevel
{
	/// <summary>
	/// A frozen third-party contract: a mod may build against it and expect the
	/// shape to survive a CUO update. Adding, removing or changing a Stable
	/// member is a reviewed change — the baseline gate fails until the recorded
	/// surface is updated in the same commit, and a removal additionally names
	/// its reason.
	/// </summary>
	Stable,

	/// <summary>
	/// A designed surface that is still allowed to move: it is documented and
	/// usable, and a CUO update may change it with the policy doc's notice rule
	/// instead of the Stable freezing rule. It is the first stop of the
	/// promotion funnel (patch → several mods need it → Experimental → Stable).
	/// </summary>
	Experimental,

	/// <summary>
	/// A supported but game-build-sensitive surface: it works as documented, and
	/// its shape follows the game (or a curated registry keyed to the game) rather
	/// than CUO's own model, so a game update may move it with no CUO API
	/// decision involved.
	/// </summary>
	Advanced,

	/// <summary>
	/// Kept only until its consumers migrate: it still works, a new mod must not
	/// adopt it, and it may be removed once nothing uses it.
	/// </summary>
	Obsolete
}

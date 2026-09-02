namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The statically declared scope of a <see cref="ModStatusDefinition"/>.
/// CUCoreLib-style statuses attach either to a body or to a single limb; CUO
/// keeps that distinction explicit in the content schema so a future typed
/// player-status domain can route updates without guessing.
/// </summary>
public enum ModStatusScope
{
	/// <summary>Body-level status state.</summary>
	Body = 1,

	/// <summary>Per-limb status state; the runtime instance also needs a limb index.</summary>
	Limb = 2,
}

using System;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Declares the <see cref="ApiStabilityLevel"/> of a public CUO surface
/// (<c>docs/api/advanced-modification-policy.md`). A type without the attribute
/// is <see cref="ApiStabilityLevel.Stable"/>; a member without the attribute
/// carries its declaring type's level. The attribute is a declaration for
/// authors and for the public-surface gate
/// (<c>docs/api/abstractions-api-baseline.txt</c>) — it is not an enforcement
/// mechanism of its own, and it changes nothing at runtime.
/// </summary>
[AttributeUsage(
	AttributeTargets.Interface
	| AttributeTargets.Class
	| AttributeTargets.Struct
	| AttributeTargets.Enum
	| AttributeTargets.Delegate
	| AttributeTargets.Method
	| AttributeTargets.Property
	| AttributeTargets.Field
	| AttributeTargets.Event,
	Inherited = false)]
public sealed class ApiStabilityAttribute(ApiStabilityLevel level) : Attribute
{
	/// <summary>The declared level of the annotated surface.</summary>
	public ApiStabilityLevel Level { get; } = level;
}

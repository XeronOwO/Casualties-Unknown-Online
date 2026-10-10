namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The light behavior of a custom item. This is the contract a consumer reads:
/// <see cref="IModItemDefinition.Light"/> is interface-typed, so a mod that
/// computes a light value hands over its own implementation instead of filling in
/// the framework's.
///
/// <see cref="ModItemLight"/> is the framework's ready-made implementation: use it
/// when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back
/// a filled default for the members it does not touch — never inheritance from
/// the data class, which stays <c>sealed</c>.
/// </summary>
public interface IModItemLight
{
	/// <summary>Light intensity.</summary>
	float Intensity { get; }

	/// <summary>Light color red channel (0..1).</summary>
	float ColorR { get; }

	/// <summary>Light color green channel (0..1).</summary>
	float ColorG { get; }

	/// <summary>Light color blue channel (0..1).</summary>
	float ColorB { get; }

	/// <summary>Light color alpha channel (0..1).</summary>
	float ColorA { get; }

	/// <summary>Radial/edge falloff softness (0 = sharp, 1 = soft).</summary>
	float FalloffIntensity { get; }

	/// <summary>Outer radius for point/2D types.</summary>
	float OuterRadius { get; }

	/// <summary>Inner radius for point/2D types.</summary>
	float InnerRadius { get; }

	/// <summary>Outer cone angle in degrees.</summary>
	float OuterAngle { get; }

	/// <summary>Inner cone angle in degrees.</summary>
	float InnerAngle { get; }

	/// <summary>Underlying Unity 2D light shape.</summary>
	ModLightType LightType { get; }

	/// <summary>Local X offset of the spawned light.</summary>
	float OffsetX { get; }

	/// <summary>Local Y offset of the spawned light.</summary>
	float OffsetY { get; }

	/// <summary>Local Z-axis rotation in degrees.</summary>
	float Rotation { get; }

	/// <summary>Whether a <c>LightItem</c> helper is added automatically.</summary>
	bool AddLightItem { get; }
}

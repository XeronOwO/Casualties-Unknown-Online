namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Light behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter materializes a vanilla <c>Light2D</c> child
/// and, when requested, a <c>LightItem</c> helper on the runtime item template.
/// </summary>
public sealed class ModItemLight : IModItemLight
{
	/// <summary>Light intensity.</summary>
	public float Intensity { get; set; } = 0.75f;

	/// <summary>Light color red channel (0..1).</summary>
	public float ColorR { get; set; } = 1f;

	/// <summary>Light color green channel (0..1).</summary>
	public float ColorG { get; set; } = 1f;

	/// <summary>Light color blue channel (0..1).</summary>
	public float ColorB { get; set; } = 1f;

	/// <summary>Light color alpha channel (0..1).</summary>
	public float ColorA { get; set; } = 1f;

	/// <summary>Radial/edge falloff softness (0 = sharp, 1 = soft).</summary>
	public float FalloffIntensity { get; set; } = 0.5f;

	/// <summary>Outer radius for point/2D types.</summary>
	public float OuterRadius { get; set; } = 7.5f;

	/// <summary>Inner radius for point/2D types.</summary>
	public float InnerRadius { get; set; }

	/// <summary>Outer cone angle in degrees.</summary>
	public float OuterAngle { get; set; } = 360f;

	/// <summary>Inner cone angle in degrees.</summary>
	public float InnerAngle { get; set; } = 360f;

	/// <summary>Underlying Unity 2D light shape.</summary>
	public ModLightType LightType { get; set; } = ModLightType.Point;

	/// <summary>Local X offset of the spawned light.</summary>
	public float OffsetX { get; set; }

	/// <summary>Local Y offset of the spawned light.</summary>
	public float OffsetY { get; set; }

	/// <summary>Local Z-axis rotation in degrees.</summary>
	public float Rotation { get; set; }

	/// <summary>Whether a <c>LightItem</c> helper is added automatically.</summary>
	public bool AddLightItem { get; set; } = true;
}

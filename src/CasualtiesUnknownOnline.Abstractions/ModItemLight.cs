using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Light behavior for a custom item. The values are plain data in
/// Abstractions; the Game Adapter materializes a vanilla <c>Light2D</c> child
/// and, when requested, a <c>LightItem</c> helper on the runtime item template.
/// </summary>
[DataContract]
public sealed class ModItemLight
{
	/// <summary>Light intensity.</summary>
	[DataMember(Order = 1)]
	public float Intensity { get; set; } = 0.75f;

	/// <summary>Light color red channel (0..1).</summary>
	[DataMember(Order = 2)]
	public float ColorR { get; set; } = 1f;

	/// <summary>Light color green channel (0..1).</summary>
	[DataMember(Order = 3)]
	public float ColorG { get; set; } = 1f;

	/// <summary>Light color blue channel (0..1).</summary>
	[DataMember(Order = 4)]
	public float ColorB { get; set; } = 1f;

	/// <summary>Light color alpha channel (0..1).</summary>
	[DataMember(Order = 5)]
	public float ColorA { get; set; } = 1f;

	/// <summary>Radial/edge falloff softness (0 = sharp, 1 = soft).</summary>
	[DataMember(Order = 6)]
	public float FalloffIntensity { get; set; } = 0.5f;

	/// <summary>Outer radius for point/2D types.</summary>
	[DataMember(Order = 7)]
	public float OuterRadius { get; set; } = 7.5f;

	/// <summary>Inner radius for point/2D types.</summary>
	[DataMember(Order = 8)]
	public float InnerRadius { get; set; }

	/// <summary>Outer cone angle in degrees.</summary>
	[DataMember(Order = 9)]
	public float OuterAngle { get; set; } = 360f;

	/// <summary>Inner cone angle in degrees.</summary>
	[DataMember(Order = 10)]
	public float InnerAngle { get; set; } = 360f;

	/// <summary>Underlying Unity 2D light shape.</summary>
	[DataMember(Order = 11)]
	public ModLightType LightType { get; set; } = ModLightType.Point;

	/// <summary>Local X offset of the spawned light.</summary>
	[DataMember(Order = 12)]
	public float OffsetX { get; set; }

	/// <summary>Local Y offset of the spawned light.</summary>
	[DataMember(Order = 13)]
	public float OffsetY { get; set; }

	/// <summary>Local Z-axis rotation in degrees.</summary>
	[DataMember(Order = 14)]
	public float Rotation { get; set; }

	/// <summary>Whether a <c>LightItem</c> helper is added automatically.</summary>
	[DataMember(Order = 15)]
	public bool AddLightItem { get; set; } = true;
}

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Unity 2D light shapes for a custom light-emitting item. The values mirror
/// the URP <c>Light2D.LightType</c> enum so the Game Adapter can translate the
/// DTO using reflection (URP is not in the reference graph).
/// </summary>
public enum ModLightType
{
	/// <summary>Parametric 2D light shape.</summary>
	Parametric = 0,

	/// <summary>Freeform 2D light shape.</summary>
	Freeform = 1,

	/// <summary>Sprite-shaped 2D light.</summary>
	Sprite = 2,

	/// <summary>Point light.</summary>
	Point = 3,

	/// <summary>Global light.</summary>
	Global = 4
}

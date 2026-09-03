namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Firing mode for a custom firearm. Mirrors the vanilla
/// <c>GunScript.FiringMode</c> enum.
/// </summary>
public enum ModGunFiringMode
{
	/// <summary>Pump-action firing.</summary>
	Pump = 0,

	/// <summary>Semi-automatic firing.</summary>
	SemiAuto = 1,

	/// <summary>Fully automatic firing.</summary>
	Auto = 2
}

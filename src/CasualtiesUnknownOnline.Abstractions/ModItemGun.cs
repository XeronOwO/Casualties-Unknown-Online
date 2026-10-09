namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Firearm behavior for a custom item. Nullable fields keep the base prefab's
/// vanilla <c>GunScript</c> defaults when an author does not override a value.
/// The values are plain data in Abstractions; the Game Adapter configures the
/// vanilla <c>GunScript</c> component and installs the trigger
/// <c>ItemInfo.useAction</c> at item registration time.
/// </summary>
public sealed class ModItemGun
{
	/// <summary>Optional ammo type.</summary>
	public ModGunAmmoType? AmmoType { get; set; }

	/// <summary>Optional firing mode.</summary>
	public ModGunFiringMode? FiringMode { get; set; }

	/// <summary>Optional feed type.</summary>
	public ModGunFeedType? FeedType { get; set; }

	/// <summary>Optional magazine capacity.</summary>
	public int? MagCapacity { get; set; }

	/// <summary>Optional recoil/knockback force.</summary>
	public float? KnockBack { get; set; }

	/// <summary>Optional structure damage per shot.</summary>
	public float? StructureDamage { get; set; }

	/// <summary>Optional animal damage per shot.</summary>
	public float? AnimalDamage { get; set; }

	/// <summary>Optional loudness.</summary>
	public float? Loudness { get; set; }

	/// <summary>Optional gas/rack time.</summary>
	public float? DesiredGasTime { get; set; }

	/// <summary>Optional shots per trigger pull.</summary>
	public int? ShotsPerFire { get; set; }

	/// <summary>Optional vertical spread.</summary>
	public float? VerticalSpread { get; set; }

	/// <summary>Optional condition lost per shot.</summary>
	public float? ConditionLossPerShot { get; set; }
}

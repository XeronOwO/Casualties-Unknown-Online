using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Firearm behavior for a custom item. Nullable fields keep the base prefab's
/// vanilla <c>GunScript</c> defaults when an author does not override a value.
/// The values are plain data in Abstractions; the Game Adapter configures the
/// vanilla <c>GunScript</c> component and installs the trigger
/// <c>ItemInfo.useAction</c> at item registration time.
/// </summary>
[DataContract]
public sealed class ModItemGun
{
	/// <summary>Optional ammo type.</summary>
	[DataMember(Order = 1)]
	public ModGunAmmoType? AmmoType { get; set; }

	/// <summary>Optional firing mode.</summary>
	[DataMember(Order = 2)]
	public ModGunFiringMode? FiringMode { get; set; }

	/// <summary>Optional feed type.</summary>
	[DataMember(Order = 3)]
	public ModGunFeedType? FeedType { get; set; }

	/// <summary>Optional magazine capacity.</summary>
	[DataMember(Order = 4)]
	public int? MagCapacity { get; set; }

	/// <summary>Optional recoil/knockback force.</summary>
	[DataMember(Order = 5)]
	public float? KnockBack { get; set; }

	/// <summary>Optional structure damage per shot.</summary>
	[DataMember(Order = 6)]
	public float? StructureDamage { get; set; }

	/// <summary>Optional animal damage per shot.</summary>
	[DataMember(Order = 7)]
	public float? AnimalDamage { get; set; }

	/// <summary>Optional loudness.</summary>
	[DataMember(Order = 8)]
	public float? Loudness { get; set; }

	/// <summary>Optional gas/rack time.</summary>
	[DataMember(Order = 9)]
	public float? DesiredGasTime { get; set; }

	/// <summary>Optional shots per trigger pull.</summary>
	[DataMember(Order = 10)]
	public int? ShotsPerFire { get; set; }

	/// <summary>Optional vertical spread.</summary>
	[DataMember(Order = 11)]
	public float? VerticalSpread { get; set; }

	/// <summary>Optional condition lost per shot.</summary>
	[DataMember(Order = 12)]
	public float? ConditionLossPerShot { get; set; }
}

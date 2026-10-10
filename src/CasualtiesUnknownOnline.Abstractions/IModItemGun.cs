namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The firearm behavior of a custom item. This is the contract a consumer reads:
/// <see cref="IModItemDefinition.Gun"/> is interface-typed, so a mod that computes
/// a gun value hands over its own implementation instead of filling in the
/// framework's.
///
/// Every member is nullable because an unset one keeps the base prefab's vanilla
/// <c>GunScript</c> default; a mod-authored implementation that does not carry a
/// member returns null for it and changes nothing.
///
/// <see cref="ModItemGun"/> is the framework's ready-made implementation: use it
/// when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back
/// a filled default for the members it does not touch — never inheritance from
/// the data class, which stays <c>sealed</c>.
/// </summary>
public interface IModItemGun
{
	/// <summary>Optional ammo type.</summary>
	ModGunAmmoType? AmmoType { get; }

	/// <summary>Optional firing mode.</summary>
	ModGunFiringMode? FiringMode { get; }

	/// <summary>Optional feed type.</summary>
	ModGunFeedType? FeedType { get; }

	/// <summary>Optional magazine capacity.</summary>
	int? MagCapacity { get; }

	/// <summary>Optional recoil/knockback force.</summary>
	float? KnockBack { get; }

	/// <summary>Optional structure damage per shot.</summary>
	float? StructureDamage { get; }

	/// <summary>Optional animal damage per shot.</summary>
	float? AnimalDamage { get; }

	/// <summary>Optional loudness.</summary>
	float? Loudness { get; }

	/// <summary>Optional gas/rack time.</summary>
	float? DesiredGasTime { get; }

	/// <summary>Optional shots per trigger pull.</summary>
	int? ShotsPerFire { get; }

	/// <summary>Optional vertical spread.</summary>
	float? VerticalSpread { get; }

	/// <summary>Optional condition lost per shot.</summary>
	float? ConditionLossPerShot { get; }
}
